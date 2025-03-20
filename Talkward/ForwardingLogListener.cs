using BepInEx.Logging;

namespace Talkward;

[PublicAPI]
public class ForwardingLogListener : ILogListener
{
    public event EventHandler<LogEventArgs>? LogEvent;

    void ILogListener.LogEvent(object sender, LogEventArgs eventArgs)
        => LogEvent?.Invoke(sender, eventArgs);

    public void Dispose()
    {
    }
}