namespace Talkward;

public sealed partial class SinglyLinkedList<T>
{
    internal struct Node
    {
        public T Value;

        // 1-based index;
        // will be 0 for no next node (this is the last node or disconnected)
        // will be 1 for the first node
        public nuint NextIndex;

        public Node(T value)
        {
            Value = value;
            NextIndex = 0;
        }

        public unsafe ref Node GetNext(SinglyLinkedList<T> list)
        {
            if (NextIndex == 0)
                return ref Unsafe.NullRef<Node>();
                
            // When the list is defragmented, the NextIndex is simply linearIndex + 1
            // This avoids the need to do an extra array access
            var nodes = list.Nodes;
            ref var next = ref nodes.At(NextIndex - 1);
            return ref next;
        }

        public void SetNext(SinglyLinkedList<T> list, ref Node next)
            => NextIndex = next.GetLinearIndex(list);

        public unsafe nuint GetLinearIndex(SinglyLinkedList<T> list)
        {
            var nodes = list._nodes;
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (nodes is null || nodes.Length == 0) return 0;
            
            // Fast path: if list is defragmented, we can calculate the index directly
            if (list.IsDefragmented)
            {
                // Calculate ordinal position (assuming we're in the active list)
                // This optimization works because in a defragmented list, nodes are ordered
                // sequentially by their position in the logical sequence
                var nodesSpan = list.Nodes;
                nuint i = 0;
                var current = list._head;
                while (current != 0)
                {
                    ref var node = ref nodesSpan.At(current - 1);
                    if (Unsafe.AreSame(ref node, ref this))
                        return current;
                        
                    current = node.NextIndex;
                    i++;
                    
                    // Safeguard against infinite loops (shouldn't happen)
                    if (i > list.Count)
                        break;
                }
            }
            
            // Fallback to original pointer-based calculation
            ref var firstNode = ref nodes[0];
            // ReSharper disable once RedundantOverflowCheckingContext
            var byteOffset = unchecked((nuint) Unsafe.AsPointer(ref firstNode) - (nuint) Unsafe.AsPointer(ref this));
            var index = byteOffset / (nuint) Unsafe.SizeOf<Node>();
            return index < (ulong) nodes.LongLength
                ? index + 1
                : 0;
        }
    }
}

