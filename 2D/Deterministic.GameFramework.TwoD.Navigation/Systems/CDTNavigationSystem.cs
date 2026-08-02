using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Detour;

using Deterministic.GameFramework.Utils.Logging;
using CDT = Deterministic.GameFramework.CDT;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// CDT-based navigation system with DtCrowd integration.
/// Builds nav meshes using constrained Delaunay triangulation, then delegates
/// pathfinding and inter-agent avoidance to Detour's crowd simulation.
///
/// Per-agent path data is stored in the NavigationAgent2D ECS component
/// (FixedPathBuffer16) so it survives rollback deterministically.
/// </summary>
[UpdateOrder(-100)]
public class CDTNavigationSystem : ISystem
{
    [ThreadStatic] private static List<Entity>? _agentBuffer;
    [ThreadStatic] private static List<Vector2>? _pathScratch;

    private static List<Entity> AgentBuffer => _agentBuffer ??= new();
    private static List<Vector2> PathScratch => _pathScratch ??= new(64);

    public void Update(EntityWorld state)
    {
        var cdtState = state.GetCustomData<CDTNavigationState>();
        if (cdtState == null)
        {
            cdtState = new CDTNavigationState();
            state.SetCustomData(cdtState);
        }

        BakeIfNeeded(state, cdtState);
        ClampCharacterPositions(state, cdtState);
        UpdateAgents(state, cdtState);
    }

    /// <summary>
    /// Predictive move_and_slide for NavMeshConstraint entities. Runs BEFORE velocity
    /// integration: if the next integrated position would be off-mesh, rewrite the
    /// velocity so the integrator lands directly on the navmesh boundary — the entity
    /// never visibly penetrates the obstacle. Per-entity <see cref="NavMeshConstraint.SlideFactor"/>
    /// controls how much of the wall-tangent velocity is preserved.
    /// </summary>
    private static void ClampCharacterPositions(EntityWorld state, CDTNavigationState cdtState)
    {
        if (!cdtState.Map.IsBuilt) return;

        var gameTime = state.GetCustomData<IGameTime>();
        Float dt = gameTime?.FixedDeltaTime ?? (Float)1 / (Float)60;

        foreach (var entity in state.Filter<NavMeshConstraint, CharacterBody2D, Transform2D>())
        {
            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var body = ref state.GetComponent<CharacterBody2D>(entity);
            var constraint = state.GetComponent<NavMeshConstraint>(entity);
            var pos = transform.Position;

            // ── Already off-mesh (stuck inside an obstacle / spawned on top) ──
            if (cdtState.Map.FindTriangle(pos) < 0)
            {
                if (!constraint.DisablePushOut)
                {
                    var (pushTri, pushNearest) = cdtState.Map.FindClosestTriangle(pos);
                    if (pushTri >= 0)
                        transform.Position = pushNearest;
                }
                continue;
            }

            // ── On-mesh: predict next position with current velocity ──
            var vel = body.Velocity;
            if (vel.X == (Float)0 && vel.Y == (Float)0) continue;

            var predicted = pos + vel * dt;
            if (cdtState.Map.FindTriangle(predicted) >= 0) continue; // safe to integrate

            // Predicted off-mesh — would penetrate the wall this tick.
            if (constraint.DisableSlide)
            {
                // Hard block: kill velocity, integrator will leave pos alone.
                body.Velocity = Vector2.Zero;
                body.RealVelocity = Vector2.Zero;
                continue;
            }

            var (triIdx, nearest) = cdtState.Map.FindClosestTriangle(predicted);
            if (triIdx < 0)
            {
                body.Velocity = Vector2.Zero;
                body.RealVelocity = Vector2.Zero;
                continue;
            }

            // Wall normal points from the predicted (in-obstacle) position toward the
            // closest walkable point. Project vel onto this and remove the into-wall
            // component — what's left is the velocity along the wall tangent.
            var wallNormal = nearest - predicted;
            Float normalLenSq = wallNormal.SqrMagnitude;
            if (normalLenSq <= (Float)0) continue;

            Float velDotNormal = Vector2.Dot(vel, wallNormal);
            if (velDotNormal >= (Float)0) continue; // already moving away from the wall

            var velNormalComponent = (velDotNormal / normalLenSq) * wallNormal;
            var velTangent = vel - velNormalComponent;

            // Angle gate: |tangent|² / |vel|² = cos²(angle between vel and wall).
            // Stop when angle exceeds MaxSlideAngleDegrees (near-perpendicular approach).
            Float velLenSq = vel.SqrMagnitude;
            Float velTangentLenSq = velTangent.SqrMagnitude;
            float maxCos = (float)System.Math.Cos((double)constraint.MaxSlideAngleDegrees * System.Math.PI / 180.0);
            Float maxCosSq = (Float)(maxCos * maxCos);
            if (velTangentLenSq < maxCosSq * velLenSq)
            {
                body.Velocity = Vector2.Zero;
                body.RealVelocity = Vector2.Zero;
                continue;
            }

            Float slide = (Float)constraint.SlideFactor;
            if (slide <= (Float)0) { body.Velocity = Vector2.Zero; body.RealVelocity = Vector2.Zero; continue; }
            if (slide > (Float)1) slide = (Float)1;

            body.Velocity = velTangent * slide;
        }
    }

    // ══════════════════════════════════════════════
    //  Baking
    // ══════════════════════════════════════════════

    private static void BakeIfNeeded(EntityWorld state, CDTNavigationState cdtState)
    {
        Entity worldEntity = Entity.Null;
        foreach (var entity in state.Filter<NavigationWorld2D>())
        {
            worldEntity = entity;
            break;
        }

        if (worldEntity == Entity.Null)
        {
            if (cdtState.Map.IsBuilt)
                state.SetCustomData(new CDTNavigationState());
            return;
        }

        ref var world = ref state.GetComponent<NavigationWorld2D>(worldEntity);

        var gameTime = state.GetCustomData<IGameTime>();
        long currentTick = gameTime?.CurrentTick ?? 0;

        if (world.ForceBake)
        {
            world.ForceBake = false;
            world.DirtyTick = -1;
        }
        else if (world.PhysicsBaked)
        {
            // After state restore, Map is cleared but PhysicsBaked is still true.
            // Force a rebuild so the navmesh and DtCrowd are deterministically recreated.
            if (!cdtState.Map.IsBuilt)
            {
                world.DirtyTick = -1;
                // Fall through to rebuild below
            }
            else if (cdtState.NeedsStaleCheck)
            {
                // After rollback: check if obstacles changed during the rolled-back window.
                // The Map was built with MapObstacleHash. If the restored obstacle state
                // has a different hash, the Map is stale and must be rebuilt.
                cdtState.NeedsStaleCheck = false;
                int restoredHash = ObstaclePolygonExtractor.ComputeObstacleHash(state, ref world);
                if (restoredHash == cdtState.MapObstacleHash)
                {
                    // Obstacles unchanged — Map is still valid, skip expensive rebuild.
                    // But verify the map hash matches ECS (catches divergence after full state sync
                    // where the server's MapHash was restored but our local map differs).
                    int localMapHash = cdtState.Map.ComputeMapHash();
                    if (localMapHash == world.MapHash)
                        return;
                    // Map hash mismatch — fall through to rebuild
                    ILogger.Log($"[Nav] Map hash mismatch after rollback: local={localMapHash:X8} ecs={world.MapHash:X8}, rebuilding");
                }
                world.DirtyTick = -1;
                // Fall through to rebuild below
            }
            else
            {
            int bodyCount = CountObstacleBodies(state, world.ObstacleMask);

            if (bodyCount == world.LastStaticBodyCount)
                return;

            if (world.DirtyTick < 0)
                world.DirtyTick = currentTick;

            long ticksSinceDirty = currentTick - world.DirtyTick;
            if (ticksSinceDirty < CDTNavigationState.RebakeDebounceFrames)
                return;

            int currentHash = ObstaclePolygonExtractor.ComputeObstacleHash(state, ref world);
            if (currentHash == world.LastObstacleHash)
            {
                world.LastStaticBodyCount = bodyCount;
                world.DirtyTick = -1;
                return;
            }
            }
        }

        var (vertices, edges) = ObstaclePolygonExtractor.ExtractObstaclePolygons(state, ref world);
        cdtState.Map.Build(vertices, edges);

        var builtHash = ObstaclePolygonExtractor.ComputeObstacleHash(state, ref world);
        int newMapHash = cdtState.Map.ComputeMapHash();

        // Only clear agent paths if the map geometry actually changed.
        // After state sync, the map is rebuilt from the same obstacles (same MapHash),
        // so agent paths (restored from ECS) are still valid. Clearing them would
        // desync PathPointCount vs the server which never rebuilt.
        if (newMapHash != world.MapHash)
        {
            foreach (var entity in state.Filter<NavigationAgent2D>())
            {
                ref var agent = ref state.GetComponent<NavigationAgent2D>(entity);
                agent.PathPointCount = 0;
                agent.PathIndex = 0;
            }
        }

        world.LastObstacleHash = builtHash;
        cdtState.MapObstacleHash = builtHash;
        world.MapHash = newMapHash;
        world.LastStaticBodyCount = CountObstacleBodies(state, world.ObstacleMask);
        world.DirtyTick = -1;
        world.PhysicsBaked = true;

        ILogger.Log($"[Nav] Navmesh rebuilt: {cdtState.Map.TriangleCount} tris, mapHash={world.MapHash:X8}");
    }

    private static int CountObstacleBodies(EntityWorld state, uint obstacleMask)
    {
        int count = 0;
        foreach (var e in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
        {
            var sb = state.GetComponent<StaticBody2D>(e);
            if ((sb.CollisionLayer & obstacleMask) != 0)
                count++;
        }
        return count;
    }

    private static void UpdateAgents(EntityWorld state, CDTNavigationState cdtState)
    {
        UpdateAgentsImpl(state, cdtState);
    }

    // ══════════════════════════════════════════════
    //  Agent updating (pure CDT pathfinding, no DtCrowd)
    // ══════════════════════════════════════════════

    private static void UpdateAgentsImpl(EntityWorld state, CDTNavigationState cdtState)
    {
        var agentBuffer = AgentBuffer;
        agentBuffer.Clear();
        foreach (var entity in state.Filter<NavigationAgent2D, Transform2D>())
            agentBuffer.Add(entity);
        agentBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in agentBuffer)
        {
            ref var agent = ref state.GetComponent<NavigationAgent2D>(entity);

            if (agent.IsNavigationFinished && agent.TargetPosition == agent.LastTargetPosition)
            {
                agent.Velocity = Vector2.Zero;
                continue;
            }

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            var currentPos = transform.GlobalPosition;

            agent.IsTargetPositionChanged = agent.TargetPosition != agent.LastTargetPosition;
            bool needsPath = agent.IsTargetPositionChanged
                || (agent.PathPointCount == 0 && !agent.IsNavigationFinished
                    && agent.TargetPosition != Vector2.Zero);

            if (needsPath)
            {
                agent.LastTargetPosition = agent.TargetPosition;
                ComputePath(ref agent, currentPos, cdtState.Map, agent.Radius);

                if (!agent.IsTargetPositionChanged)
                {
                    if (agent.PathIndex >= agent.PathPointCount)
                        agent.PathIndex = agent.PathPointCount > 0 ? (byte)(agent.PathPointCount - 1) : (byte)0;
                }
            }

            var distToActualTarget = Vector2.Distance(currentPos, agent.TargetPosition);
            if (distToActualTarget <= agent.TargetDesiredDistance)
            {
                agent.IsNavigationFinished = true;
                agent.Velocity = Vector2.Zero;
                agent.NextPathPosition = agent.TargetPosition;
                agent.DistanceToTarget = (Float)0;
                agent.PathIndex = 0;
                agent.PathPointCount = 0;
                continue;
            }

            if (agent.PathPointCount > 0 && agent.PathIndex < agent.PathPointCount)
            {
                var nextPoint = agent.PathPoints[agent.PathIndex];
                var distToWaypoint = Vector2.Distance(currentPos, nextPoint);

                if (distToWaypoint <= agent.PathDesiredDistance)
                {
                    if (agent.PathIndex == agent.PathPointCount - 1)
                    {
                        agent.PathPointCount = 1;
                        agent.PathPoints[0] = agent.TargetPosition;
                        agent.PathIndex = 0;
                    }
                    else
                    {
                        agent.PathIndex++;
                    }
                }

                if (agent.PathIndex < agent.PathPointCount)
                {
                    nextPoint = agent.PathPoints[agent.PathIndex];
                    agent.NextPathPosition = nextPoint;
                    var direction = nextPoint - currentPos;
                    var dist = direction.Magnitude;
                    agent.DistanceToTarget = distToActualTarget;
                    if (dist > Float.Epsilon)
                        agent.Velocity = direction / dist * agent.MaxSpeed;
                    else
                        agent.Velocity = Vector2.Zero;
                    agent.IsNavigationFinished = false;
                }
            }
            else
            {
                agent.Velocity = Vector2.Zero;
                agent.IsNavigationFinished = true;
            }
        }
    }

    private static void ComputePath(ref NavigationAgent2D agent,
        Vector2 currentPos, CDTNavigationMap map, Float agentRadius)
    {
        agent.PathIndex = 0;
        agent.PathPointCount = 0;
        agent.IsNavigationFinished = false;
        agent.IsTargetReachable = false;

        if (!map.IsBuilt) return;

        var scratch = PathScratch;
        scratch.Clear();

        bool found = map.ComputeSmoothedPath(currentPos, agent.TargetPosition, scratch, agentRadius);
        if (found)
        {
            // Copy scratch list into fixed buffer (truncate if > 16)
            int count = Math.Min(scratch.Count, FixedPathBuffer16.Capacity);
            for (int i = 0; i < count; i++)
                agent.PathPoints[i] = scratch[i];
            agent.PathPointCount = (byte)count;
            agent.IsTargetReachable = true;
            agent.IsNavigationFinished = false;
        }
    }
}
