using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Utilities;
using Moq;
using NuGet.Common;
using ILogger = Microsoft.Build.Framework.ILogger;

namespace Finder.MsBuild.Task.Tests;

public class FindNetStandardCompatibleContentViaNuGetMsBuildTests
{
    private string _testDirectory;
    private NuGetTraceLogger _nuGetLogger;
    private string _nugetPackageRoot;
    private string _taskAssemblyPath;
    private INodeLogger _msbuildLogger;

    [SetUp]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testDirectory);

        // Create a subdirectory that will serve as our NuGet package root
        _nugetPackageRoot = Path.Combine(_testDirectory, "packages");
        Directory.CreateDirectory(_nugetPackageRoot);

        // Initialize the NuGet logger
        _nuGetLogger = new NuGetTraceLogger(LogLevel.Debug);
        
        // Initialize the MSBuild logger
        _msbuildLogger = new MsBuildTraceLogger();

        // Get the actual path to the task assembly
        _taskAssemblyPath = Assembly.GetAssembly(typeof(FindNetStandardCompatibleContentViaNuGet))!.Location;
        TestContext.WriteLine($"Task assembly path: {_taskAssemblyPath}");

        TestContext.WriteLine($"Test directory: {_testDirectory}");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            // Clean up the temporary project collection used for MSBuild tests
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();

            // Clean up temporary files
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }

    [Test]
    public void Execute_InProcess_WithMsBuild_FindsPackageContent()
    {
        // Arrange - Create test package
        const string packageName = "TestPackage";
        const string packageVersion = "1.0.0";

        // Create a package with NetStandard 2.0 content
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);

        // Create a temporary project file - using non-SDK style to avoid SDK resolution issues
        var projectPath = Path.Combine(_testDirectory, "TestProject.csproj");
        var projectContent =
            $"""
             <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
               <PropertyGroup>
                 <TargetFramework>netstandard2.0</TargetFramework>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="{packageName}" Version="{packageVersion}" />
               </ItemGroup>
               <UsingTask AssemblyFile="{_taskAssemblyPath.Replace("\\", @"\\")}" 
                          TaskName="Finder.MsBuild.Task.FindNetStandardCompatibleContentViaNuGet" />
               <Target Name="FindContent" Returns="@(FoundContent)">
                 <FindNetStandardCompatibleContentViaNuGet
                     NuGetPackageRoot="{_nugetPackageRoot}"
                     MaximumNetStandard="2.1"
                     Packages="@(PackageReference)">
                   <Output TaskParameter="LibraryContentFiles" ItemName="FoundContent" />
                 </FindNetStandardCompatibleContentViaNuGet>
                 <PropertyGroup>
                   <FoundContentCount>@(FoundContent->Count())</FoundContentCount>
                 </PropertyGroup>
                 <Message Text="Found @(FoundContent->Count()) content files" Importance="high" />
                 <Message Text="First found file: %(FoundContent.Identity)" Condition="'@(FoundContent)'!=''" Importance="high" />
               </Target>
             </Project>
             """;
        File.WriteAllText(projectPath, projectContent);

        // Load the project in MSBuild
        var projectCollection = new ProjectCollection();
        var project = projectCollection.LoadProject(projectPath);

        // Create a logger to capture output
        var logger = new ConsoleLogger(LoggerVerbosity.Detailed);

        // Configure a build request for the FindContent target
        var buildParameters = new BuildParameters(projectCollection)
        {
            Loggers = [logger],
            DetailedSummary = true
        };

        // Create RequestedProjectState to properly filter for output items
        var requestedProjectState = new RequestedProjectState
        {
            ItemFilters = new Dictionary<string, List<string>?>
            {
                {"FoundContent", null}, // Capture all metadata
            }
        };

        var buildRequest = new BuildRequestData(
            projectInstance: project.CreateProjectInstance(),
            targetsToBuild: ["FindContent"],
            hostServices: null,
            flags: BuildRequestDataFlags.None,
            propertiesToTransfer: null, // No properties to transfer
            requestedProjectState: requestedProjectState);

        // Act - Execute the build
        var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);

        // Assert - Check if the target executed successfully
        buildResult.ResultsByTarget.Should().ContainKey("FindContent", "The FindContent target should have executed");
        var targetResult = buildResult.ResultsByTarget["FindContent"];
        
        // Check if our task within the target executed correctly
        targetResult.Should().NotBeNull();

        // Check if items were successfully returned
        // Get items directly from build output instead of target result
        var outputItems = buildResult.ResultsByTarget["FindContent"].Items;
        outputItems.Should().NotBeEmpty("The task should find content files");

        // Check specific expected file
        var expectedDllPath = Path.Combine(_nugetPackageRoot, packageName.ToLowerInvariant(), packageVersion, "lib",
            "netstandard2.0", $"{packageName}.dll");

        outputItems.Should().Contain(item =>
            string.Equals(item.ItemSpec, expectedDllPath, StringComparison.OrdinalIgnoreCase));

        // Check metadata
        var packageItems = outputItems.Where(item =>
            string.Equals(item.GetMetadata("Package"), packageName, StringComparison.OrdinalIgnoreCase)).ToList();

        packageItems.Should().NotBeEmpty("Should have items with Package metadata");
        packageItems.Should().OnlyContain(item => 
            item.GetMetadata("TargetFramework") == "netstandard2.0", 
            "All items should have correct TargetFramework metadata");
    }

    private void CreateTestPackageStructure(string packageName, string packageVersion, string framework, bool createDll)
    {
        var packageDir = Path.Combine(_nugetPackageRoot, packageName.ToLowerInvariant(), packageVersion);
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

        // Create a minimal nuspec file
        var nuspecPath = Path.Combine(packageDir, $"{packageName}.nuspec");
        var nuspecContent =
            $"""
             <?xml version="1.0"?>
             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
               <metadata>
                 <id>{packageName}</id>
                 <version>{packageVersion}</version>
                 <authors>Test</authors>
                 <description>Test package</description>
               </metadata>
             </package>
             """;
        File.WriteAllText(nuspecPath, nuspecContent);
    }

    [Test]
    public void Execute_InProcess_WithMsBuild_HandlesPrivateAssets()
    {
        // Arrange - Create test package
        const string packageName = "PrivateAssetsPackage";
        const string packageVersion = "1.0.0";

        // Create a package with compile and build assets
        var packageDir = Path.Combine(_nugetPackageRoot, packageName.ToLowerInvariant(), packageVersion);

        // Create compile assets
        var libDir = Path.Combine(packageDir, "lib", "netstandard2.0");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, $"{packageName}.dll"), "test dll");

        // Create build assets (private by default)
        var buildDir = Path.Combine(packageDir, "build");
        Directory.CreateDirectory(buildDir);
        File.WriteAllText(Path.Combine(buildDir, $"{packageName}.props"), "test props");

        // Create a minimal nuspec file
        var nuspecPath = Path.Combine(packageDir, $"{packageName}.nuspec");
        var nuspecContent =
            $"""
             <?xml version="1.0"?>
             <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
               <metadata>
                 <id>{packageName}</id>
                 <version>{packageVersion}</version>
                 <authors>Test</authors>
                 <description>Test package</description>
               </metadata>
             </package>
             """;
        File.WriteAllText(nuspecPath, nuspecContent);

        // Create a temporary project file with PrivateAssets - using non-SDK style
        var projectPath = Path.Combine(_testDirectory, "PrivateAssetsProject.csproj");
        var projectContent =
            $"""
             <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
               <PropertyGroup>
                 <TargetFramework>netstandard2.0</TargetFramework>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="{packageName}" Version="{packageVersion}" PrivateAssets="all" />
               </ItemGroup>
               <UsingTask AssemblyFile="{_taskAssemblyPath.Replace("\\", @"\\")}" 
                          TaskName="Finder.MsBuild.Task.FindNetStandardCompatibleContentViaNuGet" />
               <Target Name="FindContent" Returns="@(FoundContent)">
                 <FindNetStandardCompatibleContentViaNuGet
                     NuGetPackageRoot="{_nugetPackageRoot}"
                     MaximumNetStandard="2.1"
                     Packages="@(PackageReference)">
                   <Output TaskParameter="LibraryContentFiles" ItemName="FoundContent" />
                 </FindNetStandardCompatibleContentViaNuGet>
                 <PropertyGroup>
                   <FoundContentCount>@(FoundContent->Count())</FoundContentCount>
                 </PropertyGroup>
                 <Message Text="Found @(FoundContent->Count()) content files" Importance="high" />
                 <Message Text="Types: @(FoundContent->'%(AssetType)')" Importance="high" />
               </Target>
             </Project>
             """;
        File.WriteAllText(projectPath, projectContent);

        // Load the project in MSBuild
        var projectCollection = new ProjectCollection();
        var project = projectCollection.LoadProject(projectPath);

        // Create a logger to capture output
        var logger = new ConsoleLogger(LoggerVerbosity.Detailed);

        // Configure a build request for the FindContent target
        var buildParameters = new BuildParameters(projectCollection)
        {
            Loggers = [logger],
            DetailedSummary = true
        };

        // Create RequestedProjectState to properly filter for output items
        var requestedProjectState = new RequestedProjectState
        {
            ItemFilters = new Dictionary<string, List<string>?>
            {
                {"FoundContent", null}, // Capture all metadata
            },
            PropertyFilters = new List<string> { "FoundContentCount" } // Also capture the count property
        };

        var buildRequest = new BuildRequestData(
            projectInstance: project.CreateProjectInstance(),
            targetsToBuild: ["FindContent"],
            hostServices: null,
            flags: BuildRequestDataFlags.None,
            propertiesToTransfer: null, // No properties to transfer (they're captured via RequestedProjectState)
            requestedProjectState: requestedProjectState);

        // Act - Execute the build
        var buildResult = BuildManager.DefaultBuildManager.Build(buildParameters, buildRequest);

        // Assert - Check the target results directly instead of overall build success
        buildResult.ResultsByTarget.Should().ContainKey("FindContent", "The FindContent target should have executed");
        var targetResult = buildResult.ResultsByTarget["FindContent"];
        
        targetResult.Should().NotBeNull();
        targetResult.Items.Should().NotBeEmpty("The task should find content files");

        // Both compile and build assets should be included (because Private=true)
        var dllItem = targetResult.Items.FirstOrDefault(item => item.ItemSpec.EndsWith(".dll"));
        dllItem.Should().NotBeNull("Should find the DLL file");
        dllItem.GetMetadata("IsPrivate").Should().Be("True", "Compile assets should be marked as private");

        var propsItem = targetResult.Items.FirstOrDefault(item => item.ItemSpec.EndsWith(".props"));
        propsItem.Should().NotBeNull("Should find the props file");
        propsItem.GetMetadata("IsPrivate").Should().Be("True", "Build assets should be marked as private");
    }
}