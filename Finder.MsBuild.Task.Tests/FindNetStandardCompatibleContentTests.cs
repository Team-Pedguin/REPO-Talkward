using System;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Moq;

namespace Finder.MsBuild.Task.Tests
{
    public class FindNetStandardCompatibleContentTests
    {
        private string _testDirectory;
        private Mock<IBuildEngine> _mockBuildEngine;

        [SetUp]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_testDirectory);
            
            _mockBuildEngine = new Mock<IBuildEngine>();
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
            var task = new FindNetStandardCompatibleContent
            {
                NuGetPackageRoot = _testDirectory,
                MaximumNetStandard = "2.1",
                Packages = [],
                BuildEngine = _mockBuildEngine.Object
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue("Task should succeed with no packages");
            task.LibraryContentFiles.Should().BeEmpty();
        }

        [Test]
        public void Execute_WithInvalidNetStandardVersion_ThrowsArgumentException()
        {
            // Arrange
            var task = new FindNetStandardCompatibleContent
            {
                NuGetPackageRoot = _testDirectory,
                MaximumNetStandard = "9.9", // Invalid version
                Packages = [new TaskItem("TestPackage")],
                BuildEngine = _mockBuildEngine.Object
            };

            // Act & Assert
            var a = () => task.Execute();
            a.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Execute_FindsCorrectLibraryFiles()
        {
            // Arrange
            const string packageName = "TestPackage";
            const string packageVersion = "1.0.0";
            var packageDir = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
            var netStandardDir = Path.Combine(packageDir, "lib", "netstandard2.0");
            Directory.CreateDirectory(netStandardDir);

            // Create test DLL
            var dllPath = Path.Combine(netStandardDir, "TestPackage.dll");
            File.WriteAllText(dllPath, "test");

            // Create fallback marker file that should be ignored
            var fallbackPath = Path.Combine(netStandardDir, "_._");
            File.WriteAllText(fallbackPath, "ignored");

            var taskItem = new TaskItem(packageName);
            taskItem.SetMetadata("Version", packageVersion);

            var task = new FindNetStandardCompatibleContent
            {
                NuGetPackageRoot = _testDirectory,
                MaximumNetStandard = "2.1",
                Packages = [taskItem],
                BuildEngine = _mockBuildEngine.Object
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
        public void Execute_SelectsHighestCompatibleFramework()
        {
            // Arrange - Create package with multiple framework versions
            const string packageName = "MultiFrameworkPackage";
            const string packageVersion = "1.0.0";
            var packageDir = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
            
            // Create both 2.0 and 1.6 framework directories
            var ns20Dir = Path.Combine(packageDir, "lib", "netstandard2.0");
            var ns16Dir = Path.Combine(packageDir, "lib", "netstandard1.6");
            Directory.CreateDirectory(ns20Dir);
            Directory.CreateDirectory(ns16Dir);

            var dll20 = Path.Combine(ns20Dir, "Package.dll");
            var dll16 = Path.Combine(ns16Dir, "Package.dll");
            File.WriteAllText(dll20, "ns20");
            File.WriteAllText(dll16, "ns16");

            var taskItem = new TaskItem(packageName);
            taskItem.SetMetadata("Version", packageVersion);

            var task = new FindNetStandardCompatibleContent
            {
                NuGetPackageRoot = _testDirectory,
                MaximumNetStandard = "2.1",
                Packages = [taskItem],
                BuildEngine = _mockBuildEngine.Object
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.LibraryContentFiles.Should().HaveCount(1);
            task.LibraryContentFiles[0].ItemSpec.Should().Be(dll20);
            task.LibraryContentFiles[0].GetMetadata("TargetFramework").Should().Be("netstandard2.0");
        }
        
        
        [Test]
        public void Execute_RealPackageWithNoContent_ReturnsTrue()
        {
            // Arrange
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var nugetPackageRoot = Path.Combine(homeDir, ".nuget");
            var item = new TaskItem("Microsoft.Build.Framework");
            item.SetMetadata("Version", "17.11.4");
            var task = new FindNetStandardCompatibleContent
            {
                NuGetPackageRoot = nugetPackageRoot,
                MaximumNetStandard = "2.1",
                Packages = [item],
                BuildEngine = _mockBuildEngine.Object
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue("Task should succeed but find no .NET Standard content");
            task.LibraryContentFiles.Should().BeEmpty("No .NET Standard content should be found");
        }        
        [Test]
        public void Execute_RealPackageWithContent_ReturnsTrue()
        {
            // Arrange
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var nugetPackageRoot = Path.Combine(homeDir, ".nuget");
            var item = new TaskItem("Microsoft.Bcl.AsyncInterfaces");
            item.SetMetadata("Version", "9.0.3");
            var task = new FindNetStandardCompatibleContent
            {
                NuGetPackageRoot = nugetPackageRoot,
                MaximumNetStandard = "2.1",
                Packages = [item],
                BuildEngine = _mockBuildEngine.Object
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue("Task should succeed");
            task.LibraryContentFiles.Should().NotBeEmpty("Some .NET Standard content should be found");
            
            // log the contents
            foreach (var libraryContentFile in task.LibraryContentFiles)
            {
                TestContext.WriteLine($"Found: {libraryContentFile.ItemSpec}");
                TestContext.WriteLine($"Package: {libraryContentFile.GetMetadata("Package")}");
                TestContext.WriteLine($"TargetFramework: {libraryContentFile.GetMetadata("TargetFramework")}");
            }
        }
    }
}