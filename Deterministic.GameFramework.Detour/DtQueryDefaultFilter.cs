using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    using static DtDetour;

    public class DtQueryDefaultFilter : IDtQueryFilter
    {
        private readonly Float[] m_areaCost = new Float[DT_MAX_AREAS]; //< Cost per area type. (Used by default implementation.)
        private int m_includeFlags; //< Flags for polygons that can be visited. (Used by default implementation.)
        private int m_excludeFlags; //< Flags for polygons that should not be visited. (Used by default implementation.)

        public DtQueryDefaultFilter()
        {
            m_includeFlags = 0xffff;
            m_excludeFlags = 0;
            for (int i = 0; i < DT_MAX_AREAS; ++i)
            {
                m_areaCost[i] = 1.0f;
            }
        }

        public DtQueryDefaultFilter(int includeFlags, int excludeFlags, Float[] areaCost)
        {
            m_includeFlags = includeFlags;
            m_excludeFlags = excludeFlags;
            for (int i = 0; i < Math.Min(DT_MAX_AREAS, areaCost.Length); ++i)
            {
                m_areaCost[i] = areaCost[i];
            }

            for (int i = areaCost.Length; i < DT_MAX_AREAS; ++i)
            {
                m_areaCost[i] = 1.0f;
            }
        }

        public bool PassFilter(long refs, DtMeshTile tile, DtPoly poly)
        {
            return (poly.flags & m_includeFlags) != 0 && (poly.flags & m_excludeFlags) == 0;
        }

        public Float GetCost(Vector3 pa, Vector3 pb, long prevRef, DtMeshTile prevTile, DtPoly prevPoly, long curRef,
            DtMeshTile curTile, DtPoly curPoly, long nextRef, DtMeshTile nextTile, DtPoly nextPoly)
        {
            return Vector3.Distance(pa, pb) * m_areaCost[curPoly.GetArea()];
        }

        public int GetIncludeFlags()
        {
            return m_includeFlags;
        }

        public void SetIncludeFlags(int flags)
        {
            m_includeFlags = flags;
        }

        public int GetExcludeFlags()
        {
            return m_excludeFlags;
        }

        public void SetExcludeFlags(int flags)
        {
            m_excludeFlags = flags;
        }
    }
}
