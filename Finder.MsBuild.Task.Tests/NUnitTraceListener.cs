using System.Diagnostics;

namespace Finder.MsBuild.Task.Tests;

/// <summary>
/// A trace listener that forwards trace messages to NUnit's TestContext.
/// </summary>
public class NUnitTraceListener : TraceListener
{
    private readonly bool _includeCategory;
    private readonly string _messagePrefix;
    
    /// <summary>
    /// Creates a new trace listener that forwards to NUnit's TestContext
    /// </summary>
    /// <param name="includeCategory">Whether to include the source/category in output</param>
    /// <param name="messagePrefix">Optional prefix for all messages</param>
    public NUnitTraceListener(bool includeCategory = false, string messagePrefix = "")
    {
        _includeCategory = includeCategory;
        _messagePrefix = string.IsNullOrEmpty(messagePrefix) ? "" : $"{messagePrefix}: ";
    }
    
    public override void Write(string? message)
    {
        if (message != null)
            TestContext.Write($"{_messagePrefix}{message}");
    }

    public override void WriteLine(string? message)
    {
        if (message != null)
            TestContext.WriteLine($"{_messagePrefix}{message}");
    }
    
    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        if (message == null) return;
        
        var prefix = _includeCategory ? $"[{source}] " : "";
        var typePrefix = eventType != TraceEventType.Information ? $"[{eventType}] " : "";
        
        TestContext.WriteLine($"{_messagePrefix}{prefix}{typePrefix}{message}");
    }
}
