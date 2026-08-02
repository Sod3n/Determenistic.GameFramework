using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public class DtNode
    {
        public readonly int ptr;

        public Vector3 pos; // Position of the node.
        public Float cost; // Cost from previous node to current node.
        public Float total; // Cost up to the node.
        public int pidx; // Index to parent node.
        public int state; // extra state information. A polyRef can have multiple nodes with different extra info. see DT_MAX_STATES_PER_NODE
        public int flags; // Node flags. A combination of dtNodeFlags.
        public long id; // Polygon ref the node corresponds to.

        public DtNode(int ptr)
        {
            this.ptr = ptr;
        }

        public static int ComparisonNodeTotal(DtNode a, DtNode b)
        {
            int compare = a.total.CompareTo(b.total);
            if (0 != compare)
                return compare;

            return a.ptr.CompareTo(b.ptr);
        }

        public override string ToString()
        {
            return $"Node [ptr={ptr} id={id} cost={cost} total={total}]";
        }
    }
}
