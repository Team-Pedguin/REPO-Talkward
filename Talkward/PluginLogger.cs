using BepInEx.Logging;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Talkward;

public class PluginLogger : ILogger
{
    private static ManualLogSource Logger => Plugin.Logger!;
    private readonly string _categoryName;
    private readonly LogLevel _level;

    public PluginLogger(string categoryName, LogLevel level = LogLevel.Information)
    {
        _categoryName = categoryName;
        _level = level;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel < _level) return;

        var message = formatter(state, exception);

        // translate Microsoft.Extensions.Logging.LogLevel to BepInEx.Logging.LogLevel
        var bepInExLevel = logLevel switch
        {
            LogLevel.None => BepInEx.Logging.LogLevel.None,
            LogLevel.Trace => BepInEx.Logging.LogLevel.Debug,
            LogLevel.Debug => BepInEx.Logging.LogLevel.Debug,
            LogLevel.Information => BepInEx.Logging.LogLevel.Info,
            LogLevel.Warning => BepInEx.Logging.LogLevel.Warning,
            LogLevel.Error => BepInEx.Logging.LogLevel.Error,
            LogLevel.Critical => BepInEx.Logging.LogLevel.Fatal,
            _ => BepInEx.Logging.LogLevel.None
        };

        Logger.Log(bepInExLevel, (eventId, message, state, exception));
    }

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= _level;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => new LoggerScope<TState>(this, state);
}