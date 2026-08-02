namespace Deterministic.GameFramework.Detour
{
    /// Bounding volume node.
    /// @note This structure is rarely if ever used by the end user.
    /// @see dtMeshTile
    public class DtBVNode
    {
        public DtVec3i bmin; //< Minimum bounds of the node's AABB. [(x, y, z)]
        public DtVec3i bmax; //< Maximum bounds of the node's AABB. [(x, y, z)]
        public int i; //< The node's index. (Negative for escape sequence.)
    }
}
