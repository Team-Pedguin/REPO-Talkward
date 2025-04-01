namespace Talkward;

public partial class SinglyLinkedList<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint Max(nuint a, nuint b) => a > b ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nuint Min(nuint a, nuint b) => a < b ? a : b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkAsFragmented()
        => Interlocked.Exchange(ref _lastDefragmentedVersion, ~_version);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private nuint GetLinearIndex(ref Node node)
        => node.GetLinearIndex(this);
}