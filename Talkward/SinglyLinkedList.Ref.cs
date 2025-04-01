namespace Talkward;

public partial class SinglyLinkedList<T>
{
    [ThreadStatic] // FFS DO NOT MAKE THIS NULLABLE
    private static Enumerator _tlsEnumerator;

#pragma warning disable CS9083 // A member is returned by reference but was initialized to a value that cannot be returned by reference
#pragma warning disable CS9092 // This returns a member of local by reference but it is not a ref local
    [SuppressMessage("ReSharper", "CognitiveComplexity")]
    private unsafe ref T Ref(nint ordinal)
    {
        // bounds check hack
        var count = Count;
        if ((nuint) ordinal >= count)
            IndexOutOfRange();

        if (IsDefragmented)
        {
            var nodes = Nodes;
            ref var node = ref nodes.At((nuint) ordinal);
            return ref node.Value;
        }

        if (ordinal == 0)
        {
            if (_head == 0) IndexOutOfRange();
            var nodes = Nodes;
            return ref nodes.At(_head - 1).Value;
        }

        if ((nuint) ordinal == count - 1)
        {
            if (_tail == 0) IndexOutOfRange();
            var nodes = Nodes;
            return ref nodes.At(_tail - 1).Value;
        }

        ref var tlsEnumerator = ref _tlsEnumerator;
        if (tlsEnumerator.List != this || !tlsEnumerator.IsValid)
        {
            _tlsEnumerator = new Enumerator(this);
            if (!tlsEnumerator.MoveNext()) IndexOutOfRange();
        }

        var currentOrdinal = tlsEnumerator.Ordinal;

        if (currentOrdinal == ordinal)
            return ref tlsEnumerator.Current;

        if (currentOrdinal > ordinal || currentOrdinal == -1)
        {
            tlsEnumerator.Reset();
            if (!tlsEnumerator.MoveNext()) IndexOutOfRange();
            currentOrdinal = 0;
        }

        while (currentOrdinal < ordinal)
        {
            if (!tlsEnumerator.MoveNext()) IndexOutOfRange();
            currentOrdinal++;
        }

        return ref tlsEnumerator.Current;
    }
#pragma warning restore CS9092 // This returns a member of local by reference but it is not a ref local
#pragma warning restore CS9083
}