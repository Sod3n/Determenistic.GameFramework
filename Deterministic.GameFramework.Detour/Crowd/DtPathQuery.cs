using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    public class DtPathQuery
    {
        /// Path find start and end location.
        public Vector3 startPos;
        public Vector3 endPos;
        public long startRef;
        public long endRef;

        public readonly DtPathQueryResult result = new DtPathQueryResult();
        public IDtQueryFilter filter; // < TODO: This is potentially dangerous!

        public DtNavMeshQuery navQuery;
    }
}
