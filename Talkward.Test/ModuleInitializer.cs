using System.Diagnostics;
using System.Runtime.CompilerServices;
using FluentAssertions;

namespace Talkward.Test;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        License.Accepted = true;

        // add trace listener
        Trace.Listeners.Add(new NUnitTraceListener
        {
            Name = "NUnitTraceListener",
            Filter = new EventTypeFilter(SourceLevels.All),
            TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ThreadId,
        });
        Trace.AutoFlush = true;
        
    }
}