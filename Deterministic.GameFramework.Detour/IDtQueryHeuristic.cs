using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public interface IDtQueryHeuristic
    {
        Float GetCost(Vector3 neighbourPos, Vector3 endPos);
    }
}
