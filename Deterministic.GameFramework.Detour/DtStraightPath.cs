using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public readonly struct DtStraightPath
    {
        /// The local path corridor corners for the agent. (Straight path.) [(x, y, z) * #ncorners]
        public readonly Vector3 pos;

        /// The local path corridor corner flags. (See: #dtStraightPathFlags) [(flags) * #ncorners]
        public readonly byte flags;

        /// The reference id of the polygon being entered at the corner. [(polyRef) * #ncorners]
        public readonly long refs;

        public DtStraightPath(Vector3 pos, byte flags, long refs)
        {
            this.pos = pos;
            this.flags = flags;
            this.refs = refs;
        }
    }
}
