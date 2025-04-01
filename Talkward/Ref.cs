namespace Talkward;

/// <summary>
/// .NET Standard compatible single-item reference via Span.
/// Mimics the behavior of a ref field in later C# versions.
/// </summary>
[PublicAPI]
internal ref struct Ref<T>
{
    public Span<T> Span;

    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Span.IsEmpty;
    }

    public ref T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Span.GetPinnableReference();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ref(Span<T> span) => Span = span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ref(ref T value) => Span = MemoryMarshal.CreateSpan(ref value, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Ref<T>(Span<T> span) => new(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<T>(Ref<T> reference) => reference.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator T(Ref<T> reference) => reference.Value;
}