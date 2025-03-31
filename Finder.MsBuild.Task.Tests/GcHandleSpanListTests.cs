using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using NUnit.Framework;

namespace Finder.MsBuild.Task.Tests;

public class GcHandleSpanListTests
{
    [Test]
    public void Constructor_InitializesCorrectly()
    {
        Span<GCHandle> handles = stackalloc GCHandle[5];
        var list = new GcHandleSpanList<string>(handles);

        list.Count.Should().Be(0);
        list.BackingSpan.Length.Should().Be(5);
    }

    [Test]
    public void Add_IncreasesCount()
    {
        Span<GCHandle> handles = stackalloc GCHandle[3];
        var list = new GcHandleSpanList<string>(handles);

        list.Add("test1");
        list.Count.Should().Be(1);

        list.Add("test2");
        list.Count.Should().Be(2);
    }

    [Test]
    public void Add_ThrowsWhenFull()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[2];

        try
        {
            list.Add("test1");
            list.Add("test2");
            list.Add("test3");
            // ReSharper disable once NUnitAssertMigration
            throw new AssertionException("Should have thrown InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().Be("GcHandleSpanList is full");
        }
    }

    [Test]
    public void Indexer_GetsAndSetsValues()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test2");

        list[0].Should().Be("test1");
        list[1].Should().Be("test2");

        list[0] = "modified";
        list[0].Should().Be("modified");
    }

    [Test]
    public void Clear_FreesHandlesAndResetsCount()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test2");

        list.Clear();
        list.Count.Should().Be(0);

        // Verify handles are freed by checking IsAllocated
        var span = list.BackingSpan.BackingSpan;
        span[0].IsAllocated.Should().BeFalse("Handle should be freed after Clear");
        span[1].IsAllocated.Should().BeFalse("Handle should be freed after Clear");
    }

    [Test]
    public void Contains_FindsExistingItems()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test2");

        list.Contains("test1").Should().BeTrue();
        list.Contains("test2").Should().BeTrue();
        list.Contains("test3").Should().BeFalse();
    }

    [Test]
    public void IndexOf_ReturnsCorrectIndex()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test2");

        list.IndexOf("test1").Should().Be(0);
        list.IndexOf("test2").Should().Be(1);
        list.IndexOf("test3").Should().Be(-1);
    }

    [Test]
    public void Insert_AddsItemAtSpecifiedPosition()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test3");

        list.Insert(1, "test2");

        list[0].Should().Be("test1");
        list[1].Should().Be("test2");
        list[2].Should().Be("test3");
    }

    [Test]
    public void Remove_RemovesFirstOccurrence()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[4];


        list.Add("test1");
        list.Add("test2");
        list.Add("test3");

        var result = list.Remove("test2");

        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list[0].Should().Be("test1");
        list[1].Should().Be("test3");
    }

    [Test]
    public void RemoveAt_RemovesItemAtIndex()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[4];

        list.Add("test1");
        list.Add("test2");
        list.Add("test3");

        list.RemoveAt(1);

        list.Count.Should().Be(2);
        list[0].Should().Be("test1");
        list[1].Should().Be("test3");
    }

    [Test]
    public void Filled_ReturnsUsedPortion()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[5];

        list.Add("test1");
        list.Add("test2");

        var filled = list.Filled.BackingSpan;

        filled.Length.Should().Be(2);
        (filled[0].Target as string).Should().Be("test1");
        (filled[1].Target as string).Should().Be("test2");
    }

    [Test]
    public void Remaining_ReturnsUnusedPortion()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[5];

        list.Add("test1");
        list.Add("test2");

        var remaining = list.Remaining;

        remaining.Length.Should().Be(3);
    }

    [Test]
    public void Enumerator_IteratesOverItems()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test2");

        var count = 0;
        var items = new string[2];

        var enumerator = list.GetEnumerator();
        while (enumerator.MoveNext())
            items[count++] = enumerator.Current!;

        count.Should().Be(2);
        items[0].Should().Be("test1");
        items[1].Should().Be("test2");
    }

    [Test]
    public void Clear_FreesAllHandles()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];
        var handles = list.BackingSpan.BackingSpan;

        list.Add("test1");
        list.Add("test2");

        list.Clear();

        handles[0].IsAllocated.Should().BeFalse("Handle should be freed after Dispose");
        handles[1].IsAllocated.Should().BeFalse("Handle should be freed after Dispose");
        list.Count.Should().Be(0);
    }

    [Test]
    public void CopyTo_CopiesElementsToArray()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test2");

        var array = new string[3];
        list.CopyTo(array, 0);

        array[0].Should().Be("test1");
        array[1].Should().Be("test2");
        array[2].Should().BeNull();
    }

    [Test]
    public void ImplicitConversion_GcHandleSpanListToGcHandleSpan_WorksCorrectly()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Add("test1");
        list.Add("test2");

        GcHandleSpan<string> span = list;

        span.Length.Should().Be(2);
        span[0].Should().Be("test1");
        span[1].Should().Be("test2");
    }

    [Test]
    public void ImplicitConversion_GcHandleSpanToGcHandleSpanList_WorksCorrectly()
    {
        using GcHandleSpanList<string> list = stackalloc GCHandle[3];

        list.Count.Should().Be(0);
    }

    [Test]
    public void AssetTypes_FromAssetTypes()
    {
        var all = AssetTypes.FromAssetType(AssetType.All);
        all.Should().Be("all");
        var none = AssetTypes.FromAssetType(AssetType.None);
        none.Should().Be("none");
        var allButAnalyzers = AssetTypes.FromAssetType(AssetType.All & ~AssetType.Analyzers);
        allButAnalyzers.Should().Be("compile;runtime;contentfiles;build;buildmultitargeting;buildtransitive;native");
    }
}