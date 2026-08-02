using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public class DtQueryEmptyFilter : IDtQueryFilter
    {
        public static readonly DtQueryEmptyFilter Shared = new DtQueryEmptyFilter();

        private DtQueryEmptyFilter()
        {
        }

        public bool PassFilter(long refs, DtMeshTile tile, DtPoly poly)
        {
            return false;
        }

        public Float GetCost(Vector3 pa, Vector3 pb, long prevRef, DtMeshTile prevTile, DtPoly prevPoly, long curRef,
            DtMeshTile curTile, DtPoly curPoly, long nextRef, DtMeshTile nextTile, DtPoly nextPoly)
        {
            return 0;
        }
    }
}
