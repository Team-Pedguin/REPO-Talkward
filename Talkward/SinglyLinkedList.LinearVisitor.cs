namespace Talkward;

public sealed partial class SinglyLinkedList<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LinearVisitor GetLinearVisitor()
        => new(this);

    [PublicAPI]
    public struct LinearVisitor
    {
        private readonly SinglyLinkedList<T> _list;
        private long _version;
        private nuint _linearOffset; // 0-based offset into node array

        public LinearVisitor(SinglyLinkedList<T> list)
        {
            _list = list;
            _linearOffset = 0;
            _version = list._version;
        }

        public readonly nuint LinearOffset => _linearOffset;

        public readonly ref T Current
        {
            get
            {
                if (_version != _list._version)
                    EnumeratorInvalidated();
                return ref _list._nodes[_linearOffset].Value;
            }
        }

        public bool MoveNext()
        {
            if (_version != _list._version)
                return false;
            if (_linearOffset >= _list.Count)
                return false;
            _linearOffset++;
            return _linearOffset < _list.Count;
        }

        public void Reset()
        {
            _linearOffset = 0;
            _version = _list._version;
        }

        public readonly bool IsValid
            => _version == _list._version && _linearOffset < _list.Count;
    }
}