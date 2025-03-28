using System.Diagnostics;

namespace Talkward.Test;

public class NUnitTraceListener : TraceListener
{
    public override void Write(string? message)
    {
        if (message != null)
            TestContext.Write(message);
    }

    public override void WriteLine(string? message)
    {
        if (message != null)
            TestContext.WriteLine(message);
    }
}