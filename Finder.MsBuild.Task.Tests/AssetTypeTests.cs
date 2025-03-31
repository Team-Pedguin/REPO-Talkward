using FluentAssertions;
using Microsoft.Build.Utilities;

namespace Finder.MsBuild.Task.Tests;

[TestFixture]
public class AssetTypeTests
{
    [Test]
    public void AssetTypes_ToString_ShouldReturnCorrectString()
    {
        // Test individual values
        AssetTypes.FromAssetType(AssetType.Compile).Should().Be("compile");
        AssetTypes.FromAssetType(AssetType.Runtime).Should().Be("runtime");
        AssetTypes.FromAssetType(AssetType.ContentFiles).Should().Be("contentfiles");
        AssetTypes.FromAssetType(AssetType.Build).Should().Be("build");
        AssetTypes.FromAssetType(AssetType.None).Should().Be("none");
        AssetTypes.FromAssetType(AssetType.All).Should().Be("all");
        
        // Test combined values
        var combined = AssetType.Compile | AssetType.Runtime;
        var result = AssetTypes.FromAssetType(combined);
        result.Should().Contain("compile");
        result.Should().Contain("runtime");
    }
    
    [Test]
    public void AssetTypes_ToAssetType_ShouldParseCorrectly()
    {
        // Test individual values
        AssetTypes.ToAssetType("compile").Should().Be(AssetType.Compile);
        AssetTypes.ToAssetType("runtime").Should().Be(AssetType.Runtime);
        AssetTypes.ToAssetType("contentfiles").Should().Be(AssetType.ContentFiles);
        AssetTypes.ToAssetType("none").Should().Be(AssetType.None);
        AssetTypes.ToAssetType("all").Should().Be(AssetType.All);
        AssetTypes.ToAssetType(null).Should().Be(AssetType.None);
        AssetTypes.ToAssetType("").Should().Be(AssetType.None);
        
        // Test combined values with different cases
        AssetTypes.ToAssetType("compile;runtime").Should().Be(AssetType.Compile | AssetType.Runtime);
        AssetTypes.ToAssetType("Compile;Runtime").Should().Be(AssetType.Compile | AssetType.Runtime);
        AssetTypes.ToAssetType("COMPILE;RUNTIME").Should().Be(AssetType.Compile | AssetType.Runtime);
    }
    
}
