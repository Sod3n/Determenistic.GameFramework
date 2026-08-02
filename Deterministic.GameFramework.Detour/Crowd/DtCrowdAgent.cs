using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour.Crowd
{
    /// Represents an agent managed by a #dtCrowd object.
    /// @ingroup crowd
    public class DtCrowdAgent
    {
        public readonly int idx;

        /// The type of mesh polygon the agent is traversing. (See: #CrowdAgentState)
        public DtCrowdAgentState state;

        /// True if the agent has valid path (targetState == DT_CROWDAGENT_TARGET_VALID) and the path does not lead to the requested position, else false.
        public bool partial;

        /// The path corridor the agent is using.
        public readonly DtPathCorridor corridor;

        /// The local boundary data for the agent.
        public readonly DtLocalBoundary boundary;

        /// Time since the agent's path corridor was optimized.
        public Float topologyOptTime;

        /// The known neighbors of the agent.
        public readonly DtCrowdNeighbour[] neis = new DtCrowdNeighbour[DtCrowdConst.DT_CROWDAGENT_MAX_NEIGHBOURS];

        /// The number of neighbors.
        public int nneis;

        /// The desired speed.
        public Float desiredSpeed;

        public Vector3 npos; // < The current agent position. [(x, y, z)]
        public Vector3 disp; // < A temporary value used to accumulate agent displacement during iterative collision resolution. [(x, y, z)]
        public Vector3 dvel; // < The desired velocity of the agent. Based on the current path, calculated from scratch each frame. [(x, y, z)]
        public Vector3 nvel; // < The desired velocity adjusted by obstacle avoidance, calculated from scratch each frame. [(x, y, z)]
        public Vector3 vel; // < The actual velocity of the agent. The change from nvel -> vel is constrained by max acceleration. [(x, y, z)]

        /// The agent's configuration parameters.
        public DtCrowdAgentParams option;

        /// The local path corridor corners for the agent.
        public DtStraightPath[] corners = new DtStraightPath[DtCrowdConst.DT_CROWDAGENT_MAX_CORNERS];

        /// The number of corners.
        public int ncorners;

        public DtMoveRequestState targetState; // < State of the movement request.
        public long targetRef; // < Target polyref of the movement request.
        public Vector3 targetPos; // < Target position of the movement request (or velocity in case of DT_CROWDAGENT_TARGET_VELOCITY).
        public DtPathQueryResult targetPathQueryResult; // < Path finder query
        public bool targetReplan; // < Flag indicating that the current path is being replanned.
        public Float targetReplanTime; // <Time since the agent's target was replanned.
        public Float targetReplanWaitTime;

        public DtCrowdAgentAnimation animation;

        public DtCrowdAgent(int idx)
        {
            this.idx = idx;
            corridor = new DtPathCorridor();
            boundary = new DtLocalBoundary();
            animation = new DtCrowdAgentAnimation();
        }

        public void Integrate(Float dt)
        {
            // Fake dynamic constraint (acceleration limiting).
            Float maxDelta = option.maxAcceleration * dt;
            Vector3 dv = nvel - vel;
            Float ds = dv.Magnitude;
            if (ds > maxDelta)
                dv = dv * (maxDelta / ds);
            vel = vel + dv;

            // Position integration: DtCrowd moves npos so MoveAgents/HandleCollisions
            // can constrain it. The game syncs entity position → npos each tick
            // (before Update) and reads velocity from vel (after Update).
            if (vel.Magnitude > (Float)0.0001f)
                npos = DtVecHelper.Mad(npos, vel, dt);
            else
                vel = Vector3.Zero;
        }

        public bool OverOffmeshConnection(Float radius)
        {
            if (0 == ncorners)
                return false;

            bool offMeshConnection = ((corners[ncorners - 1].flags
                                       & DtStraightPathFlags.DT_STRAIGHTPATH_OFFMESH_CONNECTION) != 0)
                ? true
                : false;
            if (offMeshConnection)
            {
                Float distSq = DtVecHelper.Dist2DSqr(npos, corners[ncorners - 1].pos);
                if (distSq < radius * radius)
                    return true;
            }

            return false;
        }

        public Float GetDistanceToGoal(Float range)
        {
            if (0 == ncorners)
                return range;

            bool endOfPath = ((corners[ncorners - 1].flags & DtStraightPathFlags.DT_STRAIGHTPATH_END) != 0) ? true : false;
            if (endOfPath)
                return Float.Min(DtVecHelper.Dist2D(npos, corners[ncorners - 1].pos), range);

            return range;
        }

        public Vector3 CalcSmoothSteerDirection()
        {
            Vector3 dir = Vector3.Zero;
            if (0 < ncorners)
            {
                int ip0 = 0;
                int ip1 = Math.Min(1, ncorners - 1);
                var p0 = corners[ip0].pos;
                var p1 = corners[ip1].pos;

                var dir0 = p0 - npos;
                var dir1 = p1 - npos;
                dir0.Y = 0;
                dir1.Y = 0;

                Float len0 = dir0.Magnitude;
                Float len1 = dir1.Magnitude;
                if (len1 > (Float)0.001f)
                    dir1 = dir1 * (1 / len1);

                dir.X = dir0.X - dir1.X * len0 * (Float)0.5f;
                dir.Y = 0;
                dir.Z = dir0.Z - dir1.Z * len0 * (Float)0.5f;
                dir = dir.Normalized;
            }

            return dir;
        }

        public Vector3 CalcStraightSteerDirection()
        {
            Vector3 dir = Vector3.Zero;
            if (0 < ncorners)
            {
                dir = corners[0].pos - npos;
                dir.Y = 0;
                dir = dir.Normalized;
            }

            return dir;
        }

        public void SetTarget(long refs, Vector3 pos)
        {
            targetRef = refs;
            targetPos = pos;
            targetPathQueryResult = null;
            if (targetRef != 0)
            {
                targetState = DtMoveRequestState.DT_CROWDAGENT_TARGET_REQUESTING;
            }
            else
            {
                targetState = DtMoveRequestState.DT_CROWDAGENT_TARGET_FAILED;
            }
        }
    }
}
