using System.Collections;

namespace Talkward;

/// <summary>
/// A circular queue implementation.
/// This is a fixed-size queue that can optionally overwrite the oldest elements when reaching capacity.
/// </summary>
/// <typeparam name="T">The type of elements in the queue.</typeparam>
[PublicAPI]
public class CircularQueue<T>
    : IReadOnlyList<T>, ICollection<T>
{
    // NOTE: unchecked((uint) index) > size
    // is equiv to (index < 0 || index >= size),
    // a bounds check hack with no tradeoffs

    // Buffer for the queue
    private T[] _buffer;

    // Position of first element
    private int _head;

    // Current number of elements
    private int _size;

    // Changes when the queue is modified
    private volatile int _version;

    private static readonly bool IsValueType = typeof(T).IsValueType;

    /// <summary>
    /// Creates a new instance of the <see cref="CircularQueue{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity"></param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacity"/> is less than 0.</exception>
    public CircularQueue(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity),
                "Capacity cannot be less than 0.");

        _buffer = new T[capacity];
        _head = 0;
        _size = 0;
    }

    void ICollection<T>.Add(T item)
    {
        if (!TryEnqueue(item))
            throw new InvalidOperationException("Collection is full.");
    }

    /// <summary>
    /// Clears the queue.
    /// </summary>
    public void Clear()
    {
        Interlocked.Increment(ref _version);

        // Only clear elements that contain actual data
        if (_size > 0)
        {
            if (_head + _size <= _buffer.Length)
            {
                // Data is contiguous
                _buffer.AsSpan(_head, _size)
                    .Clear();
            }
            else
            {
                // Data wraps around
                var firstPartSize = _buffer.Length - _head;
                _buffer.AsSpan(_head, firstPartSize)
                    .Clear();
                _buffer.AsSpan(0, _size - firstPartSize)
                    .Clear();
            }
        }

        _head = 0;
        _size = 0;
    }

    /// <summary>
    /// Checks if the queue contains the specified item.
    /// </summary>
    /// <param name="item">The item to check for.</param>
    /// <param name="equalityComparer">The equality comparer to use.</param>
    /// <returns><see langref="true"/> if the item is found; <see langref="false"/> otherwise.</returns>
    public bool Contains(T item, IEqualityComparer<T> equalityComparer)
    {
        for (var i = 0; i < _size; i++)
        {
            var circularIndex = (_head + i) % _buffer.Length;
            if (equalityComparer.Equals(item, _buffer[circularIndex]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the queue contains the specified item.
    /// </summary>
    /// <remarks>
    /// Note that this implementation deviates from the standard
    /// equality check for reference types and instead just
    /// checks for reference equality.
    ///
    /// Use <see cref="Contains(T,System.Collections.Generic.IEqualityComparer{T})"/>
    /// if you need the standard equality check for reference types,
    /// or use the <see cref="ICollection{T}.Contains(T)"/> interface
    /// implementation.
    /// </remarks>
    /// <param name="item">The item to check for.</param>
    /// <returns><see langref="true"/> if the item is found; <see langref="false"/> otherwise.</returns>
    public bool Contains(T item)
    {
        if (IsValueType)
            return Contains(item, EqualityComparer<T>.Default);

        for (var i = 0; i < _size; i++)
        {
            var circularIndex = (_head + i) % _buffer.Length;
            if (ReferenceEquals(item, _buffer[circularIndex]))
                return true;
        }

        return false;
    }

    bool ICollection<T>.Contains(T item)
        => Contains(item, EqualityComparer<T>.Default);

    /// <summary>
    /// Copies the elements of the queue to an array, starting at the specified index.
    /// </summary>
    /// <param name="array">The array to copy the elements to.</param>
    /// <param name="arrayIndex">The index in the array at which to start copying.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="array"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown <paramref name="arrayIndex"/> is less than 0 or greater than the length of the array.</exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));

        var index = arrayIndex + _size;
        if (arrayIndex < 0 || unchecked((uint) index) >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex),
                "Array index is out of range.");

        CopyInternal(new Span<T>(array, arrayIndex, _size));
    }

    /// <summary>
    /// Copies the elements of the queue to a span.
    /// </summary>
    /// <param name="buffer">The span to copy the elements to.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the length of the span is less than the size of the queue.</exception>
    public void CopyTo(Span<T> buffer)
    {
        if (buffer.Length < _size)
            throw new ArgumentOutOfRangeException(nameof(buffer),
                "Buffer is too small.");

        CopyInternal(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopyInternal(Span<T> buffer)
    {
        if (_size == 0)
            return;

        if (_head + _size <= _buffer.Length)
        {
            // Data is contiguous
            _buffer.AsSpan(_head, _size)
                .CopyTo(buffer);
        }
        else
        {
            // Data wraps around
            var firstSegSize = _buffer.Length - _head;
            _buffer.AsSpan(_head, firstSegSize)
                .CopyTo(buffer);
            _buffer.AsSpan(0, _size - firstSegSize)
                .CopyTo(buffer.Slice(firstSegSize));
        }
    }

    /// <summary>
    /// Tries to remove the specified item from the queue.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <param name="equalityComparer">The equality comparer to use.</param>
    /// <returns><see langref="true"/> if the item was removed; <see langref="false"/> if the item was not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="item"/> is null.</exception>
    public bool TryRemove(T item, IEqualityComparer<T> equalityComparer)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        for (var i = 0; i < _size; i++)
        {
            var circularIndex = (_head + i) % _buffer.Length;
            if (!equalityComparer.Equals(item, _buffer[circularIndex]))
                continue;

            RemoveAtInternal(i);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to remove the specified item from the queue.
    /// </summary>
    /// <param name="item">The item to remove.</param>
    /// <returns><see langref="true"/> if the item was removed; <see langref="false"/> if the item was not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="item"/> is null.</exception>
    public bool TryRemove(T item)
    {
        if (IsValueType)
            return TryRemove(item, EqualityComparer<T>.Default);

        if (item == null)
            throw new ArgumentNullException(nameof(item));

        for (var i = 0; i < _size; i++)
        {
            var circularIndex = (_head + i) % _buffer.Length;
            if (!ReferenceEquals(item, _buffer[circularIndex]))
                continue;

            RemoveAtInternal(i);

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveAtInternal(int index)
    {
        Interlocked.Increment(ref _version);

        // Special case: removing from the beginning is just a dequeue operation
        if (index == 0)
        {
            _buffer[_head] = default!;
            _head = (_head + 1) % _buffer.Length;
            --_size;
            return;
        }

        // Special case: removing from the end just requires clearing last element
        if (index == _size - 1)
        {
            _buffer[(_head + _size - 1) % _buffer.Length] = default!;
            --_size;
            return;
        }

        // General case: shift elements
        for (var i = index; i < _size - 1; i++)
        {
            var currentIdx = (_head + i) % _buffer.Length;
            var nextIdx = (_head + i + 1) % _buffer.Length;
            _buffer[currentIdx] = _buffer[nextIdx];
        }

        // Clear the last element
        _buffer[(_head + _size - 1) % _buffer.Length] = default!;
        --_size;
    }

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    /// <param name="index">The index of the item to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the index is out of range.</exception>
    public void RemoveAt(int index)
    {
        if (unchecked((uint) index) >= _size)
            throw new ArgumentOutOfRangeException(nameof(index),
                "Index is out of range.");

        RemoveAtInternal(index);
    }

    /// <summary>
    /// Tries to remove the item at the specified index.
    /// </summary>
    /// <param name="index">The index of the item to remove.</param>
    /// <returns><see langref="true"/> if the item was removed; <see langref="false"/> if the index is out of range.</returns>
    public bool TryRemoveAt(int index)
    {
        if (unchecked((uint) index) >= _size)
            return false;

        RemoveAtInternal(index);
        return true;
    }

    bool ICollection<T>.Remove(T item)
        => TryRemove(item);

    /// <summary>
    /// The number of elements in the queue.
    /// </summary>
    public int Count => _size;

    /// <summary>
    /// Indicates whether the queue is read-only.
    /// </summary>
    /// <remarks>
    /// Always returns <see langref="false"/>.
    /// </remarks>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets the current size of the queue.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Ensures the capacity of the queue.
    /// </summary>
    /// <param name="capacity">The new capacity.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the new capacity is less than the current size.</exception>
    public void EnsureCapacity(int capacity)
    {
        if (capacity < _size)
            throw new ArgumentOutOfRangeException(nameof(capacity),
                "New capacity cannot be less than the current size.");

        if (capacity == _buffer.Length)
            return;

        Interlocked.Increment(ref _version);

        var newBuffer = new T[capacity];

        CopyInternal(newBuffer);

        _buffer = newBuffer;
        _head = 0;
    }
    
    /// <summary>
    /// Sets the capacity to the actual number of elements in the queue.
    /// </summary>
    public void TrimExcess()
    {
        if (_size == _buffer.Length || _size == 0)
            return;
        
        EnsureCapacity(_size);
    }
    
    /// <summary>
    /// Resizes the queue to the specified size by removing the oldest elements
    /// if necessary, and adjusts capacity to match this size.
    /// </summary>
    /// <param name="targetSize">The target size for the queue.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the target size is negative.</exception>
    public void ResizeTo(int targetSize)
    {
        if (targetSize < 0)
            throw new ArgumentOutOfRangeException(nameof(targetSize), 
                "Target size cannot be negative.");
    
        // If target size equals or exceeds current size, just ensure capacity
        if (targetSize >= _size)
        {
            EnsureCapacity(targetSize);
            return;
        }
    
        var elementsToRemove = _size - targetSize;
    
        Interlocked.Increment(ref _version);
    
        // Update head position and size
        _head = (_head + elementsToRemove) % _buffer.Length;
        _size = targetSize;
    
        // Create new buffer with exactly the target size
        var newBuffer = new T[targetSize];
        CopyInternal(newBuffer);
    
        _buffer = newBuffer;
        _head = 0;
    }
    
    /// <summary>
    /// Enqueues an item into the circular queue and dequeues the oldest item.
    /// </summary>
    /// <param name="newItem">The item to enqueue.</param>
    /// <param name="onlyDequeueIfFull">Whether to only dequeue the oldest item if the queue is full.</param>
    /// <returns>The dequeued item, or <see langref="null"/> if the queue was not full.</returns>
    public T? EnqueueAndDequeue(T newItem, bool onlyDequeueIfFull = false)
    {
        if (_size < _buffer.Length)
        {
            Interlocked.Increment(ref _version);
            var insertPosition = (_head + _size) % _buffer.Length;
            _buffer[insertPosition] = newItem;
            _size++;
            return default!;
        }

        if (onlyDequeueIfFull)
            return default!;

        Interlocked.Increment(ref _version);
        var dequeuedItem = _buffer[_head];
        _buffer[_head] = newItem;
        _head = (_head + 1) % _buffer.Length;
        return dequeuedItem;
    }

    /// <summary>
    /// Enqueues an item into the circular queue.
    /// If the queue is full, it will overwrite the oldest item if <paramref name="overwrite"/> is <see langref="true"/>.
    /// Otherwise, it will not add the item.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <param name="overwrite">Whether to overwrite the oldest item if the queue is full.</param>
    /// <returns><see langref="true"/> if the item was added; <see langref="false"/> if the queue was full and <paramref name="overwrite"/> was false.</returns>
    public bool TryEnqueue(T item, bool overwrite = true)
    {
        if (_size < _buffer.Length)
        {
            Interlocked.Increment(ref _version);
            var insertPosition = (_head + _size) % _buffer.Length;
            _buffer[insertPosition] = item;
            _size++;
            return true;
        }

        if (!overwrite)
            return false;

        // Overwrite oldest element
        Interlocked.Increment(ref _version);
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        return true;
    }

    /// <summary>
    /// Attempts to enqueue multiple items at once as an atomic operation.
    /// All items are either enqueued or none are enqueued.
    /// </summary>
    /// <param name="items">The items to enqueue.</param>
    /// <param name="overwrite">Whether to overwrite the oldest items if the queue doesn't have enough capacity.</param>
    /// <returns><see langref="true"/> if all items were enqueued; <see langref="false"/> if there wasn't enough space and overwrite was false.</returns>
    public bool TryEnqueue(ReadOnlySpan<T> items, bool overwrite = false)
    {
        if (items.Length == 0)
            return true;
        
        // If items exceed buffer capacity, we can't add them all
        if (items.Length > _buffer.Length)
            return false;
        
        // Check if we need to overwrite
        if (_size + items.Length > _buffer.Length)
        {
            if (!overwrite)
                return false;
            
            // Calculate how many old items need to be overwritten
            var itemsToOverwrite = _size + items.Length - _buffer.Length;
        
            // Adjust head and size
            _head = (_head + itemsToOverwrite) % _buffer.Length;
            _size -= itemsToOverwrite;
        }
    
        Interlocked.Increment(ref _version);
    
        // Copy the items to the buffer
        var insertPosition = (_head + _size) % _buffer.Length;
    
        // If the insertion point doesn't wrap around the buffer
        if (insertPosition + items.Length <= _buffer.Length)
        {
            items.CopyTo(_buffer.AsSpan(insertPosition, items.Length));
        }
        else
        {
            // Need to copy in two parts
            var firstPartLength = _buffer.Length - insertPosition;
            items.Slice(0, firstPartLength)
                .CopyTo(_buffer.AsSpan(insertPosition, firstPartLength));
            items.Slice(firstPartLength)
                .CopyTo(_buffer.AsSpan(0, items.Length - firstPartLength));
        }
    
        _size += items.Length;
        return true;
    }
    
    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <remarks>
    /// This is a reference to the slot which is only valid until the queue is modified.
    /// If used after modification, no guarantees are made about accessing or modifying
    /// the value.
    /// </remarks>
    /// <param name="index">The index of the item to get.</param>
    /// <returns>A reference to the item at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the index is out of range.</exception>
    public ref T ReferenceAt(int index)
    {
        if (unchecked((uint) index) >= _size)
            throw new ArgumentOutOfRangeException(nameof(index),
                "Index is out of range.");

        var circularIndex = (_head + index) % _buffer.Length;
        return ref _buffer[circularIndex];
    }
    
    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    /// <remarks>
    /// Note: Setting an item will not interrupt enumeration.
    /// </remarks>
    /// <param name="index">The index of the item to get.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown if the index is out of range.</exception>
    public T this[int index]
    {
        get
        {
            if (unchecked((uint) index) >= _size)
                throw new IndexOutOfRangeException();

            var circularIndex = (_head + index) % _buffer.Length;
            return _buffer[circularIndex];
        }
        set
        {
            if (unchecked((uint) index) >= _size)
                throw new IndexOutOfRangeException();

            var circularIndex = (_head + index) % _buffer.Length;
            _buffer[circularIndex] = value;
        }
    }

    /// <summary>
    /// Peeks at the first item in the queue without removing it.
    /// </summary>
    /// <param name="item">The first item in the queue.</param>
    /// <returns><see langref="true"/> if the item was retrieved; <see langref="false"/> if the queue is empty.</returns>
    public bool TryPeek(out T item)
    {
        if (_size == 0)
        {
            item = default!;
            return false;
        }

        item = _buffer[_head];
        return true;
    }

    /// <summary>
    /// Dequeues the first item in the queue.
    /// </summary>
    /// <param name="item">The dequeued item.</param>
    /// <returns><see langref="true"/> if the item was dequeued; <see langref="false"/> if the queue is empty.</returns>
    public bool TryDequeue(out T item)
    {
        if (_size == 0)
        {
            item = default!;
            return false;
        }

        Interlocked.Increment(ref _version);
        item = _buffer[_head];
        _head = (_head + 1) % _buffer.Length;
        _size--;
        return true;
    }

    /// <summary>
    /// Enumerates the items in the queue.
    /// </summary>
    /// <returns>An enumerator that iterates through the queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue is modified during enumeration.</exception>
    public IEnumerator<T> GetEnumerator()
    {
        var size = _size;
        if (size == 0) yield break;

        var head = _head;
        var buffer = _buffer;
        var bufferLength = buffer.Length;
        var version = _version;

        yield return buffer[head];

        for (var i = 1; i < size; i++)
        {
            if (version != _version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            yield return buffer[(head + i) % bufferLength];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    /// Creates a new array containing all elements in the queue.
    /// </summary>
    /// <returns>An array containing the elements of the queue.</returns>
    public T[] ToArray()
    {
        var result = new T[_size];
        CopyInternal(result);
        return result;
    }
    
    /// <summary>
    /// Tries to get the newest item in the queue.
    /// </summary>
    /// <param name="item">The newest item in the queue.</param>
    /// <returns><see langref="true"/> if the item was retrieved; <see langref="false"/> if the queue is empty.</returns>
    public bool TryGetNewest(out T item)
    {
        if (_size > 0)
        {
            var newestIndex = (_head + _size - 1) % _buffer.Length;
            item = this[newestIndex];
            return true;
        }

        item = default!;
        return false;
    }
}