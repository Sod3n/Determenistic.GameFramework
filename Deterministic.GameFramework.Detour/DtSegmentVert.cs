using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public struct DtSegmentVert
    {
        public Vector3 vmin;
        public Vector3 vmax;

        public DtSegmentVert(Float v0, Float v1, Float v2, Float v3, Float v4, Float v5)
        {
            vmin.X = v0;
            vmin.Y = v1;
            vmin.Z = v2;

            vmax.X = v3;
            vmax.Y = v4;
            vmax.Z = v5;
        }
    }
}
