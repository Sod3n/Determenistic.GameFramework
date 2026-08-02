using System.Collections.Generic;
using System.Linq;

namespace Deterministic.GameFramework.Detour
{
    public class DtNodePool
    {
        private readonly Dictionary<long, List<DtNode>> m_map;

        private int m_nodeCount;
        private readonly List<DtNode> m_nodes;

        public DtNodePool()
        {
            m_map = new Dictionary<long, List<DtNode>>();
            m_nodes = new List<DtNode>();
        }

        public void Clear()
        {
            m_map.Clear();
            m_nodeCount = 0;
        }

        public int GetNodeCount()
        {
            return m_nodeCount;
        }

        public int FindNodes(long id, out List<DtNode> nodes)
        {
            var hasNode = m_map.TryGetValue(id, out nodes);
            if (hasNode)
            {
                return nodes.Count;
            }

            return 0;
        }

        public DtNode FindNode(long id)
        {
            m_map.TryGetValue(id, out var nodes);
            if (nodes != null && 0 != nodes.Count)
            {
                return nodes[0];
            }

            return null;
        }

        public DtNode GetNode(long id, int state)
        {
            m_map.TryGetValue(id, out var nodes);
            if (nodes != null)
            {
                foreach (DtNode node in nodes)
                {
                    if (node.state == state)
                    {
                        return node;
                    }
                }
            }
            else
            {
                nodes = new List<DtNode>();
                m_map.Add(id, nodes);
            }

            return Create(id, state, nodes);
        }

        private DtNode Create(long id, int state, List<DtNode> nodes)
        {
            if (m_nodes.Count <= m_nodeCount)
            {
                var newNode = new DtNode(m_nodeCount);
                m_nodes.Add(newNode);
            }

            int i = m_nodeCount;
            m_nodeCount++;
            var node = m_nodes[i];
            node.pidx = 0;
            node.cost = 0;
            node.total = 0;
            node.id = id;
            node.state = state;
            node.flags = 0;

            nodes.Add(node);
            return node;
        }

        public int GetNodeIdx(DtNode node)
        {
            return node != null
                ? node.ptr + 1
                : 0;
        }

        public DtNode GetNodeAtIdx(int idx)
        {
            return idx != 0
                ? m_nodes[idx - 1]
                : null;
        }

        public DtNode GetNode(long refs)
        {
            return GetNode(refs, 0);
        }

        public IEnumerable<DtNode> AsEnumerable()
        {
            return m_nodes.Take(m_nodeCount);
        }
    }
}
