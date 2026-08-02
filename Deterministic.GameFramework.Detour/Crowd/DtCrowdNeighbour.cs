using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    /// Provides neighbor data for agents managed by the crowd.
    /// @ingroup crowd
    /// @see dtCrowdAgent::neis, dtCrowd
    public readonly struct DtCrowdNeighbour
    {
        public readonly DtCrowdAgent agent; // < The index of the neighbor in the crowd.
        public readonly Float dist; // < The distance between the current agent and the neighbor.

        public DtCrowdNeighbour(DtCrowdAgent agent, Float dist)
        {
            this.agent = agent;
            this.dist = dist;
        }
    };
}
