using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using FluentAssertions;

namespace Finder.MsBuild.Task.Tests;

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