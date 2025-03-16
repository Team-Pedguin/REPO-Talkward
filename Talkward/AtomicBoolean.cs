using System.Threading;
using System.Runtime.CompilerServices;

namespace Talkward;

[PublicAPI]
public struct AtomicBoolean
{
    private int _value;

    public AtomicBoolean(bool value)
        => Value = value;

    /// <summary>
    /// Atomic accessor and mutator for the contained value.
    /// </summary>
    /// <remarks>
    /// This property is thread-safe and can be used to read or write the value.
    /// Note: Compile with ATOMIC_BOOLEAN_MEM_ORDER_SEQ_CST to use sequential consistency,
    /// otherwise acquire-release consistency is used.
    /// </remarks> 
    public bool Value
    {
#if ATOMIC_BOOLEAN_MEM_ORDER_SEQ_CST
        // sequential consistency
        readonly get => UnsafeBitCast<byte, bool>((byte)Interlocked.CompareExchange(ref Unsafe.AsRef(_value), 0, 0));
#else
        // acquire-release consistency
        readonly get => UnsafeBitCast<byte, bool>((byte) Volatile.Read(ref Unsafe.AsRef(_value)));
#endif
        set => Interlocked.Exchange(ref _value, UnsafeBitCast<bool, byte>(value));
    }

    /// <summary>
    /// Sets the value to <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value to set, true or false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(bool value) => Value = value;

    /// <summary>
    /// Sets the value to <paramref name="value"/> if it is currently not.
    /// </summary>
    /// <param name="value">The value to set, true or false.</param>
    /// <returns>True if the value was set, false if it was already set to <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySet(bool value)
        => value ? TrySet() : TryClear();

    /// <summary>
    /// Sets the value to true if it is currently false.
    /// </summary>
    /// <returns>True if the value was set to true, false if it was already true.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySet()
        => Interlocked.CompareExchange(ref _value, 1, 0) == 0;

    /// <summary>
    /// Sets the value to false if it is currently true.
    /// </summary>
    /// <returns>True if the value was set to false, false if it was already false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryClear()
        => Interlocked.CompareExchange(ref _value, 0, 1) != 0;

    /// <summary>
    /// Accessor for the contained value.
    /// </summary>
    /// <remarks>
    /// Note: Compile with ATOMIC_BOOLEAN_MEM_ORDER_SEQ_CST to use sequential consistency,
    /// otherwise acquire-release consistency is used.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get() => Value;

    /// <summary>
    /// Accessor for the contained value.
    /// </summary>
    /// <remarks>
    /// Note: Compile with ATOMIC_BOOLEAN_MEM_ORDER_SEQ_CST to use sequential consistency,
    /// otherwise acquire-release consistency is used.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Get(out bool value) => value = Value;

    /// <summary>
    /// Implicit conversion from <see cref="AtomicBoolean"/> to <see cref="bool"/>.
    /// </summary>
    /// <remarks>
    /// Note: Compile with ATOMIC_BOOLEAN_MEM_ORDER_SEQ_CST to use sequential consistency,
    /// otherwise acquire-release consistency is used.
    /// </remarks>
    /// <param name="atomicBoolean">The <see cref="AtomicBoolean"/> to convert.</param>
    /// <returns>The contained value.</returns>
    public static implicit operator bool(AtomicBoolean atomicBoolean)
        => atomicBoolean.Value;

    public static explicit operator AtomicBoolean(bool value)
        => new(value);
}