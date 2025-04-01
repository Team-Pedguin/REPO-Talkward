namespace Talkward;

internal static class UnityThreadHelperInitializer
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void InitializeForUnity()
    {
        UnityThreadHelper._loopRegistrar = new UnityPlayerLoopRegistrar();
        UnityThreadHelper._logger = Plugin.Logger;
        UnityThreadHelper._timeProvider = new UnityTimeProvider();
    }
}