using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Finder.MsBuild.Task
{
    /// <summary>
    /// Extension methods to enhance logging capabilities and integrate with test frameworks.
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Logs detailed operation timing information
        /// </summary>
        public static IDisposable LogOperation(this TaskLoggingHelper logger, string operationName,
            MessageImportance importance = MessageImportance.Normal)
            => new OperationLogger(logger, operationName, importance);

        /// <summary>
        /// Helper class to log operation start and end with timing information
        /// </summary>
        private class OperationLogger : IDisposable
        {
            private readonly TaskLoggingHelper _logger;
            private readonly string _operationName;
            private readonly MessageImportance _importance;
            private readonly Stopwatch _stopwatch;
            private readonly DateTime _started;

            public OperationLogger(TaskLoggingHelper logger, string operationName, MessageImportance importance)
            {
                _logger = logger;
                _operationName = operationName;
                _importance = importance;
                _stopwatch = Stopwatch.StartNew();
                _started = DateTime.UtcNow;

                logger.LogMessage(_importance, $"{_operationName} Started");
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                var duration = _stopwatch.Elapsed;
                _logger.LogMessage(_importance, $"{_operationName} Completed in {duration.TotalMilliseconds}ms");
                _logger.LogTelemetry(_operationName, new Dictionary<string, string>
                {
                    {"OperationName", _operationName},
                    {"StartTime", _started.ToString("o")},
                    {"EndTime", DateTime.UtcNow.ToString("o")},
                    {"Duration", duration.ToString()},
                    {"Importance", _importance.ToString()}
                });
            }
        }
    }
}