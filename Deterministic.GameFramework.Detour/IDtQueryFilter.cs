using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public interface IDtQueryFilter
    {
        bool PassFilter(long refs, DtMeshTile tile, DtPoly poly);

        Float GetCost(Vector3 pa, Vector3 pb, long prevRef, DtMeshTile prevTile, DtPoly prevPoly, long curRef, DtMeshTile curTile,
            DtPoly curPoly, long nextRef, DtMeshTile nextTile, DtPoly nextPoly);
    }
}
