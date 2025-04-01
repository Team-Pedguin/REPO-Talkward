using UnityEngine.LowLevel;

namespace Talkward;

public sealed class UnityPlayerLoopRegistrar : IPlayerLoopRegistrar
{
    public void RegisterUpdateFunction(Type type, Action updateDelegate)
    {
        var pl = PlayerLoop.GetCurrentPlayerLoop();
        pl.subSystemList = pl.subSystemList.Append(new PlayerLoopSystem
        {
            type = type,
            // wonder if this will work lol
            updateDelegate = Unsafe.As<Action, PlayerLoopSystem.UpdateFunction>(ref updateDelegate)
        }).ToArray();
        PlayerLoop.SetPlayerLoop(pl);
    }
}