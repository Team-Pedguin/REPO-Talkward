using Unity.VisualScripting;

namespace Talkward;

public sealed partial class SinglyLinkedList<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator()
        => new(this);
    
    [PublicAPI]
    public struct Enumerator
    {
        private readonly SinglyLinkedList<T> _list;
        private long _version;
        private nuint _linearIndex; // 1-based index into the node array
        private nint _ordinal;      // Position in the sequence (0-based)

        public Enumerator(SinglyLinkedList<T> list)
        {
            _list = list;
            _version = list._version;
            _linearIndex = 0;
            _ordinal = -1;
        }

        public readonly ref T Current
        {
            get
            {
                if (_version != _list._version)
                    EnumeratorInvalidated();
                if ((nuint)_ordinal > _list.Count)
                    InvalidEnumeratorPosition();
                return ref _list._nodes[_linearIndex - 1].Value;
            }
        }

        public readonly nuint LinearIndex => _linearIndex;
        public readonly nint Ordinal => _ordinal;

        internal readonly ref Node Node
        {
            get
            {
                if (_version != _list._version)
                    EnumeratorInvalidated();
                if (_linearIndex > _list.Count)
                    InvalidEnumeratorPosition();
                return ref _list._nodes[_linearIndex - 1];
            }
        }

        public bool MoveNext()
        {
            if (_version != _list._version)
                return false;

            if (_linearIndex == 0)
            {
                _linearIndex = _list._head;
                _ordinal = 0;
                return 0 < _list.Count;
            }

            var nextIndex = _list._nodes[_linearIndex - 1].NextIndex;
            if (nextIndex == 0)
                return false;

            _linearIndex = nextIndex;
            _ordinal++;
            return true;
        }

        public void Reset()
        {
            _linearIndex = 0;
            _ordinal = -1;
            _version = _list._version;
        }

        public readonly bool IsValid
            => _list != null && _version == _list._version;

        public readonly SinglyLinkedList<T> List => _list;
    }
}