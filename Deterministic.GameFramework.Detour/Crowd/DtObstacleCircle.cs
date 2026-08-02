using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    /// < Max number of adaptive rings.
    public class DtObstacleCircle
    {
        /** Position of the obstacle */
        public Vector3 p;

        /** Velocity of the obstacle */
        public Vector3 vel;

        /** Velocity of the obstacle */
        public Vector3 dvel;

        /** Radius of the obstacle */
        public Float rad;

        /** Use for side selection during sampling. */
        public Vector3 dp;

        /** Use for side selection during sampling. */
        public Vector3 np;
    }
}
