using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using NUnit.Framework;

namespace Finder.MsBuild.Task.Tests;

public class GcHandleSpanTests
{
    [Test]
    public void Constructor_InitializesCorrectly()
    {
        Span<GCHandle> handles = stackalloc GCHandle[5];
        var span = new GcHandleSpan<string>(handles);
        
        span.Length.Should().Be(5);
    }
    
    [Test]
    public void Indexer_GetsAndSetsValues()
    {
        var obj1 = "test1";
        var obj2 = "test2";
        
        Span<GCHandle> handles = stackalloc GCHandle[2];
        handles[0] = GCHandle.Alloc(obj1, GCHandleType.Normal);
        handles[1] = GCHandle.Alloc(obj2, GCHandleType.Normal);
        
        try
        {
            var span = new GcHandleSpan<string>(handles);
            
            span[0].Should().Be("test1");
            span[1].Should().Be("test2");
            
            ((string)handles[0].Target!).Should().Be("test1");
            ((string)handles[1].Target!).Should().Be("test2");
            
            span[0] = "modified";
            
            ((string)handles[0].Target!).Should().Be("modified");
            ((string)handles[1].Target!).Should().Be("test2");
        }
        finally
        {
            handles[0].Free();
            handles[1].Free();
        }
    }
    
    [Test]
    public void WorksWithValueTypes()
    {
        object boxed1 = 42;
        object boxed2 = 99;
        
        Span<GCHandle> handles = stackalloc GCHandle[2];
        handles[0] = GCHandle.Alloc(boxed1, GCHandleType.Normal);
        handles[1] = GCHandle.Alloc(boxed2, GCHandleType.Normal);
        
        try
        {
            var span = new GcHandleSpan<object>(handles);
            
            span[0].Should().Be(42);
            span[1].Should().Be(99);
        }
        finally
        {
            handles[0].Free();
            handles[1].Free();
        }
    }
}
