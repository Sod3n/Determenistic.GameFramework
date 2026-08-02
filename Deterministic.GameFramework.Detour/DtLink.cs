namespace Deterministic.GameFramework.Detour
{
    /// Defines a link between polygons.
    /// @note This structure is rarely if ever used by the end user.
    /// @see dtMeshTile
    public class DtLink
    {
        public long refs; //< Neighbour reference. (The neighbor that is linked to.)
        public int next; //< Index of the next link.
        public byte edge; //< Index of the polygon edge that owns this link.
        public byte side; //< If a boundary link, defines on which side the link is.
        public byte bmin; //< If a boundary link, defines the minimum sub-edge area.
        public byte bmax; //< If a boundary link, defines the maximum sub-edge area.
    }
}
