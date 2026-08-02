using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    /** Provides high level information related to a dtMeshTile object. */
    public class DtMeshHeader
    {
        /** Tile magic number. (Used to identify the data format.) */
        public int magic;

        /** Tile data format version number. */
        public int version;

        /** The x-position of the tile within the dtNavMesh tile grid. (x, y, layer) */
        public int x;

        /** The y-position of the tile within the dtNavMesh tile grid. (x, y, layer) */
        public int y;

        /** The layer of the tile within the dtNavMesh tile grid. (x, y, layer) */
        public int layer;

        /** The user defined id of the tile. */
        public int userId;

        /** The number of polygons in the tile. */
        public int polyCount;

        /** The number of vertices in the tile. */
        public int vertCount;

        /** The number of allocated links. */
        public int maxLinkCount;

        /** The number of sub-meshes in the detail mesh. */
        public int detailMeshCount;

        /** The number of unique vertices in the detail mesh. (In addition to the polygon vertices.) */
        public int detailVertCount;

        /** The number of triangles in the detail mesh. */
        public int detailTriCount;

        /** The number of bounding volume nodes. (Zero if bounding volumes are disabled.) */
        public int bvNodeCount;

        /** The number of off-mesh connections. */
        public int offMeshConCount;

        /** The index of the first polygon which is an off-mesh connection. */
        public int offMeshBase;

        /** The height of the agents using the tile. */
        public Float walkableHeight;

        /** The radius of the agents using the tile. */
        public Float walkableRadius;

        /** The maximum climb height of the agents using the tile. */
        public Float walkableClimb;

        /** The minimum bounds of the tile's AABB. [(x, y, z)] */
        public Vector3 bmin = new Vector3();

        /** The maximum bounds of the tile's AABB. [(x, y, z)] */
        public Vector3 bmax = new Vector3();

        /** The bounding volume quantization factor. */
        public Float bvQuantFactor;
    }
}
