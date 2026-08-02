using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    public class DtCrowdConfig
    {
        public readonly Float maxAgentRadius;

        public int pathQueueSize = 32; // Max number of path requests in the queue
        public int maxFindPathIterations = 100; // Max number of sliced path finding iterations executed per update (used to handle longer paths and replans)
        public int maxTargetFindPathIterations = 20; // Max number of sliced path finding iterations executed per agent to find the initial path to target
        public Float topologyOptimizationTimeThreshold = 0.5f; // Min time between topology optimizations (in seconds)
        public int checkLookAhead = 10; // The number of polygons from the beginning of the corridor to check to ensure path validity
        public Float targetReplanDelay = 1.0f; // Min time between target re-planning (in seconds)
        public int maxTopologyOptimizationIterations = 32; // Max number of sliced path finding iterations executed per topology optimization per agent
        public Float collisionResolveFactor = 0.7f;
        public int maxObstacleAvoidanceCircles = 6; // Max number of neighbour agents to consider in obstacle avoidance processing
        public int maxObstacleAvoidanceSegments = 8; // Max number of neighbour segments to consider in obstacle avoidance processing

        public DtCrowdConfig(Float maxAgentRadius)
        {
            this.maxAgentRadius = maxAgentRadius;
        }
    }
}
