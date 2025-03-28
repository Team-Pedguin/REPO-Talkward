using UnityEngine;

namespace Talkward;

[Serializable]
internal class UnreachableException : Exception
{
    protected UnreachableException()
        : base("Unreachable code reached.")
    {
    }

    protected UnreachableException(string message)
        : base(message)
    {
    }

    protected UnreachableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    [MethodImpl(MethodImplOptions.NoInlining), StackTraceHidden, HideInCallstack]
    public static UnreachableException Create() => new();

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    public static void Throw() => throw new();
}