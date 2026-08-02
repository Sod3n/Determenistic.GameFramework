using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    public class DtSegment
    {
        /** Segment start/end */
        public Vector3[] s = new Vector3[2];

        /** Distance for pruning. */
        public Float d;
    }
}
