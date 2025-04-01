namespace Talkward;

public interface IPlayerLoopRegistrar
{
    void RegisterUpdateFunction(Type type, Action updateDelegate);
}