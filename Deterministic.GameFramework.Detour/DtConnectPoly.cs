using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public readonly struct DtConnectPoly
    {
        public readonly long refs;
        public readonly Float tmin;
        public readonly Float tmax;

        public DtConnectPoly(long refs, Float tmin, Float tmax)
        {
            this.refs = refs;
            this.tmin = tmin;
            this.tmax = tmax;
        }
    }
}
