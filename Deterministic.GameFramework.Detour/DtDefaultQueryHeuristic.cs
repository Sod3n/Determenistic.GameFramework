using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public class DtDefaultQueryHeuristic : IDtQueryHeuristic
    {
        public static readonly Float H_SCALE = 0.999f; // Search heuristic scale.
        public static readonly DtDefaultQueryHeuristic Default = new DtDefaultQueryHeuristic(H_SCALE);

        private readonly Float scale;

        public DtDefaultQueryHeuristic(Float scale)
        {
            this.scale = scale;
        }

        public Float GetCost(Vector3 neighbourPos, Vector3 endPos)
        {
            return Vector3.Distance(neighbourPos, endPos) * scale;
        }
    }
}
