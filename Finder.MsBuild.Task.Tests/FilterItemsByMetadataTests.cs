using System;
using System.Diagnostics;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using NUnit.Framework;

namespace Finder.MsBuild.Task.Tests;

[TestFixture]
public class FilterItemsByMetadataTests
{
    private Mock<IBuildEngine> _mockBuildEngine;
    private List<BuildMessageEventArgs> _loggedMessages;

    [SetUp]
    public void Setup()
    {
        _loggedMessages = [];
        _mockBuildEngine = new Mock<IBuildEngine>();
        _mockBuildEngine
            .Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
            .Callback<BuildErrorEventArgs>(e => Trace.TraceError($"MSBuild: {e.Message}"));
        _mockBuildEngine
            .Setup(x => x.LogWarningEvent(It.IsAny<BuildWarningEventArgs>()))
            .Callback<BuildWarningEventArgs>(e => Trace.TraceWarning($"MSBuild: {e.Message}"));
        _mockBuildEngine
            .Setup(x => x.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(e =>
            {
                _loggedMessages.Add(e);
                Trace.TraceInformation($"MSBuild: {e.Message}");
            });
    }

    [Test]
    public void Execute_WithNoItemsToFilter_ReturnsEmptyResult()
    {
        // Arrange
        var filterItems = new[]
        {
            CreateTaskItem("Package1"),
            CreateTaskItem("Package2")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = [],
            FilterByItems = filterItems,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed with empty input");
        task.FilteredItems.Should().BeEmpty("No items to filter should result in empty output");
    }

    [Test]
    public void Execute_WithNoFilterItems_ReturnsNoItemsWhenExclusiveFilter()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "Package1"),
            CreateTaskItem("Reference2", "Package2")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = [],
            ExclusiveFilter = true,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should().BeEmpty("With exclusive filter and no filter items, no items should match");
    }

    [Test]
    public void Execute_WithNoFilterItems_ReturnsAllItemsWhenNonExclusiveFilter()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "Package1"),
            CreateTaskItem("Reference2", "Package2")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = [],
            ExclusiveFilter = false,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should()
            .HaveCount(2, "With non-exclusive filter and no filter items, all items should match");
        task.FilteredItems.Should().BeEquivalentTo(itemsToFilter, "All original items should be returned");
    }

    [Test]
    public void Execute_WithExclusiveFilter_ReturnsOnlyMatchingItems()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "Package1"),
            CreateTaskItem("Reference2", "Package2"),
            CreateTaskItem("Reference3", "Package3")
        };

        var filterItems = new[]
        {
            CreateTaskItem("Package1"),
            CreateTaskItem("Package3")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = filterItems,
            ExclusiveFilter = true,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should().HaveCount(2, "Only matching items should be included");
        task.FilteredItems.Select(i => i.ItemSpec).Should().BeEquivalentTo(
            ["Reference1", "Reference3"],
            "Only references from Package1 and Package3 should be included");
    }

    [Test]
    public void Execute_WithNonExclusiveFilter_ReturnsOnlyNonMatchingItems()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "Package1"),
            CreateTaskItem("Reference2", "Package2"),
            CreateTaskItem("Reference3", "Package3")
        };

        var filterItems = new[]
        {
            CreateTaskItem("Package1"),
            CreateTaskItem("Package3")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = filterItems,
            ExclusiveFilter = false,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should().HaveCount(1, "Only non-matching items should be included");
        task.FilteredItems[0].ItemSpec.Should().Be("Reference2", "Only Reference2 from Package2 should be included");
    }

    [Test]
    public void Execute_WithCustomMetadataName_FiltersUsingSpecifiedMetadata()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItemWithCustomMetadata("Reference1", "CustomPackageId", "Package1"),
            CreateTaskItemWithCustomMetadata("Reference2", "CustomPackageId", "Package2")
        };

        var filterItems = new[]
        {
            CreateTaskItem("Package1")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = filterItems,
            MetadataName = "CustomPackageId",
            BuildEngine = _mockBuildEngine.Object
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should().HaveCount(1, "Only matching items should be included");
        task.FilteredItems[0].ItemSpec.Should().Be("Reference1", "Only Reference1 from Package1 should be included");
    }

    [Test]
    public void Execute_CaseInsensitiveMatching_MatchesRegardlessOfCase()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "package1"),
            CreateTaskItem("Reference2", "PACKAGE2")
        };

        var filterItems = new[]
        {
            CreateTaskItem("Package1"),
            CreateTaskItem("package2")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = filterItems,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should().HaveCount(2, "Matching should be case-insensitive");
        task.FilteredItems.Select(i => i.ItemSpec).Should().BeEquivalentTo(
            ["Reference1", "Reference2"],
            "Both references should match case-insensitively");
    }

    [Test]
    public void Execute_WithEmptyOrNullMetadata_SkipsItemsWithoutMetadata()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "Package1"),
            CreateTaskItem("Reference2", "Package2"),
            CreateTaskItem("Reference3", ""), // Empty metadata
            CreateTaskItem("Reference4", null) // Null metadata
        };

        var filterItems = new[]
        {
            CreateTaskItem("Package1"),
            CreateTaskItem("Package2")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = filterItems,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should().HaveCount(2, "Only items with valid matching metadata should be included");
        task.FilteredItems.Select(i => i.ItemSpec).Should().BeEquivalentTo(
            ["Reference1", "Reference2"],
            "Items with empty or null metadata should be excluded");
    }

    [Test]
    public void Execute_LogsAppropriateMessages()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "Package1"),
            CreateTaskItem("Reference2", "Package2"),
            CreateTaskItem("Reference3", "Package3")
        };

        var filterItems = new[]
        {
            CreateTaskItem("Package1"),
            CreateTaskItem("Package3")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = filterItems,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");

        _loggedMessages.Should().Contain(m =>
            m.Message!.Contains("Item with NuGetPackageId='Package1' matches a filter item") &&
            m.Importance == MessageImportance.Low);

        _loggedMessages.Should().Contain(m =>
            m.Message!.Contains("Item with NuGetPackageId='Package3' matches a filter item") &&
            m.Importance == MessageImportance.Low);

        _loggedMessages.Should().Contain(m =>
            m.Message!.Contains("Processed 3 items, matched 2 items") &&
            m.Importance == MessageImportance.Normal);
    }

    [Test]
    public void Execute_WithMultipleMatchingFilterItems_MatchesAnyFilterItem()
    {
        // Arrange
        var itemsToFilter = new[]
        {
            CreateTaskItem("Reference1", "Package1"),
            CreateTaskItem("Reference2", "Package2"),
            CreateTaskItem("Reference3", "Package2") // Another reference from Package2
        };

        var filterItems = new[]
        {
            CreateTaskItem("Package2"),
            CreateTaskItem("Package3")
        };

        var task = new FilterItemsByMetadata
        {
            ItemsToFilter = itemsToFilter,
            FilterByItems = filterItems,
            BuildEngine = _mockBuildEngine.Object,
            MetadataName = "NuGetPackageId"
        };

        // Act
        var result = task.Execute();

        // Assert
        result.Should().BeTrue("Task should succeed");
        task.FilteredItems.Should().HaveCount(2, "All items matching any filter item should be included");
        task.FilteredItems.Select(i => i.ItemSpec).Should().BeEquivalentTo(
            ["Reference2", "Reference3"],
            "Both references from Package2 should be included");
    }

    private static ITaskItem CreateTaskItem(string itemSpec, string? nuGetPackageId = null)
    {
        var taskItem = new TaskItem(itemSpec);
        if (nuGetPackageId != null)
        {
            taskItem.SetMetadata("NuGetPackageId", nuGetPackageId);
        }

        return taskItem;
    }

    private static ITaskItem CreateTaskItemWithCustomMetadata(string itemSpec, string metadataName,
        string metadataValue)
    {
        var taskItem = new TaskItem(itemSpec);
        taskItem.SetMetadata(metadataName, metadataValue);
        return taskItem;
    }
}