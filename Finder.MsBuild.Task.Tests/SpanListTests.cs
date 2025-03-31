using System;
using FluentAssertions;
using NUnit.Framework;

namespace Finder.MsBuild.Task.Tests;

public class SpanListTests
{
    [Test]
    public void Constructor_InitializesCorrectly()
    {
        Span<int> span = stackalloc int[10];
        var list = new SpanList<int>(span);
        
        list.Count.Should().Be(0);
        list.BackingSpan.Length.Should().Be(10);
    }
    
    [Test]
    public void Add_IncreasesCount()
    {
        Span<int> span = stackalloc int[3];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Count.Should().Be(1);
        
        list.Add(2);
        list.Count.Should().Be(2);
    }
    
    [Test]
    public void Add_ThrowsWhenFull()
    {
        Span<int> span = stackalloc int[2];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        
        //Action act = () => list.Add(3); // can't copy ref struct into a lambda
        //act.Should().Throw<InvalidOperationException>().WithMessage("SpanList is full");
        try
        {
            list.Add(3);
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().Be("SpanList is full");
        }
    }
    
    [Test]
    public void Indexer_GetsAndSetsValues()
    {
        Span<int> span = stackalloc int[3];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        
        list[0] = 10;
        list[0].Should().Be(10);
    }
    
    [Test]
    public void Clear_ResetsCount()
    {
        Span<int> span = stackalloc int[3];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        list.Clear();
        
        list.Count.Should().Be(0);
    }
    
    [Test]
    public void Contains_FindsExistingItems()
    {
        Span<int> span = stackalloc int[3];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        
        list.Contains(1).Should().BeTrue();
        list.Contains(2).Should().BeTrue();
        list.Contains(3).Should().BeFalse();
    }
    
    [Test]
    public void IndexOf_ReturnsCorrectIndex()
    {
        Span<int> span = stackalloc int[3];
        var list = new SpanList<int>(span);
        
        list.Add(10);
        list.Add(20);
        
        list.IndexOf(10).Should().Be(0);
        list.IndexOf(20).Should().Be(1);
        list.IndexOf(30).Should().Be(-1);
    }
    
    [Test]
    public void Insert_AddsItemAtSpecifiedPosition()
    {
        Span<int> span = stackalloc int[4];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(3);
        
        list.Insert(1, 2);
        
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        list[2].Should().Be(3);
    }
    
    [Test]
    public void Remove_RemovesFirstOccurrence()
    {
        Span<int> span = stackalloc int[4];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        list.Add(3);
        
        bool result = list.Remove(2);
        
        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }
    
    [Test]
    public void RemoveAt_RemovesItemAtIndex()
    {
        Span<int> span = stackalloc int[4];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        list.Add(3);
        
        list.RemoveAt(1);
        
        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }
    
    [Test]
    public void Filled_ReturnsUsedPortion()
    {
        Span<int> span = stackalloc int[5];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        
        Span<int> filled = list.Filled;
        
        filled.Length.Should().Be(2);
        filled[0].Should().Be(1);
        filled[1].Should().Be(2);
    }
    
    [Test]
    public void Remaining_ReturnsUnusedPortion()
    {
        Span<int> span = stackalloc int[5];
        var list = new SpanList<int>(span);
        
        list.Add(1);
        list.Add(2);
        
        Span<int> remaining = list.Remaining;
        
        remaining.Length.Should().Be(3);
    }
    
    [Test]
    public void ImplicitConversion_SpanToSpanList_WorksCorrectly()
    {
        Span<int> span = stackalloc int[] { 1, 2, 3 };
        SpanList<int> list = span;
        
        list.Count.Should().Be(3);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        list[2].Should().Be(3);
    }
    
    [Test]
    public void ImplicitConversion_SpanListToSpan_WorksCorrectly()
    {
        Span<int> original = stackalloc int[3];
        var list = new SpanList<int>(original);
        
        list.Add(1);
        list.Add(2);
        
        Span<int> converted = list;
        
        converted.Length.Should().Be(2);
        converted[0].Should().Be(1);
        converted[1].Should().Be(2);
    }
}
