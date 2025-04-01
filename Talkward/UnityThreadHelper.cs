using System.Collections.Concurrent;
using System.Reflection;
using BepInEx.Logging;

namespace Talkward;

[PublicAPI]
public static class UnityThreadHelper
{
    static UnityThreadHelper()
    {
        if (UnitTestSignal.Active) return;
        InitializeForUnity();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void InitializeForUnity()
    {
        _loopRegistrar = new UnityPlayerLoopRegistrar();
        _logger = Plugin.Logger;
        _timeProvider = new UnityTimeProvider();
    }

    internal static ITimeProvider _timeProvider;
    internal static IPlayerLoopRegistrar _loopRegistrar;
    internal static ManualLogSource? _logger;

    private readonly struct CallbackState
    {
        public readonly SendOrPostCallback Callback;
        public readonly object? State;
        public readonly ManualResetEventSlim? Completed;

        public CallbackState(SendOrPostCallback callback, object? state, ManualResetEventSlim? completed)
        {
            Callback = callback;
            State = state;
            Completed = completed;
        }
    }

    private struct ScheduledCallbackState
    {
        public readonly double UnscaledTime;
        public readonly SendOrPostCallback Callback;
        public readonly object? State;
        public AtomicBoolean Executed;

        public ScheduledCallbackState(double unscaledTime, SendOrPostCallback callback, object? state)
        {
            UnscaledTime = unscaledTime;
            Callback = callback;
            State = state;
            Executed = default;
        }
    }

    private static ConcurrentQueue<CallbackState> _callbackQueue = new();

    /// <summary>
    /// A dictionary of scheduled callbacks.
    /// The key is the unscaled time as a long (so it the specific second),
    /// and the value is a list of callbacks scheduled for that second, in no specific order.
    /// </summary>
    private static ConcurrentDictionary<long, RefList<ScheduledCallbackState>> _scheduled = new();

    internal static AtomicBoolean Initialized;

    internal static void Initialize()
    {
        if (!Initialized.TrySet()) return;

        _loopRegistrar.RegisterUpdateFunction(typeof(UnityThreadHelper), Run);
    }

    internal static long _nextSecondToRun;

    internal static void Run()
    {
        while (_callbackQueue.TryDequeue(out var queued))
        {
            try
            {
                queued.Callback(queued.State);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    $"UnityThreadHelper Queued: {ex.GetType().FullName} in {queued.Callback.Method.DeclaringType?.FullName}.{queued.Callback.Method.Name}\n{ex}");
            }
            finally
            {
                queued.Completed?.Set();
            }
        }

        var time = _timeProvider.UnscaledTime;
        var thisSecond = (long) time;
        
        // Process due callbacks
        var nextSecondToRun = _nextSecondToRun;
        var nextDueSecond = long.MaxValue;
        var missedAny = false;
        for (var dueSecond = nextSecondToRun; dueSecond <= thisSecond; dueSecond++)
        {
            // lock on the list members, not on the ConcurrentDictionary ffs
            // ReSharper disable once InconsistentlySynchronizedField
            if (!_scheduled.TryGetValue(dueSecond, out var listOfScheduled))
                continue;

            lock (listOfScheduled)
            {
                var executedCount = 0;
                ref var arrayOfScheduled = ref listOfScheduled.Array;
                var lengthOfScheduled = listOfScheduled.Count;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (Unsafe.IsNullRef(ref arrayOfScheduled) || arrayOfScheduled is null)
                    continue;
                for (var i = 0; i < lengthOfScheduled; i++)
                {
                    ref var scheduled = ref arrayOfScheduled[i];

                    if (Unsafe.IsNullRef(ref scheduled))
                        break;

                    if (scheduled.Executed)
                    {
                        ++executedCount;
                        continue;
                    }

                    if (scheduled.UnscaledTime > time)
                    {
                        // not yet due
                        //missedAny = true;
                        if (dueSecond < nextDueSecond)
                            nextDueSecond = dueSecond;
                        continue;
                    }

                    ++executedCount;

                    try
                    {
                        scheduled.Callback(scheduled.State);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            $"UnityThreadHelper Scheduler: {ex.GetType().FullName} in {scheduled.Callback.Method.DeclaringType?.FullName}.{scheduled.Callback.Method.Name}\n{ex}");
                    }
                    finally
                    {
                        // this needs scheduled to be a mutable ref
                        scheduled.Executed.Set();
                    }
                }

                // remove the list if all callbacks have been executed for this second
                if (listOfScheduled.Count == executedCount)
                    _scheduled.TryRemove(dueSecond, out _);
                else
                {
                    if (dueSecond < nextDueSecond)
                        nextDueSecond = dueSecond;
                    missedAny = true;
                }
            }
        }

        if (!missedAny)
        {
            _nextSecondToRun = thisSecond;
            return;
        }
        if (nextDueSecond != long.MaxValue)
        {
            _nextSecondToRun = nextDueSecond;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Post(SendOrPostCallback callback, object? state)
        => _callbackQueue.Enqueue(new CallbackState(callback, state, null));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Send(SendOrPostCallback callback, object? state)
    {
        using var completed = new ManualResetEventSlim(false);
        _callbackQueue.Enqueue(new CallbackState(callback, state, completed));
        completed.Wait();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Schedule(double unscaledTime, SendOrPostCallback callback, object? state)
    {
        var time = _timeProvider.UnscaledTime;
        if (time >= unscaledTime)
        {
            Post(callback, state);
            return;
        }

        var scheduledSecond = (long)unscaledTime;

        // lock on the list members, not on the ConcurrentDictionary ffs
        // ReSharper disable once InconsistentlySynchronizedField
        var list = _scheduled.GetOrAdd(scheduledSecond, _ => []);
        lock (list)
            list.Add(new ScheduledCallbackState(unscaledTime, callback, state));

        var nextSecondToRun = _nextSecondToRun;
        if (scheduledSecond < nextSecondToRun)
            Interlocked.CompareExchange(ref _nextSecondToRun, scheduledSecond, nextSecondToRun);
    }

    internal static void ResetForTesting()
    {
        _callbackQueue = new ConcurrentQueue<CallbackState>();
        _scheduled = new ConcurrentDictionary<long, RefList<ScheduledCallbackState>>();
        _nextSecondToRun = (long)_timeProvider.UnscaledTime; // Initialize to current time
    }
}

