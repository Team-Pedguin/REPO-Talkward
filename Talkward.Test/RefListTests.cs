using FluentAssertions;

namespace Talkward.Test;

public class RefListTests
{
    [Test]
    public void DefaultConstructor_CreatesEmptyList()
    {
        // Arrange & Act
        var list = new RefList<int>();

        // Assert
        list.Count.Should().Be(0);
        list.Capacity.Should().Be(0);
    }

    [Test]
    public void CapacityConstructor_SetsInitialCapacity()
    {
        // Arrange & Act
        var list = new RefList<int>(10);

        // Assert
        list.Count.Should().Be(0);
        list.Capacity.Should().Be(10);
    }

    [Test]
    public void CollectionConstructor_CopiesElements()
    {
        // Arrange
        var source = new[] { 1, 2, 3, 4, 5 };

        // Act
        var list = new RefList<int>(source);

        // Assert
        list.Count.Should().Be(5);
        list.Should().BeEquivalentTo(source);
    }

    [Test]
    public void ListConstructor_CopiesFromExistingList()
    {
        // Arrange
        var sourceList = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var list = new RefList<int>(sourceList);

        // Assert
        list.Count.Should().Be(5);
        list.Should().BeEquivalentTo(sourceList);
    }

    [Test]
    public void IntIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        list[2] = 10;
        ref var element = ref list[2];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void LongIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var element = ref list[2L];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void NIntIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var element = ref list[(nint)2];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void UIntIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var element = ref list[2u];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void ULongIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var element = ref list[2ul];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void UShortIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var element = ref list[(ushort)2];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void ByteIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var element = ref list[(byte)2];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void SByteIndexer_ProvidesRef_CanModifyElement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var element = ref list[(sbyte)2];
        element = 30;
        
        // Assert
        list[2].Should().Be(30);
    }

    [Test]
    public void GetInternalArray_ReturnsUnderlyingArray()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var array = ref list.Array;
        
        // Assert
        array.Should().BeOfType<int[]>();
        array.Length.Should().BeGreaterThanOrEqualTo(list.Count);
        array[0].Should().Be(1);
        array[4].Should().Be(5);
    }

    [Test]
    public void InternalArray_ModificationReflectedInList()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref var array = ref list.Array;
        array[2] = 42;
        
        // Assert
        list[2].Should().Be(42);
    }

    [Test]
    public void ListModification_ReflectedInInternalArray()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        ref var array = ref list.Array;
        
        // Act
        list[2] = 42;
        
        // Assert
        array[2].Should().Be(42);
    }

    [Test]
    public void RefIndexer_AllowsInPlaceIncrement()
    {
        // Arrange
        var list = new RefList<int> { 1, 2, 3, 4, 5 };
        
        // Act
        ref int element = ref list[2];
        element++;
        
        // Assert
        list[2].Should().Be(4);
    }

    [Test]
    public void RefIndexer_WorksWithComplexTypes()
    {
        // Arrange
        var list = new RefList<TestClass> 
        {
            new() { Value = 10 },
            new() { Value = 20 },
            new() { Value = 30 }
        };
        
        // Act
        ref var element = ref list[1];
        element.Value = 25;
        
        // Assert
        list[1].Value.Should().Be(25);
    }

    [Test]
    public void RefIndexer_WorksWithStructs()
    {
        // Arrange
        var list = new RefList<TestStruct> 
        {
            new() { Value = 10 },
            new() { Value = 20 },
            new() { Value = 30 }
        };
        
        // Act
        ref var element = ref list[1];
        element.Value = 25;
        
        // Assert
        list[1].Value.Should().Be(25);
    }

    private class TestClass
    {
        public int Value { get; set; }
    }

    private struct TestStruct
    {
        public int Value { get; set; }
    }
}
