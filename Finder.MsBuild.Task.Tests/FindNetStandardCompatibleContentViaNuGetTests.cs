using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using System.Diagnostics;
using NUnit.Framework.Internal;

namespace Finder.MsBuild.Task.Tests;

public partial class FindNetStandardCompatibleContentViaNuGetTests
{
    private string _testDirectory;
    private Mock<IBuildEngine> _mockBuildEngine;
    private NuGetTraceLogger _nuGetLogger;

    [SetUp]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDirectory);

        _mockBuildEngine = new Mock<IBuildEngine>();
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

        _nuGetLogger = new NuGetTraceLogger(NuGet.Common.LogLevel.Debug);

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

    [Test]
    public void Execute_WithNuGetLogger_LogsInformation()
    {
        // Arrange - Create a simple package with known structure
        const string packageName = "LoggingTestPackage";
        const string packageVersion = "1.0.0";
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);

        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);

        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCount(1);

        // Verify logs contain expected information
        var capturedLogMessages = TestExecutionContext.CurrentContext.CurrentResult.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        capturedLogMessages.Should().Contain(msg => msg.Contains("Starting package processing"));
        capturedLogMessages.Should().Contain(msg => msg.Contains(packageName));
        capturedLogMessages.Should().Contain(msg => msg.Contains("Maximum NetStandard version"));
        capturedLogMessages.Should().Contain(msg => msg.Contains("Total content files found"));

        // Verify operation timing is logged
        capturedLogMessages.Should().Contain(msg => msg.Contains("[Operation]") && msg.EndsWith("started"));
        capturedLogMessages.Should()
            .Contain(msg => msg.Contains("[Operation]") && msg.Contains("completed") && msg.EndsWith("ms"));
    }
}