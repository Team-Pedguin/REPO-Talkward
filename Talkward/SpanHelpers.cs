namespace Talkward;

internal static class SpanHelpers {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T At<T>(in this Span<T> nodes, nuint offset)
        => ref Unsafe.Add(ref nodes.GetPinnableReference(), offset);
}