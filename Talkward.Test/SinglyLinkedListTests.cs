using FluentAssertions;

namespace Talkward.Test;

public partial class SinglyLinkedListTests
{
    [Test]
    public void Constructor_WithDefaultCapacity_CreatesEmptyList()
    {
        // Arrange & Act
        var list = new SinglyLinkedList<int>();

        // Assert
        list.Count.Should().Be(0);
        list.Capacity.Should().BeGreaterThanOrEqualTo(16); // Default capacity is 16
    }

    [Test]
    public void Constructor_WithCustomCapacity_SetsInitialCapacity()
    {
        // Arrange & Act
        var list = new SinglyLinkedList<int>(32);

        // Assert
        list.Count.Should().Be(0);
        list.Capacity.Should().BeGreaterThanOrEqualTo(32);
    }

    [Test]
    public void Constructor_WithNegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        FluentActions.Invoking(() => new SinglyLinkedList<int>(-1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void AddFirst_OnEmptyList_AddsItemAsFirst()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act
        list.AddFirst(42);

        // Assert
        list.Count.Should().Be(1);
        list.First.Should().Be(42);
    }

    [Test]
    public void AddFirst_OnNonEmptyList_PrependsList()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddFirst(10);

        // Act
        list.AddFirst(20);
        list.AddFirst(30);

        // Assert
        list.Count.Should().Be(3);
        list.First.Should().Be(30);
        list.ToArray().Should().BeEquivalentTo([30, 20, 10], options => options.WithStrictOrdering());
    }

    [Test]
    public void AddLast_OnEmptyList_AddsItemAsLast()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act
        list.AddLast(42);

        // Assert
        list.Count.Should().Be(1);
        list.First.Should().Be(42);
    }

    [Test]
    public void AddLast_OnNonEmptyList_AppendsToList()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);

        // Act
        list.AddLast(20);
        list.AddLast(30);

        // Assert
        list.Count.Should().Be(3);
        list.First.Should().Be(10);
        list.ToArray().Should().BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void RemoveFirst_OnEmptyList_ReturnsFalse()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act
        var result = list.RemoveFirst();

        // Assert
        result.Should().BeFalse();
        list.Count.Should().Be(0);
    }

    [Test]
    public void RemoveFirst_OnNonEmptyList_RemovesFirstItem()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var result = list.RemoveFirst();

        // Assert
        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list.First.Should().Be(20);
    }

    [Test]
    public void RemoveFirst_WithOutParam_ReturnsRemovedItem()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);

        // Act
        var result = list.RemoveFirst(out var item);

        // Assert
        result.Should().BeTrue();
        item.Should().Be(10);
        list.Count.Should().Be(1);
        list.First.Should().Be(20);
    }

    [Test]
    public void RemoveLast_OnEmptyList_ReturnsFalse()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act
        var result = list.RemoveLast();

        // Assert
        result.Should().BeFalse();
        list.Count.Should().Be(0);
    }

    [Test]
    public void RemoveLast_OnNonEmptyList_RemovesLastItem()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var result = list.RemoveLast();

        // Assert
        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list.ToArray().Should().BeEquivalentTo([10, 20], options => options.WithStrictOrdering());
    }

    [Test]
    public void RemoveLast_WithOutParam_ReturnsRemovedItem()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);

        // Act
        var result = list.RemoveLast(out var item);

        // Assert
        result.Should().BeTrue();
        item.Should().Be(20);
        list.Count.Should().Be(1);
        list.First.Should().Be(10);
    }

    [Test]
    public void First_OnEmptyList_ThrowsInvalidOperationException()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act & Assert
        FluentActions.Invoking(() => _ = list.First)
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The list is empty.");
    }

    [Test]
    public void Clear_EmptiesList()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        list.Clear();

        // Assert
        list.Count.Should().Be(0);
        FluentActions.Invoking(() => _ = list.First)
            .Should().Throw<InvalidOperationException>();
    }


    [Test]
    public void CombinedOperations_MaintainCorrectState()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        var reference = new List<int>();

        // Act - perform a series of mixed operations
        list.AddLast(10);
        reference.Add(10);
        list.AddFirst(5);
        reference.Insert(0, 5);
        list.AddLast(15);
        reference.Add(15);
        list.Remove(10);
        reference.Remove(10);
        list.AddLast(20);
        reference.Add(20);
        list.RemoveFirst();
        reference.RemoveAt(0);
        list.AddFirst(0);
        reference.Insert(0, 0);
        list.Insert(1, 10);
        reference.Insert(1, 10);
        list.RemoveLast();
        reference.RemoveAt(reference.Count - 1);
        list.RemoveAt(1);
        reference.RemoveAt(1);

        // Assert
        list.Count.Should().Be((nuint) reference.Count);
        list.ToArray().Should().BeEquivalentTo(reference, options => options.WithStrictOrdering());
    }

    [Test]
    public void MultipleDefragmentations_MaintainConsistency()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        for (int i = 0; i < 20; i++)
        {
            list.AddLast(i);
        }

        // Act & Assert - multiple cycles of modification and defragmentation
        for (int cycle = 0; cycle < 5; cycle++)
        {
            // Remove some items
            for (int i = cycle; i < 20; i += 5)
            {
                list.Remove(i);
            }

            list.IsDefragmented.Should().BeFalse();
            list.Defragment();
            list.IsDefragmented.Should().BeTrue();

            // Add some new items
            for (int i = 100 + cycle * 10; i < 100 + cycle * 10 + 5; i++)
            {
                list.AddLast(i);
            }
        }

        // Final list should be operational
        list.Count.Should().BeGreaterThan(0);
        list.AddFirst(999);
        list.RemoveLast();
        list.Contains(999).Should().BeTrue();
    }
}