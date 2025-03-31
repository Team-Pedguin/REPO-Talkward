using System.Diagnostics;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;

namespace Finder.MsBuild.Task.Tests;

public partial class FindNetStandardCompatibleContentViaNuGetTests
{
    [Test]
    public void Execute_WithIncludeAssets_OnlyIncludesSpecifiedAssets()
    {
        // Arrange
        const string packageName = "AssetFilteringPackage";
        const string packageVersion = "1.0.0";
        
        // Create a package with different asset types
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // Create content files
        var contentPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "contentfiles", "any", "netstandard2.0");
        Directory.CreateDirectory(contentPath);
        File.WriteAllText(Path.Combine(contentPath, "content.txt"), "content file");
        
        // Create build files
        var buildPath = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "build props");

        // Create task item with IncludeAssets limited to compile
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "compile");

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
        // Only compile assets (lib DLLs) should be included
        task.LibraryContentFiles.Should().HaveCount(1);
        task.LibraryContentFiles[0].ItemSpec.Should().EndWith($"{packageName}.dll");
        task.LibraryContentFiles[0].GetMetadata("AssetType").Should().Be("compile");
    }

    [Test]
    public void Execute_WithExcludeAssets_ExcludesSpecifiedAssets()
    {
        // Arrange
        const string packageName = "ExcludeAssetsPackage";
        const string packageVersion = "1.0.0";
        
        // Create a package with different asset types
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // Create task item with ExcludeAssets
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("ExcludeAssets", "compile");

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
        // No files should be included since we excluded compile assets and that's all there is
        task.LibraryContentFiles.Should().BeEmpty();
    }
    
    [Test]
    public void Execute_WithMultipleAssetTypes_HandlesEachCorrectly()
    {
        // Arrange
        const string packageName = "MultiAssetPackage";
        const string packageVersion = "1.0.0";
        
        // Create package with multiple asset types
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        
        // 1. Compile assets (lib folder)
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // 2. Native assets
        var nativePath = Path.Combine(packageRoot, "native");
        Directory.CreateDirectory(nativePath);
        File.WriteAllText(Path.Combine(nativePath, "native.dll"), "native library");
        
        // 3. Build assets
        var buildPath = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "props file");
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.targets"), "targets file");
        
        // 4. Content files
        var contentPath = Path.Combine(packageRoot, "contentfiles", "any", "netstandard2.0");
        Directory.CreateDirectory(contentPath);
        File.WriteAllText(Path.Combine(contentPath, "resource.txt"), "content resource");
        
        // Create task item to include all assets
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "all");
        
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
        // Should find all assets (5 total) since we included all asset types
        task.LibraryContentFiles.Should().HaveCount(5);
        
        // Verify we have the expected asset types
        task.LibraryContentFiles.Should().Contain(item => item.GetMetadata("AssetType") == "compile");
        task.LibraryContentFiles.Should().Contain(item => item.GetMetadata("AssetType") == "build");
        task.LibraryContentFiles.Should().Contain(item => item.GetMetadata("AssetType") == "contentfiles");
        task.LibraryContentFiles.Should().Contain(item => item.GetMetadata("AssetType") == "native");
    }
    
    [Test]
    public void Execute_WithNonStandardFiles_InLibFolder_ClassifiesAsCompileAssets()
    {
        // Arrange
        const string packageName = "NonStandardLibPackage";
        const string packageVersion = "1.0.0";
        
        // Create standard package structure
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        var libPath = Path.Combine(packageRoot, "lib", "netstandard2.0");
        Directory.CreateDirectory(libPath);
        
        // Create various file types in lib folder
        File.WriteAllText(Path.Combine(libPath, $"{packageName}.dll"), "assembly");
        File.WriteAllText(Path.Combine(libPath, $"{packageName}.xml"), "xml docs");
        File.WriteAllText(Path.Combine(libPath, $"{packageName}.pdb"), "debug symbols");
        File.WriteAllText(Path.Combine(libPath, "resource.txt"), "text resource");
        File.WriteAllText(Path.Combine(libPath, "image.png"), "image resource");
        File.WriteAllText(Path.Combine(libPath, "config.json"), "config file");
        
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
        // All files in lib folder should be considered compile assets
        task.LibraryContentFiles.Should().HaveCount(6);
        foreach (var file in task.LibraryContentFiles)
            file.GetMetadata("AssetType").Should().Be("compile");
    }
    
    [Test]
    public void Execute_AssetSpecifications_ForDependencyPackages()
    {
        // Arrange
        const string mainPackage = "MainAssetPackage";
        const string dependencyPackage = "DependencyAssetPackage";
        const string packageVersion = "1.0.0";
        
        // Create packages
        CreateTestPackageStructure(mainPackage, packageVersion, "netstandard2.0", true);
        CreateTestPackageStructure(dependencyPackage, packageVersion, "netstandard2.0", true);
        
        // Create nuspec for dependency
        var nuspecDir = Path.Combine(_testDirectory, mainPackage.ToLowerInvariant(), packageVersion);
        Directory.CreateDirectory(nuspecDir);
        var nuspecContent = 
            $"""
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{mainPackage}</id>
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
        File.WriteAllText(Path.Combine(nuspecDir, $"{mainPackage}.nuspec"), nuspecContent);
        SetupTestNuGetRepositoryStructure(mainPackage, dependencyPackage, packageVersion);
        
        // Create task item with IncludeAssets=none for the main package
        var taskItem = new TaskItem(mainPackage);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "none");  // No assets from main package
        
        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            FullDependencyGraph = true,  // Process dependencies
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };
        
        // Act
        var result = task.Execute();
        
        // Assert
        result.Should().BeTrue();
        // Main package should have no assets due to IncludeAssets=none
        task.LibraryContentFiles.Any(item => item.GetMetadata("Package") == mainPackage).Should().BeFalse();
        
        // Dependency package should have assets (uses default asset specification)
        task.LibraryContentFiles.Any(item => item.GetMetadata("Package") == dependencyPackage).Should().BeTrue();
    }
    
    [Test]
    public void Execute_WithDefaultPrivateAssets_HandlesDefaultsCorrectly()
    {
        // Arrange
        const string packageName = "DefaultPrivatePackage";
        const string packageVersion = "1.0.0";
        
        // Create package with multiple asset types that are private by default
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        
        // Compile assets
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // Build assets (private by default)
        var buildPath = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "props file");
        
        // Analyzer assets (private by default)
        var analyzerPath = Path.Combine(packageRoot, "analyzers", "dotnet", "cs");
        Directory.CreateDirectory(analyzerPath);
        File.WriteAllText(Path.Combine(analyzerPath, $"{packageName}.Analyzer.dll"), "analyzer assembly");
        
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        
        // Test with default Private=false
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
        
        task.LibraryContentFiles.Should().HaveCount(3, "All assets should be included");
        
        // Verify which assets are private by default
        var compileAssets = task.LibraryContentFiles
            .Where(item => item.GetMetadata("AssetType") == "compile")
            .ToArray();
        compileAssets.Should().HaveCount(1, "Should have one compile asset");
        compileAssets[0].GetMetadata("IsPrivate").Should().Be("False", "Compile assets should not be private by default");
        
        var buildAssets = task.LibraryContentFiles
            .Where(item => item.GetMetadata("AssetType") == "build")
            .ToArray();
        buildAssets.Should().HaveCount(1, "Should have one build asset");
        buildAssets[0].GetMetadata("IsPrivate").Should().Be("True", "Build assets should be private by default");
        
        var analyzerAssets = task.LibraryContentFiles
            .Where(item => item.GetMetadata("AssetType") == "analyzers")
            .ToArray();
        analyzerAssets.Should().HaveCount(1, "Should have one analyzer asset");
        analyzerAssets[0].GetMetadata("IsPrivate").Should().Be("True", "Analyzer assets should be private by default");
    }
    
    [Test]
    public void Execute_WithCustomAssetCombinations_HandlesCorrectly()
    {
        // Arrange
        const string packageName = "CustomAssetComboPackage";
        const string packageVersion = "1.0.0";
        
        // Create package with multiple asset types
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        
        // Create assets of different types
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        var buildPath = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "props file");
        
        var contentPath = Path.Combine(packageRoot, "contentfiles", "any", "netstandard2.0");
        Directory.CreateDirectory(contentPath);
        File.WriteAllText(Path.Combine(contentPath, "content.txt"), "content file");
        
        // Test with custom include/exclude/private combinations
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "compile;contentfiles");  // Only include compile and content assets
        taskItem.SetMetadata("PrivateAssets", "contentfiles");  // Mark content as private
        
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
        task.LibraryContentFiles.Should().HaveCount(2, "Only compile and content assets should be included");
        
        // Check which assets are marked as private
        var privateFiles = task.LibraryContentFiles
            .Where(item => item.GetMetadata("IsPrivate").ToLowerInvariant() == "true")
            .ToArray();
        privateFiles.Should().HaveCount(1, "Only content files should be marked as private");
        privateFiles[0].ItemSpec.Should().EndWith("content.txt", "Content file should be marked as private");
        
        // Check which assets are not private
        var nonPrivateFiles = task.LibraryContentFiles
            .Where(item => item.GetMetadata("IsPrivate").ToLowerInvariant() != "true")
            .ToArray();
        nonPrivateFiles.Should().HaveCount(1, "Compile assets should not be marked as private");
        nonPrivateFiles[0].ItemSpec.Should().EndWith(".dll", "DLL should not be private");
    }

    [Test]
    public void Execute_WithPrivateAssetsAll_IncludesAssetsWithIsPrivateMetadata()
    {
        // Arrange
        const string packageName = "PrivateAssetsAllPackage";
        const string packageVersion = "1.0.0";
        
        // Create a package with compile assets
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // Create task item with PrivateAssets="all"
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("PrivateAssets", "all");

        // Test with Private=false (default)
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
        // Now the assets should be included but marked as private
        task.LibraryContentFiles.Should().HaveCount(1, "Assets should be included with IsPrivate metadata");
        task.LibraryContentFiles[0].GetMetadata("IsPrivate").Should().Be("True", "Asset should be marked as private");
        
        // This represents the real-world scenario from the prompt
        var resolvedReferences = task.LibraryContentFiles
            .Where(item => 
                item.GetMetadata("Extension") == ".dll" && 
                item.GetMetadata("IsPrivate").ToLower() != "true" && 
                item.GetMetadata("AssetType") == "compile")
            .ToList();
        
        // Should be empty since all assets are private
        resolvedReferences.Should().BeEmpty("No assets should match the ResolvedReference filter condition");
    }

    [Test]
    public void Execute_PrivateAssetsAreIncludedButMarkedAsPrivate()
    {
        // Arrange
        const string packageName = "PrivacyMetadataPackage";
        const string packageVersion = "1.0.0";
        
        // Create a package with compile assets
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // Create task item with PrivateAssets=compile
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("PrivateAssets", "compile");

        // With the updated behavior, Private flag only affects metadata value
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
        
        // Assets should be included but marked as private
        task.LibraryContentFiles.Should().HaveCount(1, "Private assets should now be included regardless of Private flag");
        task.LibraryContentFiles[0].GetMetadata("IsPrivate").Should().Be("True", "Asset should be marked as private");
        
        // Demonstrate how consumers would filter private assets manually
        var filteredReferences = task.LibraryContentFiles
            .Where(item => item.GetMetadata("IsPrivate").ToLowerInvariant() != "true")
            .ToList();
            
        filteredReferences.Should().BeEmpty("Manual filtering should respect IsPrivate metadata");
    }

    [Test]
    public void Execute_ClassifiesAssetTypesCorrectly()
    {
        // Arrange
        const string packageName = "AssetTypePackage";
        const string packageVersion = "1.0.0";
        
        // Create package with multiple asset types
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        
        // Create different asset folders and files
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // Build assets
        var buildPath = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "props file");
        
        // Native assets
        var nativePath = Path.Combine(packageRoot, "native");
        Directory.CreateDirectory(nativePath);
        File.WriteAllText(Path.Combine(nativePath, "native.dll"), "native library");
        
        // Content files
        var contentPath = Path.Combine(packageRoot, "contentfiles", "any", "netstandard2.0");
        Directory.CreateDirectory(contentPath);
        File.WriteAllText(Path.Combine(contentPath, "content.txt"), "content file");
        
        // Analyzer files
        var analyzerPath = Path.Combine(packageRoot, "analyzers", "dotnet", "cs");
        Directory.CreateDirectory(analyzerPath);
        File.WriteAllText(Path.Combine(analyzerPath, "analyzer.dll"), "analyzer assembly");
        
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
        task.LibraryContentFiles.Should().HaveCountGreaterThanOrEqualTo(5);
        
        // Verify correct asset classification
        task.LibraryContentFiles.Should().Contain(item => 
            item.ItemSpec.EndsWith(".dll") && 
            !item.ItemSpec.Contains("native") && 
            !item.ItemSpec.Contains("analyzers") && 
            item.GetMetadata("AssetType") == "compile");
        
        task.LibraryContentFiles.Should().Contain(item => 
            item.ItemSpec.Contains("build") && 
            item.GetMetadata("AssetType") == "build");
        
        task.LibraryContentFiles.Should().Contain(item => 
            item.ItemSpec.Contains("native") && 
            item.GetMetadata("AssetType") == "native");
        
        task.LibraryContentFiles.Should().Contain(item => 
            item.ItemSpec.Contains("contentfiles") && 
            item.GetMetadata("AssetType") == "contentfiles");
        
        task.LibraryContentFiles.Should().Contain(item => 
            item.ItemSpec.Contains("analyzers") && 
            item.GetMetadata("AssetType") == "analyzers");
    }

    [Test]
    public void Execute_AssetSpecification_HandlesAllValues()
    {
        // Arrange
        const string packageName = "AllAssetTypesPackage";
        const string packageVersion = "1.0.0";
        
        // Create package with multiple asset types
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        
        // Create all typical asset folders
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        var buildPath = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "props file");
        
        var buildMultiPath = Path.Combine(packageRoot, "buildmultitargeting");
        Directory.CreateDirectory(buildMultiPath);
        File.WriteAllText(Path.Combine(buildMultiPath, $"{packageName}.props"), "multitargeting props");
        
        var buildTransitivePath = Path.Combine(packageRoot, "buildtransitive");
        Directory.CreateDirectory(buildTransitivePath);
        File.WriteAllText(Path.Combine(buildTransitivePath, $"{packageName}.props"), "transitive props");
        
        var nativePath = Path.Combine(packageRoot, "native");
        Directory.CreateDirectory(nativePath);
        File.WriteAllText(Path.Combine(nativePath, "native.dll"), "native library");
        
        var contentPath = Path.Combine(packageRoot, "contentfiles", "any", "netstandard2.0");
        Directory.CreateDirectory(contentPath);
        File.WriteAllText(Path.Combine(contentPath, "content.txt"), "content file");
        
        var analyzerPath = Path.Combine(packageRoot, "analyzers", "dotnet", "cs");
        Directory.CreateDirectory(analyzerPath);
        File.WriteAllText(Path.Combine(analyzerPath, "analyzer.dll"), "analyzer assembly");
        
        // Test with IncludeAssets="all"
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "all");
        
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
        task.LibraryContentFiles.Should().HaveCountGreaterThanOrEqualTo(7, "All assets should be included");
        
        // Check for each asset type
        var assetTypes = task.LibraryContentFiles
            .Select(item => item.GetMetadata("AssetType"))
            .Distinct()
            .ToList();
        
        assetTypes.Should().Contain("compile");
        assetTypes.Should().Contain("build");
        assetTypes.Should().Contain("buildmultitargeting");
        assetTypes.Should().Contain("buildtransitive");
        assetTypes.Should().Contain("contentfiles");
        assetTypes.Should().Contain("analyzers");
        assetTypes.Should().Contain("native");
    }

    [Test]
    public void Execute_WithAssetNone_IncludesNoFiles()
    {
        // Arrange
        const string packageName = "NoAssetsPackage";
        const string packageVersion = "1.0.0";
        
        // Create standard package
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        // Create task item with IncludeAssets=none
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "none");
        
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
        result.Should().BeTrue("Task should succeed even with no assets");
        task.LibraryContentFiles.Should().BeEmpty("No assets should be included with IncludeAssets=none");
    }

    [Test]
    public void Execute_WithComplexAssetSpecification_ProcessesCorrectly()
    {
        // Arrange
        const string packageName = "ComplexAssetPackage";
        const string packageVersion = "1.0.0";
        
        // Create package with multiple asset types
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        
        // Create different asset types
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        var buildPath = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "props file");
        
        var contentPath = Path.Combine(packageRoot, "contentfiles", "any", "netstandard2.0");
        Directory.CreateDirectory(contentPath);
        File.WriteAllText(Path.Combine(contentPath, "content.txt"), "content file");
        
        var nativePath = Path.Combine(packageRoot, "native");
        Directory.CreateDirectory(nativePath);
        File.WriteAllText(Path.Combine(nativePath, "native.dll"), "native library");
        
        // Create complex asset specification
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "compile;native");
        taskItem.SetMetadata("ExcludeAssets", "build;contentfiles");
        taskItem.SetMetadata("PrivateAssets", "native");
        
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
        
        // Should include only compile and native assets
        var assetTypes = task.LibraryContentFiles
            .Select(item => item.GetMetadata("AssetType"))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
            
        assetTypes.Should().BeEquivalentTo(["compile", "native"], "Only compile and native assets should be included");
        
        // Build and contentfiles should be excluded
        task.LibraryContentFiles.Should().NotContain(item => 
            item.GetMetadata("AssetType") == "build" || 
            item.GetMetadata("AssetType") == "contentfiles");
        
        // Verify which assets are private
        var privateAssets = task.LibraryContentFiles
            .Where(item => item.GetMetadata("IsPrivate") == "True")
            .Select(item => item.GetMetadata("AssetType"))
            .Distinct()
            .ToList();
            
        privateAssets.Should().BeEquivalentTo(["native"], "Only native assets should be marked as private");
        
        // Compile assets should not be private
        task.LibraryContentFiles
            .Where(item => item.GetMetadata("AssetType") == "compile")
            .All(item => item.GetMetadata("IsPrivate") == "False")
            .Should().BeTrue("Compile assets should not be private");
    }

    [Test]
    public void Execute_AssetTypeNone_WithIndividualAssetsListed_HandlesCorrectly()
    {
        // Arrange
        const string packageName = "AssetTypeNonePackage";
        const string packageVersion = "1.0.0";
        
        // Create package with all asset types
        var packageRoot = Path.Combine(_testDirectory, packageName.ToLowerInvariant(), packageVersion);
        CreateTestPackageStructure(packageName, packageVersion, "netstandard2.0", true);
        
        var buildPath = Path.Combine(packageRoot, "build");
        Directory.CreateDirectory(buildPath);
        File.WriteAllText(Path.Combine(buildPath, $"{packageName}.props"), "props file");
        
        // Test the behavior of "none" vs explicitly listing asset types
        // First with IncludeAssets="none;compile" - should include compile assets
        var taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "none;compile");
        
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
        
        // "none" takes precedence over other specifications
        task.LibraryContentFiles.Should().BeEmpty("When 'none' is specified, no assets should be included");
        
        // Now try with a valid combination that includes compile assets but excludes build
        taskItem = new TaskItem(packageName);
        taskItem.SetMetadata("Version", packageVersion);
        taskItem.SetMetadata("IncludeAssets", "compile");
        taskItem.SetMetadata("ExcludeAssets", "build");
        
        task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = _testDirectory,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };
        
        // Act
        result = task.Execute();
        
        // Assert
        result.Should().BeTrue();
        task.LibraryContentFiles.Should().HaveCount(1);
        task.LibraryContentFiles[0].GetMetadata("AssetType").Should().Be("compile");
    }

    [Test]
    [Explicit("Requires UnityEngine.Modules 2022.3.21 to be already present in the local NuGet cache")]
    public void Execute_WithUnityEngineModules_FindsExpectedContent()
    {
        // Arrange - Create a task item that exactly matches the real-world usage
        var taskItem = new TaskItem("UnityEngine.Modules");
        taskItem.SetMetadata("Version", "2022.3.21");
        taskItem.SetMetadata("IncludeAssets", "compile");
        taskItem.SetMetadata("PrivateAssets", "all");
        
        // Use the actual user's NuGet package folder instead of test directory
        string nugetPackageRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        TestContext.WriteLine($"Using NuGet package root: {nugetPackageRoot}");
        
        // If the package directory doesn't exist, skip the test
        string packageDir = Path.Combine(nugetPackageRoot, "unityengine.modules", "2022.3.21");
        if (!Directory.Exists(packageDir))
        {
            throw new InconclusiveException(
                $"Package UnityEngine.Modules 2022.3.21 not found at {packageDir}. This test requires the package to be pre-cached.");
        }
        
        // Check if there are DLLs to find
        string libPath = Path.Combine(packageDir, "lib", "netstandard2.0");
        if (!Directory.Exists(libPath))
        {
            throw new InconclusiveException($"Expected lib/netstandard2.0 folder not found at {libPath}");
        }
        
        var expectedDllCount = Directory.GetFiles(libPath, "*.dll").Length;
        TestContext.WriteLine($"Found {expectedDllCount} DLLs in {libPath}");
        
        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = nugetPackageRoot,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            BuildEngine = _mockBuildEngine.Object,
            CustomNuGetLogger = _nuGetLogger
        };
        
        // Act - Execute the task with debug logging
        var result = task.Execute();
        
        // Assert
        result.Should().BeTrue("Task should execute successfully");
        
        task.LibraryContentFiles.Should().NotBeEmpty(
            $"Should find content files for UnityEngine.Modules. Package exists at {packageDir}");
        
        // Specifically check for expected modules
        var moduleNames = task.LibraryContentFiles
            .Select(file => Path.GetFileName(file.ItemSpec))
            .ToList();
            
        TestContext.WriteLine($"Found modules: {string.Join(", ", moduleNames)}");
        
        // Check if well-known Unity modules are present
        moduleNames.Should().Contain(item => item.Contains("UnityEngine.") && item.EndsWith(".dll"),
            "Should find Unity engine module DLLs");
        
        // Additionally check metadata
        task.LibraryContentFiles.Should().OnlyContain(
            file => file.GetMetadata("Package") == "UnityEngine.Modules", 
            "All content files should be associated with the UnityEngine.Modules package");
            
        task.LibraryContentFiles.Should().OnlyContain(
            file => file.GetMetadata("IsPrivate") == "True", 
            "All files should be marked as private due to PrivateAssets='all'");
    }

    [Test]
    [Explicit("Requires UnityEngine.Modules 2022.3.21 to be already present in the local NuGet cache")]
    public void Execute_WithUnityEngineModules_DiagnosticLogging()
    {
        // Arrange - Create a more verbose diagnostic test to see exactly what's happening
        var taskItem = new TaskItem("UnityEngine.Modules");
        taskItem.SetMetadata("Version", "2022.3.21");
        taskItem.SetMetadata("IncludeAssets", "compile");
        taskItem.SetMetadata("PrivateAssets", "all");
        
        string nugetPackageRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget", "packages");

        TestContext.WriteLine($"Using NuGet package root: {nugetPackageRoot}");
        string packageDir = Path.Combine(nugetPackageRoot, "unityengine.modules", "2022.3.21");
        
        if (!Directory.Exists(packageDir))
        {
            throw new InconclusiveException(
                $"Package UnityEngine.Modules 2022.3.21 not found at {packageDir}. This test requires the package to be pre-cached.");
        }

        TestContext.WriteLine("Package directory structure:");
        LogDirectoryContents(packageDir);
        
        // Create task with diagnostic-level logging
        var nugetLogger = new NuGetTraceLogger(NuGet.Common.LogLevel.Debug);
        var mockBuildEngine = new Mock<IBuildEngine>();
        
        // Capture detailed output
        var buildMessages = new List<string>();
        var buildWarnings = new List<string>();
        var buildErrors = new List<string>();
        
        mockBuildEngine
            .Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(e => buildMessages.Add(e.Message));
        mockBuildEngine
            .Setup(x => x.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
            .Callback<BuildWarningEventArgs>(e => buildWarnings.Add(e.Message));
        mockBuildEngine
            .Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(e => buildErrors.Add(e.Message));
        
        var task = new FindNetStandardCompatibleContentViaNuGet
        {
            NuGetPackageRoot = nugetPackageRoot,
            MaximumNetStandard = "2.1",
            Packages = [taskItem],
            BuildEngine = mockBuildEngine.Object,
            CustomNuGetLogger = nugetLogger
        };
        
        // Act
        var result = task.Execute();
        
        // Assert & log diagnostic information
        TestContext.WriteLine($"Task execution result: {result}");
        TestContext.WriteLine($"Found {task.LibraryContentFiles.Length} content files");
        
        TestContext.WriteLine("Build Messages:");
        foreach (var message in buildMessages)
            TestContext.WriteLine($"  - {message}");
            
        TestContext.WriteLine("Build Warnings:");
        foreach (var warning in buildWarnings)
            TestContext.WriteLine($"  - {warning}");
            
        TestContext.WriteLine("Build Errors:");
        foreach (var error in buildErrors)
            TestContext.WriteLine($"  - {error}");
            
        TestContext.WriteLine("Asset Specifications for package:");
        TestContext.WriteLine($"  - IncludeAssets: {taskItem.GetMetadata("IncludeAssets")}");
        TestContext.WriteLine($"  - PrivateAssets: {taskItem.GetMetadata("PrivateAssets")}");
        
        // If we found any content, show details
        if (task.LibraryContentFiles.Length > 0)
        {
            TestContext.WriteLine("Found Content Files:");
            foreach (var file in task.LibraryContentFiles)
            {
                TestContext.WriteLine($"  - {file.ItemSpec}");
                TestContext.WriteLine($"    Package: {file.GetMetadata("Package")}");
                TestContext.WriteLine($"    AssetType: {file.GetMetadata("AssetType")}");
                TestContext.WriteLine($"    IsPrivate: {file.GetMetadata("IsPrivate")}");
                TestContext.WriteLine($"    TargetFramework: {file.GetMetadata("TargetFramework")}");
            }
        }
        else
        {
            TestContext.WriteLine("No content files found!");
        }
    }
    
    private void LogDirectoryContents(string directory, int indent = 0)
    {
        if (!Directory.Exists(directory))
            return;
            
        string indentation = new string(' ', indent * 2);
        
        // Log files
        foreach (var file in Directory.GetFiles(directory))
        {
            TestContext.WriteLine($"{indentation}- {Path.GetFileName(file)}");
        }
        
        // Log and recursively process subdirectories
        foreach (var subdir in Directory.GetDirectories(directory))
        {
            TestContext.WriteLine($"{indentation}+ {Path.GetFileName(subdir)}/");
            LogDirectoryContents(subdir, indent + 1);
        }
    }
}
