using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using System.Diagnostics;

namespace Finder.MsBuild.Task.Tests;

public partial  class FindNetStandardCompatibleContentViaNuGetTests
{
    private string _testDirectory;
    private Mock<IBuildEngine> _mockBuildEngine;
    private TaskLoggingHelper _logger;
    private NuGetTraceLogger _nuGetLogger;

    [SetUp]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDirectory);

        _mockBuildEngine = new Mock<IBuildEngine>();
        _logger = new TaskLoggingHelper(_mockBuildEngine.Object, "FindNetStandardCompatibleContentViaNuGet");
        _mockBuildEngine
            .Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(e =>
                Trace.TraceError($"MSBuild: {e.Message}"));
        _mockBuildEngine
            .Setup(x => x.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
            .Callback<BuildWarningEventArgs>(e =>
                Trace.TraceWarning($"MSBuild: {e.Message}"));
        _mockBuildEngine
            .Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(e =>
                Trace.TraceInformation($"MSBuild: {e.Message}"));
        _mockBuildEngine
            .Setup(x => x.LogCustomEvent(It.IsAny<CustomBuildEventArgs>()))
            .Callback<CustomBuildEventArgs>(e =>
                Trace.TraceInformation($"MSBuild Event: {e.Message}"));

        // Initialize the NuGet logger with better configuration
        _nuGetLogger = new NuGetTraceLogger(
            NuGet.Common.LogLevel.Debug);
        
        TestContext.WriteLine($"Test directory: {_testDirectory}");
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_testDirectory, true);
    }

    [Test]
    public void Execute_WithNoPackages_ReturnsTrue()
    {
        // Arrange
        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [],
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed with no packages");
        task.LibraryContentFiles.Should().BeEmpty();
    }


}
