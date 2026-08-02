using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    /// Configuration parameters used to define multi-tile navigation meshes.
    /// The values are used to allocate space during the initialization of a navigation mesh.
    /// @see dtNavMesh::init()
    /// @ingroup detour
    public struct DtNavMeshParams
    {
        public Vector3 orig; //< The world space origin of the navigation mesh's tile space. [(x, y, z)]
        public Float tileWidth; //< The width of each tile. (Along the x-axis.)
        public Float tileHeight; //< The height of each tile. (Along the z-axis.)
        public int maxTiles; //< The maximum number of tiles the navigation mesh can contain.
        public int maxPolys; //< The maximum number of polygons each tile can contain.
    }
}
