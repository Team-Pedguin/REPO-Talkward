using System.Diagnostics;
using Microsoft.Build.Framework;

namespace Finder.MsBuild.Task.Tests;

/// <summary>
/// This class implements the default logger that outputs event data
/// to tracing.
/// </summary>
/// <remarks>This class is not thread safe.</remarks>
public class MsBuildTraceLogger : INodeLogger
{
    private IEventSource? _eventSource;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public MsBuildTraceLogger()
    {
        // do nothing
    }

    /// <summary>
    /// Initializes the logger.
    /// </summary>
    public virtual void Initialize(IEventSource eventSource, int nodeCount)
    {
        Initialize(eventSource);
    }

    public void Initialize(IEventSource eventSource)
    {
        _eventSource = eventSource;
        eventSource.AnyEventRaised += AnyEventHandler;
    }

    /// <summary>
    /// The logger does not need to release any resources.
    /// This method does nothing.
    /// </summary>
    public virtual void Shutdown()
    {
        if (_eventSource == null) return;
        _eventSource.AnyEventRaised -= AnyEventHandler;
    }

    public LoggerVerbosity Verbosity { get; set; }
    public string? Parameters { get; set; }

    /// <summary>
    /// Prints any event
    /// </summary>
    public static void AnyEventHandler(object sender, BuildEventArgs buildEvent)
    {
        switch (buildEvent)
        {
            case BuildStartedEventArgs e:
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: {e.Message ?? "Build started"}");
                break;
            
            case BuildFinishedEventArgs e:
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: {e.Message ?? "Build finished"}");
                break;
            
            case ProjectStartedEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: {project} started, {e.Message}");
                break;
            }
            
            case ProjectFinishedEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: {project} finished, {e.Message}");
                break;
            }
            
            case TargetStartedEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);
                var target = e.TargetName;
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: {project} target {target} started, {e.Message}");
                break;
            }
            
            case TargetFinishedEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);
                var target1 = e.TargetName;
                Trace.TraceInformation(
                    $"{e.SenderName ?? "MSBuild"}: {project} target {target1} finished, {e.Message}");
                break;
            }
            
            case TaskStartedEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);
                var taskName = e.TaskName;
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: {project} task {taskName} started, {e.Message}");
                break;
            }
            
            case TaskFinishedEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);
                var taskName1 = e.TaskName;
                Trace.TraceInformation(
                    $"{e.SenderName ?? "MSBuild"}: {project} task {taskName1} finished, {e.Message}");
                break;
            }
            
            case BuildErrorEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);

                Trace.TraceError(
                    $"{e.SenderName ?? "MSBuild"}: {project} error @ {e.File}:{e.LineNumber}:{e.ColumnNumber} {e.Code}: {e.Message}");
                break;
            }
            
            case BuildWarningEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);

                Trace.TraceWarning(
                    $"{e.SenderName ?? "MSBuild"}: {project} warning @ {e.File}:{e.LineNumber}:{e.ColumnNumber} {e.Code}: {e.Message}");
                break;
            }
            
            case BuildMessageEventArgs e:
            {
                var project = Path.GetFileNameWithoutExtension(e.ProjectFile);
                var messageType = e.Importance switch
                {
                    MessageImportance.High => " important",
                    MessageImportance.Normal => "",
                    MessageImportance.Low => " minor note",
                    _ => $" level {e.Importance} note"
                };
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: {project}{messageType}: {e.Message}");
                break;
            }

            case CustomBuildEventArgs e:
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: custom: {e.Message}");
                break;

            case BuildStatusEventArgs e:
                Trace.TraceInformation($"{e.SenderName ?? "MSBuild"}: status: {e.Message}");
                break;

            default:
                Trace.TraceInformation(
                    $"{buildEvent.SenderName ?? "MSBuild"}: {buildEvent.GetType().Name}: {buildEvent.Message}");
                break;
        }
    }
}