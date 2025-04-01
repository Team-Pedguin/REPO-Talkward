using FluentAssertions;
using System.Collections.Generic;

namespace Talkward.Test;

public partial class SinglyLinkedListTests
{
    [Test]
    public void GetEnumerator_EnumeratesItemsInCorrectOrder()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var result = new List<int>();
        foreach (var item in list)
        {
            result.Add(item);
        }

        // Assert
        result.Should().BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void GetEnumerator_OnEmptyList_YieldsNoItems()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act
        var result = new List<int>();
        foreach (var item in list)
        {
            result.Add(item);
        }

        // Assert
        result.Should().BeEmpty();
    }

    [Test]
    public void ExplicitEnumerator_EnumeratesItemsInCorrectOrder()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var result = new List<int>();
        var enumerator = list.GetEnumerator();
        while (enumerator.MoveNext())
        {
            result.Add(enumerator.Current);
        }

        // Assert
        result.Should().BeEquivalentTo([10, 20, 30], options => options.WithStrictOrdering());
    }

    [Test]
    public void Contains_WithExistingItem_ReturnsTrue()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act & Assert
        list.Contains(20).Should().BeTrue();
    }

    [Test]
    public void Contains_WithNonExistingItem_ReturnsFalse()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act & Assert
        list.Contains(50).Should().BeFalse();
    }

    [Test]
    public void Contains_WithEmptyList_ReturnsFalse()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();

        // Act & Assert
        list.Contains(10).Should().BeFalse();
    }

    [Test]
    public void IndexOf_WithExistingItem_ReturnsCorrectIndex()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act & Assert
        list.IndexOf(20).Should().Be(1);
    }

    [Test]
    public void IndexOf_WithNonExistingItem_ReturnsMinusOne()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act & Assert
        list.IndexOf(50).Should().Be(-1);
    }

    [Test]
    public void IndexOf_WithDuplicateItems_ReturnsFirstOccurrence()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(20);
        list.AddLast(30);

        // Act & Assert
        list.IndexOf(20).Should().Be(1);
    }

    [Test]
    public void CopyTo_CopiesAllItemsToArray()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
        var array = new int[5];

        // Act
        list.CopyTo(array, 1);

        // Assert
        array.Should().BeEquivalentTo([0, 10, 20, 30, 0], options => options.WithStrictOrdering());
    }
}
