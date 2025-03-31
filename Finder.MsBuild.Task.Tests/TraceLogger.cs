using BenchmarkDotNet.Loggers;

namespace Finder.MsBuild.Task.Tests;

public class TraceLogger : ILogger
{

    public string Id { get; }
    public int Priority { get; }
    
    public TraceLogger(string id, int priority)
    {
        Id = id;
        Priority = priority;
    }
    public void Write(LogKind logKind, string text)
    {
        System.Diagnostics.Trace.Write(text);
    }

    public void WriteLine()
        => System.Diagnostics.Trace.WriteLine(string.Empty);

    public void WriteLine(LogKind logKind, string text)
    {
        switch (logKind)
        {
            case LogKind.Error:
                System.Diagnostics.Trace.TraceError(text);
                break;
            case LogKind.Warning:
                System.Diagnostics.Trace.TraceWarning(text);
                break;
            case LogKind.Info:
                System.Diagnostics.Trace.TraceInformation(text);
                break;
            case LogKind.Help:
            case LogKind.Default:
            case LogKind.Header:
            case LogKind.Statistic:
            case LogKind.Result:
            case LogKind.Hint:
            default:
                System.Diagnostics.Trace.WriteLine(text);
                break;
        }
    }

    public void Flush()
        => System.Diagnostics.Trace.Flush();
}