using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    /// Provides information about raycast hit
    /// filled by dtNavMeshQuery::raycast
    /// @ingroup detour
    public ref struct DtRaycastHit
    {
        /// The hit parameter. (FLT_MAX if no wall hit.)
        public Float t;

        /// hitNormal	The normal of the nearest wall hit. [(x, y, z)]
        public Vector3 hitNormal;

        /// The index of the edge on the final polygon where the wall was hit.
        public int hitEdgeIndex;

        /// Pointer to an array of reference ids of the visited polygons. [opt]
        public Span<long> path;

        /// The number of visited polygons. [opt]
        public int pathCount;

        /// The maximum number of polygons the @p path array can hold.
        public int maxPath;

        ///  The cost of the path until hit.
        public Float pathCost;
    }
}
