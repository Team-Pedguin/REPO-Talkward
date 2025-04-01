using System.Collections;

namespace Talkward;

public partial class SinglyLinkedList<T>
{
    T IReadOnlyList<T>.this[int index] => Ref(index);

    void IList<T>.RemoveAt(int index) => RemoveAt((nuint) index);

    void IList<T>.Insert(int index, T item) => Insert(index, item);

    void ICollection<T>.Add(T item) => AddFirst(item);

    [SuppressMessage("ReSharper", "RedundantOverflowCheckingContext")]
    int IList<T>.IndexOf(T item) => checked((int)IndexOf(item));
    
    // should return an object instead of a struct, otherwise would just => GetEnumerator() 
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        var current = _head;
        while (current != 0)
        {
            yield return _nodes[current - 1].Value;
            current = _nodes[current - 1].NextIndex;
        }
    }

    // should return an object instead of a struct, otherwise would just => GetEnumerator()
    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable<T>) this).GetEnumerator();

    [SuppressMessage("ReSharper", "RedundantOverflowCheckingContext")]
    int ICollection<T>.Count => checked((int) _count);

    [SuppressMessage("ReSharper", "RedundantOverflowCheckingContext")]
    int IReadOnlyCollection<T>.Count => checked((int) Count);
}