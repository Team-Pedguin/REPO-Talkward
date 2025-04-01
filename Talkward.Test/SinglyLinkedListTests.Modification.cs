using FluentAssertions;

namespace Talkward.Test;

public partial class SinglyLinkedListTests
{
    [Test]
    public void Insert_AtBeginning_InsertsItemAtStart()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(20);
        list.AddLast(30);

        // Act
        list.Insert(0, 10);

        // Assert
        list.Count.Should().Be(3);
        list.ToArray().Should().BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Insert_AtMiddle_InsertsItemAtCorrectPosition()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(30);

        // Act
        list.Insert(1, 20);

        // Assert
        list.Count.Should().Be(3);
        list.ToArray().Should().BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Insert_AtEnd_InsertsItemAtEnd()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);

        // Act
        list.Insert(2, 30);

        // Assert
        list.Count.Should().Be(3);
        list.ToArray().Should().BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Insert_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);

        // Act & Assert
        FluentActions.Invoking(() => list.Insert(-1, 20))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Insert_WithIndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);

        // Act & Assert
        FluentActions.Invoking(() => list.Insert(2, 20))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void RemoveAt_AtBeginning_RemovesFirstItem()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        list.RemoveAt(0);

        // Assert
        list.Count.Should().Be(2);
        list.ToArray().Should().BeEquivalentTo([20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void RemoveAt_AtMiddle_RemovesCorrectItem()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        list.RemoveAt(1);

        // Assert
        list.Count.Should().Be(2);
        list.ToArray().Should().BeEquivalentTo([10, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void RemoveAt_AtEnd_RemovesLastItem()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        list.RemoveAt(2);

        // Assert
        list.Count.Should().Be(2);
        list.ToArray().Should().BeEquivalentTo([10, 20], options => options.WithStrictOrdering());
    }

    [Test]
    public void RemoveAt_WithIndexGreaterThanOrEqualToCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);

        // Act & Assert
        FluentActions.Invoking(() => list.RemoveAt(2))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Remove_WithExistingItem_RemovesItemAndReturnsTrue()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var result = list.Remove(20);

        // Assert
        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list.ToArray().Should().BeEquivalentTo([10, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Remove_WithFirstItem_RemovesItemAndReturnsTrue()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var result = list.Remove(10);

        // Assert
        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list.ToArray().Should().BeEquivalentTo([20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Remove_WithLastItem_RemovesItemAndReturnsTrue()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var result = list.Remove(30);

        // Assert
        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list.ToArray().Should().BeEquivalentTo([10, 20], options => options.WithStrictOrdering());
    }

    [Test]
    public void Remove_WithNonExistingItem_ReturnsFalse()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);

        // Act
        var result = list.Remove(30);

        // Assert
        result.Should().BeFalse();
        list.Count.Should().Be(2);
    }

    [Test]
    public void Remove_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act
        var result = list.Remove(10);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Defragment_ReordersNodesContiguously()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
        list.AddLast(40);
        list.AddLast(50);
        
        // Create some fragmentation by removing elements
        list.Remove(20);
        list.Remove(40);

        // Act
        list.Defragment();

        // Assert
        list.Count.Should().Be(3);
        list.ToArray().Should().BeEquivalentTo([10, 30, 50], options => options.WithStrictOrdering());
        
        // Check that internal structure is contiguous
        // (indirectly test by adding new elements and checking they're appended properly)
        list.AddLast(60);
        list.AddLast(70);
        
        list.ToArray().Should().BeEquivalentTo([10, 30, 50, 60, 70], options => options.WithStrictOrdering());
    }
    
    [Test]
    public void Indexer_AllowsDirectModification_OfItems()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
    
        // Act
        list[1] = 25; // Change second item
    
        // Assert
        list.ToArray().Should().BeEquivalentTo([10, 25, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Indexer_AllowsDirectModification_OfItems_WhenNotDefragmented()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
        list.MarkAsFragmented();
    
        // Act
        list[1] = 25; // Change second item
    
        // Assert
        list.ToArray().Should().BeEquivalentTo([10, 25, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Indexer_ThrowsException_WhenIndexOutOfRange()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
    
        // Act & Assert
        FluentActions.Invoking(() => _ = list[1])
            .Should().Throw<IndexOutOfRangeException>();
    
        FluentActions.Invoking(() => list[1] = 20)
            .Should().Throw<IndexOutOfRangeException>();
    
        FluentActions.Invoking(() => _ = list[-1])
            .Should().Throw<IndexOutOfRangeException>();
    }

    [Test]
    public void ReferenceTypes_AreHandledCorrectly()
    {
        // Arrange
        var list = new SinglyLinkedList<string?>();
        list.AddLast("one");
        list.AddLast(null);
        list.AddLast("three");
    
        // Act & Assert
        list.Count.Should().Be(3);
        list.Contains(null).Should().BeTrue();
        list.IndexOf(null).Should().Be(1);
        list.Remove(null).Should().BeTrue();
    
        list.ToArray().Should().BeEquivalentTo(["one", "three"], options => options.WithStrictOrdering());
    }

}
