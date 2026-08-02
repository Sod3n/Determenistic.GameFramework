namespace Deterministic.GameFramework.Detour
{
    public class DtNodeQueue
    {
        private readonly DtSortedQueue<DtNode> m_heap;

        public DtNodeQueue()
        {
            m_heap = new DtSortedQueue<DtNode>(DtNode.ComparisonNodeTotal);
        }

        public int Count()
        {
            return m_heap.Count();
        }

        public void Clear()
        {
            m_heap.Clear();
        }

        public DtNode Peek()
        {
            return m_heap.Peek();
        }

        public DtNode Pop()
        {
            return m_heap.Dequeue();
        }

        public void Push(DtNode node)
        {
            m_heap.Enqueue(node);
        }

        public void Modify(DtNode node)
        {
            m_heap.Remove(node);
            Push(node);
        }

        public bool IsEmpty()
        {
            return m_heap.IsEmpty();
        }
    }
}
