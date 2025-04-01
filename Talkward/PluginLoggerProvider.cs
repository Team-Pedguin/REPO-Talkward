using Microsoft.Extensions.Logging;

namespace Talkward;

public class PluginLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new PluginLogger(categoryName);
    }

    public void Dispose()
    {
    }
}