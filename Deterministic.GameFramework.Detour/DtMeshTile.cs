namespace Deterministic.GameFramework.Detour
{
    using static DtDetour;

    /// Defines a navigation mesh tile.
    /// @ingroup detour
    public class DtMeshTile
    {
        public readonly int index; // DtNavMesh.m_tiles array index
        public int linksFreeList = DT_NULL_LINK; //< Index to the next free link.
        public int salt; //< Counter describing modifications to the tile.
        public DtMeshData data; // The tile data.
        public DtLink[] links; // The tile links. [Size: dtMeshHeader::maxLinkCount]

        public int flags; //< Tile flags. (See: #dtTileFlags)
        public DtMeshTile next; //< The next free tile, or the next tile in the spatial grid.

        public DtMeshTile(int index)
        {
            this.index = index;
        }
    }
}
