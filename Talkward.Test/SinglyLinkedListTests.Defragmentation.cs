using FluentAssertions;

namespace Talkward.Test;

public partial class SinglyLinkedListTests
{
    [Test]
    public void Defragment_EmptyList_DoesNothing()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        
        // Act
        list.Defragment();
        
        // Assert
        list.Count.Should().Be(0);
        list.IsDefragmented.Should().BeTrue();
    }
    
    [Test]
    public void Defragment_AlreadyDefragmentedList_MaintainsOrder()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
        
        // A fresh list should be defragmented already
        list.IsDefragmented.Should().BeTrue();
        
        // Act
        list.Defragment();
        
        // Assert
        list.Count.Should().Be(3);
        list.IsDefragmented.Should().BeTrue();
        list.ToArray().Should().BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
    }
    
    [Test]
    public void IsDefragmented_AfterModifications_ReturnsFalse()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
        list.AddLast(40);
        
        // A fresh list should be defragmented initially
        list.IsDefragmented.Should().BeTrue();
        
        // Act - create fragmentation
        list.Remove(20); // Remove from middle
        
        // Assert
        list.IsDefragmented.Should().BeFalse();
    }
    
    [Test]
    public void Defragment_FragmentedList_ReordersNodesContiguously()
    {
        // Arrange - create a heavily fragmented list
        var list = new SinglyLinkedList<int>();
        for (int i = 0; i < 10; i++)
        {
            list.AddLast(i * 10);
        }
        
        // Remove several items to create fragmentation
        list.Remove(20);
        list.Remove(50);
        list.Remove(80);
        
        // Add some new items that will use the free nodes
        list.AddLast(100);
        list.AddLast(110);
        
        // At this point, the list should be fragmented
        list.IsDefragmented.Should().BeFalse();
        
        // Act
        list.Defragment();
        
        // Assert
        list.Count.Should().Be(9);
        list.IsDefragmented.Should().BeTrue();
        list.ToArray().Should().BeEquivalentTo([0, 10, 30, 40, 60, 70, 90, 100, 110], 
            options => options.WithStrictOrdering());
    }
    
    [Test]
    public void Defragment_PreservesDataIntegrity()
    {
        // Arrange
        var list = new SinglyLinkedList<string>();
        list.AddLast("one");
        list.AddLast("two");
        list.AddLast("three");
        list.AddLast("four");
        list.AddLast("five");
        
        // Create fragmentation
        list.Remove("two");
        list.Remove("four");
        
        // Act
        list.Defragment();
        
        // Assert
        list.Count.Should().Be(3);
        list.ToArray().Should().BeEquivalentTo(["one", "three", "five"], 
            options => options.WithStrictOrdering());
        
        // Verify we can still perform operations correctly
        list.AddLast("six");
        list.AddFirst("zero");
        list.Remove("three");
        
        list.ToArray().Should().BeEquivalentTo(["zero", "one", "five", "six"], 
            options => options.WithStrictOrdering());
    }
    
    [Test]
    public void Defragment_LargeList_HandlesDefragmentationCorrectly()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        var expectedValues = new List<int>();
        
        // Add 1000 items
        for (int i = 0; i < 1000; i++)
        {
            list.AddLast(i);
            expectedValues.Add(i);
        }
        
        // Remove every third item to create fragmentation
        for (int i = 999; i >= 0; i -= 3)
        {
            list.Remove(i);
            expectedValues.Remove(i);
        }
        
        // Act
        list.Defragment();
        
        // Assert
        list.Count.Should().Be((nuint)expectedValues.Count);
        list.IsDefragmented.Should().BeTrue();
        list.ToArray().Should().BeEquivalentTo(expectedValues, options => options.WithStrictOrdering());
    }
    
    
    [Test]
    public void Defragment_MaintainsCorrectReferences_AfterComplexOperations()
    {
        // Arrange - create complex state with interleaved operations
        var list = new SinglyLinkedList<string>();
    
        // Add initial items
        list.AddLast("A");
        list.AddLast("B"); 
        list.AddLast("C");
        list.AddLast("D");
        list.AddLast("E");
    
        // Create fragmentation
        list.Remove("B");
        list.AddFirst("Z");
        list.Remove("D");
        list.AddLast("F");
        list.RemoveFirst();
    
        // Now we should have [A, C, E, F] with fragmentation
        list.IsDefragmented.Should().BeFalse();
    
        // Act
        list.Defragment();
    
        // Assert - operations still work as expected after defragmentation
        list.IsDefragmented.Should().BeTrue();
        list.ToArray().Should().BeEquivalentTo(["A", "C", "E", "F"], options => options.WithStrictOrdering());
        list.IndexOf("C").Should().Be(1);
    
        list.AddFirst("X");
        list.RemoveLast();
    
        list.ToArray().Should().BeEquivalentTo(["X", "A", "C", "E"], options => options.WithStrictOrdering());
        list.IndexOf("C").Should().Be(2);
    }

}
