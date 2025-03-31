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
    /// Accessor for the contained value.
    /// </summary>
    /// <remarks>
    /// Note: Compile with ATOMIC_BOOLEAN_MEM_ORDER_SEQ_CST to use sequential consistency,
    /// otherwise acquire-release consistency is used.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Get() => Value;

    /// <summary>
    /// Accessor for the contained value.
    /// </summary>
    /// <remarks>
    /// Note: Compile with ATOMIC_BOOLEAN_MEM_ORDER_SEQ_CST to use sequential consistency,
    /// otherwise acquire-release consistency is used.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Get(out bool value) => value = Value;

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

[PublicAPI]
public static class AtomicBooleanHelpers
{
    /// <summary>
    /// Sets the value to <see langref="true"/>.
    /// </summary>
    /// <param name="b">The <see cref="AtomicBoolean"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(ref this AtomicBoolean b) => b.Value = true;

    /// <summary>
    /// Sets the value to <paramref name="value"/>.
    /// </summary>
    /// <param name="b">The <see cref="AtomicBoolean"/>.</param>
    /// <param name="value">The value to set, true or false.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(ref this AtomicBoolean b, bool value)
        => Interlocked.Exchange(ref Unsafe.As<AtomicBoolean, int>(ref b), UnsafeBitCast<bool, byte>(value));

    /// <summary>
    /// Sets the value to <paramref name="value"/> if it is currently not.
    /// </summary>
    /// <param name="b">The <see cref="AtomicBoolean"/>.</param>
    /// <param name="value">The value to set, true or false.</param>
    /// <returns>True if the value was set, false if it was already set to <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySet(ref this AtomicBoolean b, bool value)
        => value ? b.TrySet() : b.TryClear();

    /// <summary>
    /// Sets the value to true if it is currently false.
    /// </summary>
    /// <param name="b">The <see cref="AtomicBoolean"/>.</param>
    /// <returns>True if the value was set to true, false if it was already true.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySet(ref this AtomicBoolean b)
        => Interlocked.CompareExchange(ref Unsafe.As<AtomicBoolean, int>(ref b), 1, 0) == 0;

    /// <summary>
    /// Sets the value to false if it is currently true.
    /// </summary>
    /// <param name="b">The <see cref="AtomicBoolean"/>.</param>
    /// <returns>True if the value was set to false, false if it was already false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryClear(ref this AtomicBoolean b)
        => Interlocked.CompareExchange(ref Unsafe.As<AtomicBoolean, int>(ref b), 0, 1) != 0;
}