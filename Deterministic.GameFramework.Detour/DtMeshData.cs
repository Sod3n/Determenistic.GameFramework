using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public class DtMeshData
    {
        public DtMeshHeader header; //< The tile header.
        public DtPoly[] polys; //< The tile polygons. [Size: dtMeshHeader::polyCount]
        public Float[] verts; //< The tile vertices. [(x, y, z) * dtMeshHeader::vertCount]
        public DtPolyDetail[] detailMeshes; //< The tile's detail sub-meshes. [Size: dtMeshHeader::detailMeshCount]

        /// The detail mesh's unique vertices. [(x, y, z) * dtMeshHeader::detailVertCount]
        public Float[] detailVerts;

        /// The detail mesh's triangles. [(vertA, vertB, vertC, triFlags) * dtMeshHeader::detailTriCount].
        /// See dtDetailTriEdgeFlags and dtGetDetailTriEdgeFlags.
        public int[] detailTris;

        /// The tile bounding volume nodes. [Size: dtMeshHeader::bvNodeCount]
        /// (Will be null if bounding volumes are disabled.)
        public DtBVNode[] bvTree;

        public DtOffMeshConnection[] offMeshCons; //< The tile off-mesh connections. [Size: dtMeshHeader::offMeshConCount]
    }
}
