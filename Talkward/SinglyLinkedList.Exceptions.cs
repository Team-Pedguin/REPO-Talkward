using UnityEngine;

namespace Talkward;

public partial class SinglyLinkedList<T>
{
    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void ArgumentOutOfRange(string name)
        => throw new ArgumentOutOfRangeException(name);
    
    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void ArgumentNull(string name)
        => throw new ArgumentNullException(name);

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void IndexOutOfRange()
        => throw new IndexOutOfRangeException();

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void EnumeratorInvalidated()
        => throw new InvalidOperationException(
            "Enumerator has been invalidated because the list was significantly modified.");

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void InvalidEnumeratorPosition()
        => throw new InvalidOperationException("Enumerator is not positioned on a valid element.");
}