using System;
using System.Diagnostics;
using NuGet.Common;
using Task = System.Threading.Tasks.Task;

namespace Finder.MsBuild.Task
{
    /// <summary>
    /// NuGet logger implementation that forwards log messages to System.Diagnostics.Trace,
    /// which can be captured by NUnit's TestContext when using NUnitTraceListener.
    /// </summary>
    public class NuGetTraceLogger : LoggerBase
    {
        private readonly LogLevel _minimumLevel;
        private readonly string _logPrefix;
        private readonly bool _includeTimestamps;
        private readonly bool _includeThreadId;

        /// <summary>
        /// Creates a new NuGet logger that writes to System.Diagnostics.Trace
        /// </summary>
        /// <param name="minimumLevel">Minimum log level to record</param>
        /// <param name="logPrefix">Optional prefix for all log messages (e.g., "NuGet")</param>
        /// <param name="includeTimestamps">Whether to include timestamps in log messages</param>
        /// <param name="includeThreadId">Whether to include thread IDs in log messages</param>
        public NuGetTraceLogger(
            LogLevel minimumLevel = LogLevel.Information,
            string logPrefix = "NuGet")
        {
            _minimumLevel = minimumLevel;
            _logPrefix = logPrefix;
        }

        public override void Log(ILogMessage message)
        {
            if ((int)message.Level < (int)_minimumLevel)
                return;

            switch (message.Level)
            {
                case LogLevel.Information:
                    Trace.TraceInformation($"{_logPrefix}: {message.Message}");
                    break;
                case LogLevel.Warning:
                    Trace.TraceWarning($"{_logPrefix}: {message.Message}");
                    break;
                case LogLevel.Error:
                    Trace.TraceError($"{_logPrefix}: {message.Message}");
                    break;
                
                case LogLevel.Debug:
                case LogLevel.Verbose:
                case LogLevel.Minimal:
                default:
                    Trace.WriteLine($"{_logPrefix}: {message.Message}");
                    break;
            }
        }

        public override System.Threading.Tasks.Task LogAsync(ILogMessage message)
        {
            Log(message);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private static string GetLogLevelPrefix(LogLevel level) => level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Verbose => "VERBOSE",
            LogLevel.Information => "INFO",
            LogLevel.Minimal => "MINIMAL",
            LogLevel.Warning => "WARNING",
            LogLevel.Error => "ERROR",
            _ => level.ToString()
        };
    }
}
