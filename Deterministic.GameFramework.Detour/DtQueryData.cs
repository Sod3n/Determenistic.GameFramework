using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public struct DtQueryData
    {
        public DtStatus status;
        public DtNode lastBestNode;
        public Float lastBestNodeCost;
        public long startRef;
        public long endRef;
        public Vector3 startPos;
        public Vector3 endPos;
        public IDtQueryFilter filter;
        public int options;
        public Float raycastLimitSqr;
    }
}
