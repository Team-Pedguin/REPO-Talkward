using Microsoft.Extensions.Logging;

namespace Talkward;


[ProviderAlias("PluginLogger")]
public class PluginLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
        => new PluginLogger(categoryName);

    public void Dispose()
    {
    }
}