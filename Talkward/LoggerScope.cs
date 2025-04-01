using Microsoft.Extensions.Logging;

namespace Talkward;

[PublicAPI]
public sealed class LoggerScope<T> : IDisposable
{
    private readonly ILogger _logger;
    private readonly T _state;
    
    public T State => _state;

    public LoggerScope(ILogger logger, T state)
    {
        _logger = logger;
        _state = state;
        _logger.LogTrace("[Entering {State}]", state);
    }

    public void Dispose() => _logger.LogTrace("[Exiting {State}]", _state);

    public static LoggerScope<T> Create(ILogger logger, T state) => new(logger, state);
}