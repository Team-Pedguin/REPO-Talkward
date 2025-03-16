namespace Talkward;

public static class ThrowHelper
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw<T>()
        where T : Exception, new()
        => throw new T();
}