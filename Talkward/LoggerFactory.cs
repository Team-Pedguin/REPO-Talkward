using Microsoft.Extensions.Logging;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Talkward;

public class LoggerFactory : ILoggerFactory
{
    private readonly LinkedList<ILoggerProvider> _providers = [];

    void IDisposable.Dispose()
    {
        lock (_providers)
            foreach (var provider in _providers)
            {
                try
                {
                    provider.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
    }

    public ILogger CreateLogger(string? categoryName)
    {
        lock (_providers)
            foreach (var provider in _providers)
            {
                ILogger? logger;
                try
                {
                    logger = provider.CreateLogger(categoryName!);
                }
                catch
                {
                    continue;
                }

                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (logger != null)
                    return logger;
            }

        NoProvidableLogger();
        return null;
    }

    public ILogger<T> CreateLogger<T>()
        => new LoggerWrapper<T>(CreateLogger(typeof(T).Name));

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void NoProvidableLogger()
        => throw new InvalidOperationException("No provider(s) or logger(s) available from provider(s).");

    public void AddProvider(ILoggerProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        lock (_providers)
        {
            var node = _providers.Find(provider);
            if (node is null)
                _providers.AddLast(provider);
        }
    }

    public void RemoveProvider(ILoggerProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        lock (_providers)
        {
            var node = _providers.Find(provider);
            if (node is null)
                _providers.Remove(provider);
        }
    }
}