namespace Deterministic.GameFramework.Detour
{
    /// Defines a polygon within a dtMeshTile object.
    /// @ingroup detour
    public class DtPoly
    {
        public readonly int index;

        /// Index to first link in linked list. (Or #DT_NULL_LINK if there is no link.)
        public int firstLink;

        /// The indices of the polygon's vertices.
        /// The actual vertices are located in dtMeshTile::verts.
        public readonly int[] verts;

        /// Packed data representing neighbor polygons references and flags for each edge.
        public readonly int[] neis;

        /// The user defined polygon flags.
        public int flags;

        /// The number of vertices in the polygon.
        public int vertCount;

        /// The bit packed area id and polygon type.
        /// @note Use the structure's set and get methods to access this value.
        public int areaAndtype;

        public DtPoly(int index, int maxVertsPerPoly)
        {
            this.index = index;
            verts = new int[maxVertsPerPoly];
            neis = new int[maxVertsPerPoly];
        }

        /// Sets the user defined area id. [Limit: < #DT_MAX_AREAS]
        public void SetArea(int a)
        {
            areaAndtype = (areaAndtype & 0xc0) | (a & 0x3f);
        }

        /// Sets the polygon type. (See: #dtPolyTypes.)
        public void SetPolyType(int t)
        {
            areaAndtype = (areaAndtype & 0x3f) | (t << 6);
        }

        /// Gets the user defined area id.
        public int GetArea()
        {
            return areaAndtype & 0x3f;
        }

        /// Gets the polygon type. (See: #dtPolyTypes)
        public int GetPolyType()
        {
            return areaAndtype >> 6;
        }
    }
}
