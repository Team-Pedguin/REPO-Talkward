using System.Collections;
using Unity.VisualScripting;

namespace Talkward;

/// <summary>
/// A singly linked list implementation that stores nodes in a contiguous array for better memory locality.
/// </summary>
[PublicAPI]
public sealed partial class SinglyLinkedList<T> : IReadOnlyList<T>, IList<T>
{
    private Node[] _nodes;
    private nuint _count;
    private nuint _head; // 1-based index to the first node (0 means empty list)
    private nuint _tail; // 1-based index to the last node (0 means empty list)
    private nuint _freeListHead; // 1-based index to the first free node (0 means no free nodes)
    private Func<nuint, nuint> _growthFunction;
    private long _version;
    private long _lastDefragmentedVersion; // Tracks when the list was last defragmented

    private static bool Is64Bit => Unsafe.SizeOf<nuint>() == 8;
    public nuint Capacity => Is64Bit ? (nuint) _nodes.LongLength : (nuint) _nodes.Length;

    /// <summary>
    /// Gets whether the list is currently in a defragmented state where all nodes are contiguous.
    /// </summary>
    public bool IsDefragmented => Interlocked.CompareExchange(ref _lastDefragmentedVersion, 0, 0) == _version;

    public bool Remove(T item)
    {
        if (_head == 0)
            return false;

        var nodes = Nodes;

        // Special case for head node
        if (EqualityComparer<T>.Default.Equals(nodes.At(_head - 1).Value, item))
        {
            Interlocked.Increment(ref _version);
            RemoveFirstInternal();
            return true;
        }

        // Search through the list
        var current = _head;
        while (nodes.At(current - 1).NextIndex != 0)
        {
            var next = nodes.At(current - 1).NextIndex;
            if (EqualityComparer<T>.Default.Equals(nodes.At(next - 1).Value, item))
            {
                // Found the item, remove this node
                Interlocked.Increment(ref _version);
                nodes.At(current - 1).NextIndex = nodes.At(next - 1).NextIndex;

                // Update tail if we're removing the last node
                if (next == _tail)
                    _tail = current;

                FreeNode(next);
                _count--;
                return true;
            }

            current = next;
        }

        return false;
    }

    public bool IsReadOnly => false;

    public nuint Count => _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="SinglyLinkedList{T}"/> class.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the list.</param>
    /// <param name="growthFunction">The function to determine the growth of the list when it needs to expand.</param>
    public unsafe SinglyLinkedList(int initialCapacity = 16, Func<nuint, nuint>? growthFunction = null)
    {
        if (initialCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));

        _growthFunction = growthFunction ?? DefaultGrowthFunction;
        _nodes = new Node[initialCapacity];
        _count = 0;
        _head = 0;
        _tail = 0;
        // just quickly get some sufficiently high entropy value, sign bit is set to avoid GC scanning false positives
        _version = (-1L << 63) | ((nint) Unsafe.AsPointer(ref _nodes) << 16) ^ ((long) initialCapacity << 24);
        Interlocked.Exchange(ref _lastDefragmentedVersion, _version); // A new list is considered defragmented
        InitializeFreeList();
    }

    private void InitializeFreeList()
    {
        // Initialize free list - chain all nodes together
        _freeListHead = 1; // Start with the first node

        var nodes = Nodes;

        for (nuint i = 0; i < Capacity - 1; i++)
            nodes.At(i).NextIndex = i + 2; // Point to the next node (1-based)

        // Last node points to nothing
        nodes.At(Capacity - 1).NextIndex = 0;
    }

    private nuint DefaultGrowthFunction(nuint capacity)
    {
        return capacity switch
        {
            < 128 => (capacity + 31) / 16 * 16,
            < 1024 => (capacity + 63) / 32 * 32,
            < 8192 => (capacity + 511) / 256 * 256,
            _ => (capacity + 8191) / 4096 * 4096
        };
    }

    private void EnsureCapacity(nuint minCapacity)
    {
        var currentCapacity = Capacity;
        if (currentCapacity >= minCapacity)
            return;
        Interlocked.Increment(ref _version);
        var newCapacity = _growthFunction((nuint) _nodes.LongLength);
        if (newCapacity < minCapacity) newCapacity = minCapacity;
        var newNodes = new Node[newCapacity];
        CopyNodes(currentCapacity, newNodes);

        // Update free list with new nodes
        for (var i = currentCapacity; i < newCapacity - 1; i++)
            newNodes[i].NextIndex = i + 2; // Point to the next node (1-based)

        // Connect the old free list to the new nodes
        if (_freeListHead == 0)
        {
            _freeListHead = currentCapacity + 1;
        }
        else
        {
            var nodes = Nodes;
            var current = _freeListHead - 1;
            while (nodes.At(current).NextIndex != 0)
                current = nodes.At(current).NextIndex - 1;

            newNodes[current].NextIndex = currentCapacity + 1;
        }

        // Last new node points to nothing
        newNodes[newCapacity - 1].NextIndex = 0;

        _nodes = newNodes;
    }

#pragma warning disable CS0652 // Comparison to integral constant is useless; the constant is outside the range of the type
    private void CopyNodes(nuint currentCapacity, Node[] newNodes)
    {
        if (currentCapacity > long.MaxValue)
            throw new OutOfMemoryException("Capacity exceeds maximum implementation size limit.");
        if (currentCapacity > int.MaxValue)
            Array.Copy(_nodes, newNodes, (long) currentCapacity);
        Array.Copy(_nodes, newNodes, (int) currentCapacity);
    }
#pragma warning restore CS0652

    /// <summary>
    /// Reorders the nodes in the list to be contiguous in memory.
    /// Free nodes are moved to the end of the list.
    /// Does not allocate new memory.
    /// </summary>
    public void Defragment()
    {
        if (_count == 0)
        {
            Interlocked.Exchange(ref _lastDefragmentedVersion, _version);
            return;
        }

        // If already defragmented, no need to do anything.
        if (IsDefragmented) return;

        Interlocked.Increment(ref _version);

        var count = _count;
        var capacity = Capacity;
        var nodes = Nodes;

        // Start with the first node - it should be at index 0
        if (_head != 1)
        {
            // Move the head node to position 0 if it's not already there
            var headIndex = _head - 1;
            if (headIndex != 0)
            {
                ref var headNode = ref nodes.At(headIndex);
                ref var firstNode = ref nodes.At(0);
                (headNode, firstNode) = (firstNode, headNode);

                // Update head to point to the new position
                _head = 1;

                // If tail was pointing to the old head position, update it
                if (_tail == headIndex + 1)
                    _tail = 1;
            }
        }

        // Defragment the rest of the list
        var currentIndex = _head; // Start with head (which is now at position 1)
        var expectedPosition = (nuint) 0; // The first node should be at position 0

        while (currentIndex != 0 && expectedPosition < count)
        {
            ref var currentNode = ref nodes.At(currentIndex - 1);

            // If the node is not at its expected position, swap it
            if (currentIndex - 1 != expectedPosition)
            {
                // Swap the node at currentIndex-1 with the node at expectedPosition
                ref var expectedPositionNode = ref nodes.At(expectedPosition);
                (expectedPositionNode, currentNode)
                    = (currentNode, expectedPositionNode);

                // The current node is now at the expected position
                // But we need to update any nodes that point to either of these positions

                // Update the tail if necessary
                if (_tail == currentIndex)
                    _tail = expectedPosition + 1;
                else if (_tail == expectedPosition + 1)
                    _tail = currentIndex;

                // Update the next pointers of all nodes that might be affected
                for (nuint i = 0; i < count; i++)
                {
                    if (nodes.At(i).NextIndex == currentIndex)
                        nodes.At(i).NextIndex = expectedPosition + 1;
                    else if (nodes.At(i).NextIndex == expectedPosition + 1)
                        nodes.At(i).NextIndex = currentIndex;
                }
            }

            // Move to the next node in the logical sequence
            currentIndex = nodes.At(expectedPosition).NextIndex;
            expectedPosition++;
        }

        // Update the logical sequence to match the physical order
        for (nuint i = 0; i < count - 1; i++)
            nodes.At(i).NextIndex = i + 2;

        // Last node in the sequence points to nothing
        if (count > 0)
            nodes.At(count - 1).NextIndex = 0;

        // Update the head to point to the first node
        _head = count > 0 ? (nuint) 1 : 0;

        // Update the tail to point to the last node
        _tail = count > 0 ? count : 0;

        // Rebuild the free list
        _freeListHead = count < capacity ? count + 1 : 0;

        // Chain all remaining nodes into the free list
        for (var i = count; i < capacity - 1; i++)
            nodes.At(i).NextIndex = i + 2;

        // Last free node points to nothing
        if (count < capacity)
            nodes.At(capacity - 1).NextIndex = 0;

        // Mark the list as defragmented
        Interlocked.Exchange(ref _lastDefragmentedVersion, _version);
    }

    private Span<Node> Nodes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_nodes);
    }

    private nuint AllocateNode()
    {
        // technically doesn't need to increment _version unless EnsureCapacity reallocates the backing array, right?
        if (_freeListHead == 0)
            // No free nodes, need to resize
            EnsureCapacity(_count + 1);

        var nodes = Nodes;
        var nodeIndex = _freeListHead - 1;
        _freeListHead = nodes.At(nodeIndex).NextIndex;
        nodes.At(nodeIndex).NextIndex = 0; // Disconnect from free list
        return nodeIndex + 1; // Return 1-based index
    }

    private void FreeNode(nuint linearIndex)
    {
        if (linearIndex == 0)
            return;

        var index = linearIndex - 1;
        var nodes = Nodes;
        nodes.At(index).NextIndex = _freeListHead;
        _freeListHead = linearIndex;
    }

    /// <summary>
    /// Adds an item to the beginning of the list.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void AddFirst(T value)
    {
        Interlocked.Increment(ref _version);
        var newNodeIndex = AllocateNode();
        var nodes = Nodes;
        ref var newNode = ref nodes.At(newNodeIndex - 1);
        newNode.Value = value;
        newNode.NextIndex = _head;
        _head = newNodeIndex;

        // If this is the first node, it's also the last
        if (_tail == 0)
            _tail = newNodeIndex;
        
        if (newNodeIndex == 1)
            Interlocked.Exchange(ref _lastDefragmentedVersion, _version);

        _count++;
    }

    /// <summary>
    /// Adds an item to the end of the list.
    /// </summary>
    /// <param name="value">The value to add.</param>
    public void AddLast(T value)
    {
        var newNodeIndex = AllocateNode();
        var nodes = Nodes;
        ref var newNode = ref nodes.At(newNodeIndex - 1);
        newNode.Value = value;
        newNode.NextIndex = 0;

        if (_head == 0)
        {
            _head = newNodeIndex;
        }
        else
        {
            // Use tail pointer directly instead of traversing the list
            nodes.At(_tail - 1).NextIndex = newNodeIndex;
        }

        // Update tail pointer
        _tail = newNodeIndex;
        _count++;
    }

    /// <summary>
    /// Removes the first item from the list.
    /// </summary>
    /// <returns>True if an item was removed; otherwise, false.</returns>
    public bool RemoveFirst()
    {
        if (_head == 0)
            return false;

        Interlocked.Increment(ref _version);
        RemoveFirstInternal();
        return true;
    }

    /// <summary>
    /// Removes the first item from the list.
    /// </summary>
    /// <returns>True if an item was removed; otherwise, false.</returns>
    public bool RemoveFirst(out T oldItem)
    {
        oldItem = default!;
        if (_head == 0)
            return false;

        Interlocked.Increment(ref _version);
        var nodes = Nodes;
        oldItem = nodes.At(_head - 1).Value;
        RemoveFirstInternal();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveFirstInternal()
    {
        var oldHead = _head;
        var nodes = Nodes;
        _head = nodes.At(_head - 1).NextIndex;

        // If we just removed the last node, update tail
        if (_head == 0)
            _tail = 0;

        FreeNode(oldHead);
        _count--;
    }

    /// <summary>
    /// Removes the last item from the list.
    /// </summary>
    /// <returns>True if an item was removed; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool RemoveLast()
        => RemoveLast(out _);

    /// <summary>
    /// Removes the last item from the list.
    /// </summary>
    /// <returns>True if an item was removed; otherwise, false.</returns>
    public bool RemoveLast(out T oldItem)
    {
        oldItem = default!;
        if (_head == 0)
            return false;

        // If there's only one node, remove it (same as RemoveFirst)
        var nodes = Nodes;
        if (_head == _tail)
        {
            oldItem = nodes.At(_head - 1).Value;
            RemoveFirstInternal();
            _tail = 0;
            return true;
        }

        Interlocked.Increment(ref _version);
        
        // Find the second-to-last node
        var current = _head;
        while (nodes.At(current - 1).NextIndex != _tail)
            current = nodes.At(current - 1).NextIndex;

        // current is now the second-to-last node
        oldItem = nodes.At(_tail - 1).Value;
        nodes.At(current - 1).NextIndex = 0;
        FreeNode(_tail);
        _tail = current; // Update tail to be the second-to-last node
        _count--;
        return true;
    }

    /// <summary>
    /// Gets the first element in the list.
    /// </summary>
    /// <returns>The first element in the list.</returns>
    /// <exception cref="InvalidOperationException">The list is empty.</exception>
    public T First
    {
        get
        {
            if (_head == 0)
                throw new InvalidOperationException("The list is empty.");
            var nodes = Nodes;
            return nodes.At(_head - 1).Value;
        }
    }

    /// <summary>
    /// Clears all items from the list.
    /// </summary>
    public void Clear()
    {
        Interlocked.Increment(ref _version);
        while (_head != 0) RemoveFirstInternal();
        // _tail will be set to 0 by RemoveFirstInternal when we remove the last node

        // A freshly cleared list is considered defragmented
        Interlocked.Exchange(ref _lastDefragmentedVersion, _version);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item)
        => IndexOf(item) >= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item, IEqualityComparer<T> comparer)
        => IndexOf(item, comparer) >= 0;

    public void CopyTo(T[] array, int arrayIndex)
    {
        var e = GetEnumerator();
        while (e.MoveNext())
            array[arrayIndex++] = e.Current;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint IndexOf(T item)
        => IndexOf(item, EqualityComparer<T>.Default);

    public nint IndexOf(T item, IEqualityComparer<T> comparer)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (comparer is null) ArgumentNull(nameof(comparer));

        var nodes = Nodes;

        // When the list is defragmented, we can optimize the search
        if (IsDefragmented)
        {
            for (nuint i = 0; i < _count; i++)
                if (comparer.Equals(nodes.At(i).Value, item))
                    return (nint) i;

            return -1;
        }

        // Standard traversal for non-defragmented lists
        var enumerator = new Enumerator(this);
        while (enumerator.MoveNext())
        {
            if (comparer.Equals(enumerator.Current, item))
                return enumerator.Ordinal;
        }

        return -1;
    }

    public void Insert(nint index, T item)
    {
        var count = Count;
        if ((nuint) index > count)
            ArgumentOutOfRange(nameof(index));

        if (index == 0)
            AddFirst(item);
        else if ((nuint) index == count)
            AddLast(item);
        else
            InsertInternal(index, item);
    }

    private void InsertInternal(nint ordinal, T item)
    {
        var newNodeIndex = AllocateNode();
        var nodes = Nodes;
        ref var newNode = ref nodes.At(newNodeIndex - 1);
        newNode.Value = item;

        // Find the node before the insertion point
        var current = _head;
        for (nint i = 0; i < ordinal - 1; i++)
        {
            current = nodes.At(current - 1).NextIndex;
            if (current == 0) // Should never happen if ordinal is valid
                throw new InvalidOperationException("Invalid ordinal position");
        }

        // Insert after this node
        ref var previousNode = ref nodes.At(current - 1);
        newNode.NextIndex = previousNode.NextIndex;
        previousNode.NextIndex = newNodeIndex;
        _count++;
    }

    public void RemoveAt(nuint index)
    {
        if (index >= Count)
            ArgumentOutOfRange(nameof(index));

        if (index == 0)
        {
            Interlocked.Increment(ref _version);
            RemoveFirstInternal();
            return;
        }

        // Find the node before the one to remove
        var current = _head;
        var nodes = Nodes;
        for (nuint i = 0; i < index - 1; i++)
        {
            current = nodes.At(current - 1).NextIndex;
        }

        // Remove the next node
        var nodeToRemove = nodes.At(current - 1).NextIndex;
        if (nodeToRemove == 0) // This shouldn't happen if index is valid
            return;

        Interlocked.Increment(ref _version);

        // If removing the tail node, update tail
        if (nodeToRemove == _tail)
            _tail = current;

        nodes.At(current - 1).NextIndex
            = nodes.At(nodeToRemove - 1).NextIndex;
        FreeNode(nodeToRemove);
        _count--;
    }

    public T this[int index]
    {
        get => Ref(index);
        set => Ref(index) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveFirstInternal(out T oldItem)
    {
        var nodes = Nodes;
        ref var oldHeadNode = ref nodes.At(_head - 1);
        oldItem = oldHeadNode.Value;
        var oldHead = _head;
        _head = oldHeadNode.NextIndex;
        FreeNode(oldHead);
        _count--;
    }

    private void UnlinkNext(ref Node previousNode)
    {
        var nodeIndex = previousNode.NextIndex;
        if (nodeIndex == 0) return;

        var nodes = Nodes;
        var nextNode = nodes.At(nodeIndex - 1);
        previousNode.NextIndex = nextNode.NextIndex;
        FreeNode(nodeIndex);
    }
}