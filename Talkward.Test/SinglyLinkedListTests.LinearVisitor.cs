using FluentAssertions;

namespace Talkward.Test;

public partial class SinglyLinkedListTests
{
    [Test]
    public void GetLinearVisitor_ReturnsValidVisitor()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);

        // Act
        var visitor = list.GetLinearVisitor();

        // Assert
        visitor.IsValid.Should().BeTrue();
        visitor.LinearOffset.Should().Be(0);
    }

    [Test]
    public void LinearVisitor_MoveNext_TraversesListCorrectly()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
        var visitor = list.GetLinearVisitor();

        // Act & Assert
        visitor.MoveNext().Should().BeTrue();
        visitor.Current.Should().Be(20);  // Now at index 1 (second item)

        visitor.MoveNext().Should().BeTrue();
        visitor.Current.Should().Be(30);  // Now at index 2 (third item)
        
        visitor.MoveNext().Should().BeFalse(); // No more items
    }

    [Test]
    public void LinearVisitor_Current_ReturnsCorrectItemByReference()
    {
        // Arrange
        var list = new SinglyLinkedList<string>();
        list.AddLast("one");
        list.AddLast("two");
        list.AddLast("three");
        var visitor = list.GetLinearVisitor();

        // Act
        visitor.MoveNext(); // Move to first item
        ref var current = ref visitor.Current;
        current = "modified";

        // Assert
        list.ToArray()[1].Should().Be("modified"); // The actual item in the list should be modified
    }

    [Test]
    public void LinearVisitor_IsValid_ReturnsFalseAfterModification()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        var visitor = list.GetLinearVisitor();
        
        // The visitor should be valid initially
        visitor.IsValid.Should().BeTrue();

        // Act - modify the list
        list.AddFirst(30);

        // Assert - visitor should be invalidated
        visitor.IsValid.Should().BeFalse();
    }

    [Test]
    public void LinearVisitor_Reset_ResetsToInitialState()
    {
        // Arrange
        var list = new SinglyLinkedList<int>();
        list.AddLast(10);
        list.AddLast(20);
        list.AddLast(30);
        var visitor = list.GetLinearVisitor();
        
        // Move the visitor ahead
        visitor.MoveNext();
        visitor.MoveNext();
        
        // Act
        visitor.Reset();
        
        // Assert
        visitor.LinearOffset.Should().Be(0);
        visitor.IsValid.Should().BeTrue();
    }
}
