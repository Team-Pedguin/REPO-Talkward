using System.Diagnostics;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Finder.MsBuild.Task.Tests;

public partial class FindNetStandardCompatibleContentViaNuGetTests
{
    [Test]
    public void Execute_WithInvalidNetStandardVersion_ThrowsArgumentException()
    {
        // Arrange
        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "9.9", // Invalid version
            Packages = [new TaskItem("TestPackage")],
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act & Assert
        var a = () => task.Execute();
        a.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Execute_FindsCorrectLibraryFiles()
    {
        // Arrange
        var packageName = "TestPackage";
        var packageVersion = "1.0.0";
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
        var dllPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard2.0", $"{packageName}.dll");
        task.LibraryContentFiles[0].ItemSpec.Should().Be(dllPath);
        task.LibraryContentFiles[0].GetMetadata("Package").Should().Be(packageName);
        task.LibraryContentFiles[0].GetMetadata("TargetFramework").Should().Be("netstandard2.0");
    }

    [Test]
    public void Execute_SelectsHighestCompatibleFramework()
    {
        // Arrange
        var packageName = "MultiFrameworkPackage";
        var packageVersion = "1.0.0";

        // Create package with multiple framework versions
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        CreateTestPackageStructure(packageName, packageVersion, "netstandard1.6", true);
        CreateTestPackageStructure(packageName, packageVersion, "netstandard1.0", true);

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
        var expectedDllPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard2.0", $"{packageName}.dll");
        task.LibraryContentFiles[0].ItemSpec.Should().Be(expectedDllPath);
        task.LibraryContentFiles[0].GetMetadata("TargetFramework").Should().Be("netstandard2.0");
    }

    [Test]
    public void Execute_MaximumNetStandardLimitsSelection()
    {
        // Arrange
        var packageName = "MaximumLimitedPackage";
        var packageVersion = "1.0.0";

        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.1", true);
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        CreateTestPackageStructure(packageName, packageVersion, "netstandard1.6", true);

        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);

        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.0", // Only 2.0 and below are compatible
            Packages = [taskItem],
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCount(1);
        var expectedDllPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard2.0", $"{packageName}.dll");
        task.LibraryContentFiles[0].ItemSpec.Should().Be(expectedDllPath);
        task.LibraryContentFiles[0].GetMetadata("TargetFramework").Should().Be("netstandard2.0");
    }

    [Test]
    public void Execute_IgnoresFallbackMarkers()
    {
        // Arrange
        var packageName = "FallbackPackage";
        var packageVersion = "1.0.0";

        // Create package with fallback marker
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", false);
        // Add a real implementation in a lower framework
        CreateTestPackageStructure(packageName, packageVersion, "netstandard1.6", true);

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
        var expectedDllPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard1.6", $"{packageName}.dll");
        task.LibraryContentFiles[0].ItemSpec.Should().Be(expectedDllPath);
        task.LibraryContentFiles[0].GetMetadata("TargetFramework").Should().Be("netstandard1.6");
    }

    [Test]
    public void Execute_WithVersionRange_SelectsHighestVersion()
    {
        // Arrange
        var packageName = "VersionRangePackage";

        // Create multiple versions
        CreateTestPackageStructure(packageName, "1.0.0", "netstandard2.0", true);
        CreateTestPackageStructure(packageName, "1.1.0", "netstandard2.0", true);
        CreateTestPackageStructure(packageName, "2.0.0", "netstandard2.0", true);

        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", "[1.0.0,2.0.0)"); // Range: >= 1.0.0 and < 2.0.0

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
        var expectedDllPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), "1.1.0", "lib",
            "netstandard2.0", $"{packageName}.dll");
        task.LibraryContentFiles[0].ItemSpec.Should().Be(expectedDllPath);
    }

    [Test]
    public void Execute_WithMultiplePackages_FindsAllContent()
    {
        // Arrange
        var package1 = "Package1";
        var package2 = "Package2";

        CreateTestPackageStructure(package1, "1.0.0", "netstandard2.0", true);
        CreateTestPackageStructure(package2, "2.0.0", "netstandard1.6", true);

        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = new[]
            {
                CreateTaskItem(package1, "1.0.0"),
                CreateTaskItem(package2, "2.0.0")
            },
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCount(2);

        var expectedDllPath1 = Path.Combine(_testDirectory, package1.ToLowerInvariant(), "1.0.0", "lib",
            "netstandard2.0", $"{package1}.dll");
        var expectedDllPath2 = Path.Combine(_testDirectory, package2.ToLowerInvariant(), "2.0.0", "lib",
            "netstandard1.6", $"{package2}.dll");

        task.LibraryContentFiles.Any(item => item.ItemSpec == expectedDllPath1).Should().BeTrue();
        task.LibraryContentFiles.Any(item => item.ItemSpec == expectedDllPath2).Should().BeTrue();
    }

    [Test]
    public void Execute_NoCompatibleFramework_ReturnsNoContent()
    {
        // Arrange
        var packageName = "NetCoreOnlyPackage";
        var packageVersion = "1.0.0";

        // Create a package with only netcoreapp content, no netstandard
        var packageDir = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        var libDir = Path.Combine(packageDir, "lib", "netcoreapp3.1");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, $"{packageName}.dll"), "test content");

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
        result.Should().BeTrue("Task should succeed even with no compatible content");
        task.LibraryContentFiles.Should().BeEmpty("No netstandard content should be found");
    }

    [Test]
    public void Execute_WithGeneratePathProperty_UsesCorrectPath()
    {
        // Arrange
        var packageName = "PathPropertyPackage";
        var packageVersion = "1.0.0";

        // Create package in a standard location
        var standardPackagePath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);

        // Create a custom location simulating where NuGet might put it when using GeneratePathProperty
        var customPackagePath = Path.Combine(_testDirectory, "custom", "location", packageName);
        Directory.CreateDirectory(customPackagePath);

        // Create content in the custom location
        var libPath = Path.Combine(customPackagePath, "lib", "netstandard2.0");
        Directory.CreateDirectory(libPath);
        var dllPath = Path.Combine(libPath, $"{packageName}.dll");
        File.WriteAllText(dllPath, "custom path content");

        // Mock BuildEngine6 to return our custom path property with correct casing
        var mockBuildEngine6 = new Mock<IBuildEngine6>();
        var properties = new Dictionary<string, string>
        {
            {"PkgPathPropertyPackage", customPackagePath}
        };
        mockBuildEngine6.Setup(e => e.GetGlobalProperties()).Returns(properties);

        // Set up the build engine with IBuildEngine6 support
        var mockBuildEngine = new Mock<IBuildEngine>();
        mockBuildEngine.As<IBuildEngine6>().Setup(e => e.GetGlobalProperties()).Returns(properties);

        // Set up the task item with GeneratePathProperty=true
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("GeneratePathProperty", "true");

        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            BuildEngine = mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCount(1);
        task.LibraryContentFiles[0].ItemSpec.Should().Be(dllPath);
        task.LibraryContentFiles[0].GetMetadata("Package").Should().Be(packageName);
        task.LibraryContentFiles[0].GetMetadata("TargetFramework").Should().Be("netstandard2.0");
    }

    [Test]
    public void Execute_WithGeneratePathPropertyButNoProperty_FallsBackToStandardResolution()
    {
        // Arrange
        var packageName = "MissingPathPropertyPackage";
        var packageVersion = "1.0.0";

        // Create package in the standard location
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);

        // Mock BuildEngine6 to return empty properties dictionary
        var mockBuildEngine6 = new Mock<IBuildEngine6>();
        mockBuildEngine6.Setup(e => e.GetGlobalProperties()).Returns(new Dictionary<string, string>());

        // Set up the build engine with IBuildEngine6 support
        var mockBuildEngine = new Mock<IBuildEngine>();
        mockBuildEngine.As<IBuildEngine6>().Setup(e => e.GetGlobalProperties())
            .Returns(new Dictionary<string, string>());

        // Set up warning collector to verify warning was logged
        var loggedWarnings = new List<string>();
        mockBuildEngine.Setup(e => e.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
            .Callback<BuildWarningEventArgs>(args => loggedWarnings.Add(args.Message));

        // Set up the task item with GeneratePathProperty=true
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("GeneratePathProperty", "true");

        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            BuildEngine = mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCount(1);

        // Verify content was found using standard resolution
        var expectedDllPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard2.0", $"{packageName}.dll");
        task.LibraryContentFiles[0].ItemSpec.Should().Be(expectedDllPath);

        // Verify warning was logged about missing property with correct casing
        loggedWarnings.Should().Contain(w => w.Contains("GeneratePathProperty is true") &&
                                             w.Contains("property PkgMissingPathPropertyPackage was not found"));
    }

    [Test]
    public void Execute_WithFullDependencyGraph_ResolvesDependencies()
    {
        // Arrange
        var packageName = "MainPackage";
        var dependencyPackage = "DependencyPackage";
        var packageVersion = "1.0.0";

        // Create main package with content
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);

        // Create dependency package with content
        CreateTestPackageStructure(dependencyPackage, packageVersion, "netstandard2.0", true);

        // Create nuspec file for MainPackage to define dependencies
        var nuspecDir = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        Directory.CreateDirectory(nuspecDir);
        var nuspecContent =
            $"""
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{packageName}</id>
                <version>{packageVersion}</version>
                <authors>Test</authors>
                <dependencies>
                  <group targetFramework=".NETStandard2.0">
                    <dependency id="{dependencyPackage}" version="{packageVersion}" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;
        File.WriteAllText(Path.Combine(nuspecDir, $"{packageName}.nuspec"), nuspecContent);

        // Create the repository folder structure for dependency resolution
        SetupTestNuGetRepositoryStructure(packageName, dependencyPackage, packageVersion);

        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);

        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            FullDependencyGraph = true,
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCountGreaterThanOrEqualTo(2);

        // Should find content from both main package and dependency
        var mainPackagePath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard2.0", $"{packageName}.dll");
        var dependencyPackagePath = Path.Combine(_testDirectory, dependencyPackage.ToLowerInvariant(), packageVersion,
            "lib",
            "netstandard2.0", $"{dependencyPackage}.dll");

        task.LibraryContentFiles.Any(item => item.ItemSpec == mainPackagePath).Should().BeTrue();
        task.LibraryContentFiles.Any(item => item.ItemSpec == dependencyPackagePath).Should().BeTrue();
    }

    [Test]
    public void Execute_WithFullDependencyGraphDisabled_SkipsDependencies()
    {
        // Arrange
        var packageName = "MainPackage";
        var dependencyPackage = "DependencyPackage";
        var packageVersion = "1.0.0";

        // Create main package with content
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);

        // Create dependency package with content
        CreateTestPackageStructure(dependencyPackage, packageVersion, "netstandard2.0", true);

        // Create nuspec file for MainPackage to define dependencies
        var nuspecDir = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        Directory.CreateDirectory(nuspecDir);
        var nuspecContent =
            $"""
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{packageName}</id>
                <version>{packageVersion}</version>
                <authors>Test</authors>
                <dependencies>
                  <group targetFramework=".NETStandard2.0">
                    <dependency id="{dependencyPackage}" version="{packageVersion}" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;
        File.WriteAllText(Path.Combine(nuspecDir, $"{packageName}.nuspec"), nuspecContent);

        // Setup repository structure
        SetupTestNuGetRepositoryStructure(packageName, dependencyPackage, packageVersion);

        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);

        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            FullDependencyGraph = false, // Explicitly disable dependency resolution
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCount(1);

        // Should only find content from main package
        var mainPackagePath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard2.0", $"{packageName}.dll");
        var dependencyPackagePath = Path.Combine(_testDirectory, dependencyPackage.ToLowerInvariant(), packageVersion,
            "lib",
            "netstandard2.0", $"{dependencyPackage}.dll");

        task.LibraryContentFiles.Any(item => item.ItemSpec == mainPackagePath).Should().BeTrue();
        task.LibraryContentFiles.Any(item => item.ItemSpec == dependencyPackagePath).Should().BeFalse();
    }

    [Test]
    public void Execute_WithNuGetLogger_LogsInformation()
    {
        // Arrange - Create a simple package with known structure
        var packageName = "LoggingTestPackage";
        var packageVersion = "1.0.0";
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);

        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);

        var capturedLogMessages = new List<string>();
        var traceListener = new TestTraceListener(capturedLogMessages);
        Trace.Listeners.Add(traceListener);

        try
        {
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
            capturedLogMessages.Should().Contain(msg => msg.Contains("Starting package processing"));
            capturedLogMessages.Should().Contain(msg => msg.Contains(packageName));
            capturedLogMessages.Should().Contain(msg => msg.Contains("Maximum NetStandard version"));
        }
        finally
        {
            Trace.Listeners.Remove(traceListener);
        }
    }

    private void SetupTestNuGetRepositoryStructure(string mainPackage, string dependencyPackage, string version)
    {
        // Create the repository index files to make it look like a real NuGet repository
        var indexPath = Path.Combine(_testDirectory, "index.json");
        File.WriteAllText(indexPath,
            """
            {
                "version": "3.0.0",
                "resources": [
                    {
                        "@id": "DependencyInfo/3.0.0",
                        "@type": "DependencyInfoResource/3.0.0"
                    },
                    {
                        "@id": "PackageMetadata/3.0.0",
                        "@type": "PackageMetadataResource/3.0.0"
                    },
                    {
                        "@id": "FindPackageById/3.0.0",
                        "@type": "FindPackageByIdResource/3.0.0"
                    }
                ]
            }
            """);

        // Set up package spec files for the test repository
        var v3Path = Path.Combine(_testDirectory, "v3-flatcontainer");
        Directory.CreateDirectory(v3Path);

        SetupPackageV3Index(v3Path, mainPackage, version);
        SetupPackageV3Index(v3Path, dependencyPackage, version);

        // Create a more robust nuspec file for the main package with explicit dependencies
        var mainNuspecPath =
            Path.Combine(_testDirectory, mainPackage.ToLowerInvariant(), version, $"{mainPackage}.nuspec");
        var mainNuspecContent =
            $"""
             <?xml version="1.0"?>
             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
               <metadata>
                 <id>{mainPackage}</id>
                 <version>{version}</version>
                 <authors>Test</authors>
                 <dependencies>
                   <group targetFramework=".NETStandard2.0">
                     <dependency id="{dependencyPackage}" version="{version}" />
                   </group>
                 </dependencies>
               </metadata>
             </package>
             """;
        Directory.CreateDirectory(Path.GetDirectoryName(mainNuspecPath)!);
        File.WriteAllText(mainNuspecPath, mainNuspecContent);
    }

    private void SetupPackageV3Index(string v3Path, string packageId, string version)
    {
        var packagePath = Path.Combine(v3Path, packageId.ToLowerInvariant());
        Directory.CreateDirectory(packagePath);

        // Package index file
        var indexContent =
            $$"""
              {
                "versions": ["{{version}}"]
              }
              """;
        File.WriteAllText(Path.Combine(packagePath, "index.json"), indexContent);

        // Package version folder
        var versionPath = Path.Combine(packagePath, version);
        Directory.CreateDirectory(versionPath);

        // Package manifest
        var manifestPath = Path.Combine(versionPath, $"{packageId.ToLowerInvariant()}.nuspec");
        var nuspecFolder = Path.Combine(_testDirectory, packageId.ToLowerInvariant(), version);
        if (File.Exists(Path.Combine(nuspecFolder, $"{packageId}.nuspec")))
        {
            File.Copy(Path.Combine(nuspecFolder, $"{packageId}.nuspec"), manifestPath);
        }
        else
        {
            var manifestContent =
                $"""
                 <?xml version="1.0"?>
                 <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                   <metadata>
                     <id>{packageId}</id>
                     <version>{version}</version>
                     <authors>Test</authors>
                   </metadata>
                 </package>
                 """;
            File.WriteAllText(manifestPath, manifestContent);
        }
    }

    private void CreateTestPackageStructure(string packageName, string packageVersion, string framework, bool createDll)
    {
        var packageDir = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        var frameworkDir = Path.Combine(packageDir, "lib", framework);
        Directory.CreateDirectory(frameworkDir);

        if (createDll)
        {
            // Create test DLL
            var dllPath = Path.Combine(frameworkDir, $"{packageName}.dll");
            File.WriteAllText(dllPath, $"test content for {framework}");
        }
        else
        {
            // Create fallback marker file
            var fallbackPath = Path.Combine(frameworkDir, "_._");
            File.WriteAllText(fallbackPath, "fallback marker");
        }
    }

    private TaskItem CreateTaskItem(string packageName, string version)
    {
        var item = new TaskItem(packageName);
        item.SetMetadata("Version", version);
        return item;
    }
}

// Add a simple trace listener to capture log messages for testing
public class TestTraceListener : TraceListener
{
    private readonly List<string> _capturedMessages;

    public TestTraceListener(List<string> capturedMessages)
    {
        _capturedMessages = capturedMessages;
    }

    public override void Write(string? message)
    {
        if (message != null)
        {
            _capturedMessages.Add(message);
            TestContext.Write(message);
        }
    }

    public override void WriteLine(string? message)
    {
        if (message != null)
        {
            _capturedMessages.Add(message);
            TestContext.WriteLine(message);
        }
    }
}