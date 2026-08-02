using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    /// Configuration parameters for a crowd agent.
    /// @ingroup crowd
    public class DtCrowdAgentParams
    {
        public Float radius; // < Agent radius. [Limit: >= 0]
        public Float height; // < Agent height. [Limit: > 0]
        public Float maxAcceleration; // < Maximum allowed acceleration. [Limit: >= 0]
        public Float maxSpeed; // < Maximum allowed speed. [Limit: >= 0]

        /// Defines how close a collision element must be before it is considered for steering behaviors. [Limits: > 0]
        public Float collisionQueryRange;

        public Float pathOptimizationRange; // < The path visibility optimization range. [Limit: > 0]

        /// How aggresive the agent manager should be at avoiding collisions with this agent. [Limit: >= 0]
        public Float separationWeight;

        /// the agent path.
        /// Flags that impact steering behavior. (See: #UpdateFlags)
        public int updateFlags;

        /// The index of the avoidance configuration to use for the agent.
        /// [Limits: 0 <= value < #DT_CROWD_MAX_OBSTAVOIDANCE_PARAMS]
        public int obstacleAvoidanceType;

        /// The index of the query filter used by this agent.
        public int queryFilterType;

        /// User defined data attached to the agent.
        public object userData;
    }
}
