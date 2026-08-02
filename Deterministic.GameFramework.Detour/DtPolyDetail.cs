namespace Deterministic.GameFramework.Detour
{
    /// Defines the location of detail sub-mesh data within a dtMeshTile.
    public readonly struct DtPolyDetail
    {
        public readonly int vertBase; //< The offset of the vertices in the dtMeshTile::detailVerts array.
        public readonly int triBase; //< The offset of the triangles in the dtMeshTile::detailTris array.
        public readonly byte vertCount; //< The number of vertices in the sub-mesh.
        public readonly byte triCount; //< The number of triangles in the sub-mesh.

        public DtPolyDetail(int vertBase, int triBase, byte vertCount, byte triCount)
        {
            this.vertBase = vertBase;
            this.triBase = triBase;
            this.vertCount = vertCount;
            this.triCount = triCount;
        }
    }
}
