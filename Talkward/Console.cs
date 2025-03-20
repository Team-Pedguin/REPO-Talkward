namespace Talkward;

public static class Console
{
    public delegate bool TryExecutedEventHandler(string commandLine);

    public static event TryExecutedEventHandler? TryExecute;

    public static IEnumerable<TryExecutedEventHandler> GetTryExecuteHandlers()
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        return TryExecute?.GetInvocationList()?.Cast<TryExecutedEventHandler>() ?? [];
    }
}