using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Navigation system that builds nav meshes and updates agent pathfinding.
///
/// Supports two modes:
/// 1. **Manual regions**: Define NavigationRegion2D polygons for walkable areas
/// 2. **Physics auto-bake**: Add a NavigationWorld2D component to auto-generate
///    the nav mesh from the physics world — all StaticBody2D colliders become
///    obstacles, and agents automatically avoid all collision objects via raycasts
///
/// Both modes can be combined. Physics-baked mesh + manual regions are merged.
///
/// Runs after physics so it can use Rapier for baking and avoidance.
/// </summary>
[UpdateOrder(500)] // After physics (default 0), after transform (-1000)
public class NavigationSystem : ISystem
{
    private const float MERGE_DISTANCE = 1.0f;

    // Scratch buffers (ThreadStatic for safety)
    [ThreadStatic] private static List<Entity>? _agentBuffer;
    [ThreadStatic] private static List<Entity>? _regionBuffer;
    [ThreadStatic] private static Vector2[]? _vertexScratch;
    [ThreadStatic] private static List<int>? _hashBuffer;
    [ThreadStatic] private static HashSet<int>? _regionIdScratch;

    private static List<Entity> AgentBuffer => _agentBuffer ??= new();
    private static List<Entity> RegionBuffer => _regionBuffer ??= new();
    private static Vector2[] VertexScratch => _vertexScratch ??= new Vector2[16];
    private static List<int> HashBuffer => _hashBuffer ??= new();
    private static HashSet<int> RegionIdScratch => _regionIdScratch ??= new();

    public void Update(EntityWorld state)
    {
        var navState = state.GetCustomData<NavigationState>();
        if (navState == null)
        {
            navState = new NavigationState();
            state.SetCustomData(navState);
            // NavigationState is transient (not serialized). A fresh instance has
            // PhysicsBaked=false, which BakeFromPhysicsIfNeeded already checks —
            // no need to set ForceBake on the ECS component. Mutating ECS here
            // would cause desync if client and server create NavigationState on
            // different ticks.
        }

        // Physics-based baking (NavigationWorld2D)
        BakeFromPhysicsIfNeeded(state, navState);

        // Manual region-based mesh
        RebuildNavMeshIfNeeded(state, navState);

        UpdateAgents(state, navState);
    }

    private static void BakeFromPhysicsIfNeeded(EntityWorld state, NavigationState navState)
    {
        // Find NavigationWorld2D (only one should exist)
        Entity worldEntity = Entity.Null;
        foreach (var entity in state.Filter<NavigationWorld2D>())
        {
            worldEntity = entity;
            break;
        }

        if (worldEntity == Entity.Null)
        {
            // No NavigationWorld2D — clear any previous physics bake
            if (navState.HasPhysicsBakedMesh)
            {
                navState.HasPhysicsBakedMesh = false;
                navState.PhysicsBaked = false;
                navState.Chunks.Clear();
                navState.Map.IsDirty = true; // Force full rebuild
            }
            return;
        }

        ref var world = ref state.GetComponent<NavigationWorld2D>(worldEntity);

        if (world.ChunkSize > (Float)0)
        {
            BakeChunkedIfNeeded(state, navState, ref world);
        }
        else
        {
            BakeFullIfNeeded(state, navState, ref world);
        }
    }

    private static void BakeFullIfNeeded(EntityWorld state, NavigationState navState, ref NavigationWorld2D world)
    {
        var gameTime = state.GetCustomData<IGameTime>();
        long currentTick = gameTime?.CurrentTick ?? 0;

        if (world.ForceBake)
        {
            world.ForceBake = false;
            navState.DirtyTick = -1; // bypass debounce
        }
        else if (navState.PhysicsBaked)
        {
            // Cheap pre-check: count obstacle bodies. Only compute full hash if count changed.
            int bodyCount = 0;
            foreach (var e in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
            {
                var sb = state.GetComponent<StaticBody2D>(e);
                if ((sb.CollisionLayer & world.ObstacleMask) != 0)
                    bodyCount++;
            }

            if (bodyCount == navState.LastStaticBodyCount)
                return; // nothing changed, skip expensive hash

            // Count changed — mark dirty and debounce
            if (navState.DirtyTick < 0)
                navState.DirtyTick = currentTick;

            long ticksSinceDirty = currentTick - navState.DirtyTick;
            if (ticksSinceDirty < NavigationState.RebakeDebounceFrames)
                return; // wait for debounce period

            // Debounce elapsed — compute full hash to confirm change
            int currentHash = ComputePhysicsBakeHash(state, ref world);
            if (currentHash == navState.PhysicsBakeHash)
            {
                navState.LastStaticBodyCount = bodyCount;
                navState.DirtyTick = -1;
                return; // false positive (count changed but hash didn't — e.g. entity replaced)
            }
        }

        // Full rebuild: clear map, bake from physics, then regions will add on top
        navState.Map.Clear();
        navState.Map.RegionCosts.Clear();
        navState.AgentPaths.Clear();

        PhysicsNavMeshBaker.Bake(navState.Map, ref world, state);

        navState.PhysicsBaked = true;
        navState.PhysicsBakeHash = ComputePhysicsBakeHash(state, ref world);
        navState.HasPhysicsBakedMesh = true;
        navState.PhysicsTriangleCount = navState.Map.Triangles.Count;

        // Update cached count and reset debounce
        int newBodyCount = 0;
        foreach (var e in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
        {
            var sb = state.GetComponent<StaticBody2D>(e);
            if ((sb.CollisionLayer & world.ObstacleMask) != 0)
                newBodyCount++;
        }
        navState.LastStaticBodyCount = newBodyCount;
        navState.DirtyTick = -1;

        FinishPhysicsBake(state, navState);
    }

    [ThreadStatic] private static HashSet<long>? _changedChunkKeys;
    [ThreadStatic] private static Dictionary<long, int>? _chunkHashScratch;

    private static void BakeChunkedIfNeeded(EntityWorld state, NavigationState navState, ref NavigationWorld2D world)
    {
        // Clear ForceBake flag early
        bool forceBake = world.ForceBake;
        if (forceBake)
            world.ForceBake = false;

        // Debounce: after initial bake, cheap-check relevant body count before doing anything expensive
        if (navState.PhysicsBaked && !forceBake)
        {
            int bodyCount = 0;
            uint obstacleMask = world.ObstacleMask;
            foreach (var e in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
            {
                var sb = state.GetComponent<StaticBody2D>(e);
                if ((sb.CollisionLayer & obstacleMask) != 0)
                    bodyCount++;
            }

            if (bodyCount != navState.LastStaticBodyCount)
            {
                var gameTime = state.GetCustomData<IGameTime>();
                long currentTick = gameTime?.CurrentTick ?? 0;

                if (navState.DirtyTick < 0)
                    navState.DirtyTick = currentTick;

                long ticksSinceDirty = currentTick - navState.DirtyTick;
                if (ticksSinceDirty < NavigationState.RebakeDebounceFrames)
                    return; // wait for more changes to batch together

                navState.LastStaticBodyCount = bodyCount;
                navState.DirtyTick = -1;
            }
            else
            {
                if (navState.DirtyTick >= 0) navState.DirtyTick = -1;
                return; // nothing changed
            }
        }

        // Detect world settings changes that invalidate all chunks
        int settingsHash = ComputeChunkWorldSettingsHash(ref world);
        bool settingsChanged = settingsHash != navState.ChunkWorldSettingsHash;
        if (settingsChanged)
        {
            navState.Chunks.Clear();
            navState.Map.Clear();
            navState.Map.RegionCosts.Clear();
            navState.ChunkWorldSettingsHash = settingsHash;
        }

        var boundsMin = world.BoundsMin;
        var boundsMax = world.BoundsMax;
        var chunkSize = world.ChunkSize;

        int chunkGridW = (int)((boundsMax.X - boundsMin.X) / chunkSize) + 1;
        int chunkGridH = (int)((boundsMax.Y - boundsMin.Y) / chunkSize) + 1;

        var changedKeys = _changedChunkKeys ??= new HashSet<long>();
        changedKeys.Clear();

        // Single-pass: compute all chunk hashes by iterating entities once
        var chunkHashes = _chunkHashScratch ??= new Dictionary<long, int>();
        chunkHashes.Clear();

        for (int cy = 0; cy < chunkGridH; cy++)
            for (int cx = 0; cx < chunkGridW; cx++)
                chunkHashes[((long)cx << 32) | (uint)cy] = 17;

        PhysicsNavMeshBaker.HashObstaclesIntoChunks(state, ref world, boundsMin, chunkSize, chunkGridW, chunkGridH, chunkHashes);

        // Compare hashes and rebake changed chunks
        {
            for (int cy = 0; cy < chunkGridH; cy++)
            {
                for (int cx = 0; cx < chunkGridW; cx++)
                {
                    long chunkKey = ((long)cx << 32) | (uint)cy;
                    int obstacleHash = chunkHashes[chunkKey];

                    if (!forceBake &&
                        navState.Chunks.TryGetValue(chunkKey, out var existing) &&
                        existing.ObstacleHash == obstacleHash)
                    {
                        continue;
                    }

                    var chunkMin = new Vector2(
                        boundsMin.X + (Float)cx * chunkSize,
                        boundsMin.Y + (Float)cy * chunkSize);
                    var chunkMax = new Vector2(
                        Float.Min(chunkMin.X + chunkSize, boundsMax.X),
                        Float.Min(chunkMin.Y + chunkSize, boundsMax.Y));

                    var result = PhysicsNavMeshBaker.BakeChunk(ref world, state, chunkMin, chunkMax);
                    result.ObstacleHash = obstacleHash;

                    if (navState.Chunks.TryGetValue(chunkKey, out var oldChunk))
                    {
                        result.TriangleStartIndex = oldChunk.TriangleStartIndex;
                        result.TriangleCount = oldChunk.TriangleCount;
                    }

                    navState.Chunks[chunkKey] = result;
                    changedKeys.Add(chunkKey);
                }
            }

            if (changedKeys.Count == 0 && !forceBake && navState.PhysicsBaked) return;
        }

        bool isFirstBake = !navState.PhysicsBaked || forceBake || settingsChanged;

        if (isFirstBake)
        {
            // First bake: build entire map from all chunks
            const int PHYSICS_BAKE_REGION_ID = -1;
            navState.Map.Clear();
            navState.Map.RegionCosts.Clear();
            navState.AgentPaths.Clear();
            navState.Map.RegionCosts[PHYSICS_BAKE_REGION_ID] = ((Float)1, (Float)0);

            foreach (var kvp in navState.Chunks)
            {
                var chunk = kvp.Value;
                chunk.TriangleStartIndex = navState.Map.Triangles.Count;
                foreach (var rect in chunk.Rectangles)
                {
                    navState.Map.AddTriangle(rect.V0, rect.V1, rect.V2, PHYSICS_BAKE_REGION_ID);
                    navState.Map.AddTriangle(rect.V0, rect.V2, rect.V3, PHYSICS_BAKE_REGION_ID);
                }
                chunk.TriangleCount = navState.Map.Triangles.Count - chunk.TriangleStartIndex;
            }
            navState.Map.BuildAdjacency((Float)MERGE_DISTANCE);
        }
        // Incremental update for subsequent bakes
        else if (changedKeys.Count > 0)
        {
            const int INCR_REGION_ID = -1;
            navState.AgentPaths.Clear();

            // Invalidate old triangles for changed chunks
            foreach (var key in changedKeys)
            {
                if (!navState.Chunks.TryGetValue(key, out var chunk)) continue;
                if (chunk.TriangleStartIndex >= 0 && chunk.TriangleCount > 0)
                {
                    navState.Map.InvalidateTriangleRange(chunk.TriangleStartIndex, chunk.TriangleCount);
                }
            }

            // Append new triangles for changed chunks
            int newTriStart = navState.Map.Triangles.Count;
            foreach (var key in changedKeys)
            {
                if (!navState.Chunks.TryGetValue(key, out var chunk)) continue;
                chunk.TriangleStartIndex = navState.Map.Triangles.Count;
                foreach (var rect in chunk.Rectangles)
                {
                    navState.Map.AddTriangle(rect.V0, rect.V1, rect.V2, INCR_REGION_ID);
                    navState.Map.AddTriangle(rect.V0, rect.V2, rect.V3, INCR_REGION_ID);
                }
                chunk.TriangleCount = navState.Map.Triangles.Count - chunk.TriangleStartIndex;
            }

            // Build adjacency only for newly added triangles + cross-connections
            if (navState.Map.Triangles.Count > newTriStart)
                navState.Map.BuildAdjacencyFrom(newTriStart, (Float)MERGE_DISTANCE);
            // Compact periodically to reclaim tombstoned slots
            // (when >25% of triangles are dead and we have enough to matter)
            int totalTris = navState.Map.Triangles.Count;
            if (navState.Map.InvalidatedCount > 100 &&
                navState.Map.InvalidatedCount > totalTris / 4)
            {
                var mapping = navState.Map.Compact();
                if (mapping != null)
                {
                    // Remap chunk triangle start indices
                    foreach (var chunk in navState.Chunks.Values)
                    {
                        if (chunk.TriangleStartIndex >= 0 && chunk.TriangleStartIndex < mapping.Length)
                        {
                            int newStart = mapping[chunk.TriangleStartIndex];
                            chunk.TriangleStartIndex = newStart >= 0 ? newStart : -1;
                        }
                    }
                }
            }
        }

        navState.PhysicsBaked = true;
        navState.HasPhysicsBakedMesh = true;
        navState.PhysicsTriangleCount = navState.Map.Triangles.Count - navState.Map.InvalidatedCount;

        // Cache obstacle body count for cheap dirty check
        int finalBodyCount = 0;
        foreach (var e in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
        {
            var sb = state.GetComponent<StaticBody2D>(e);
            if ((sb.CollisionLayer & world.ObstacleMask) != 0)
                finalBodyCount++;
        }
        navState.LastStaticBodyCount = finalBodyCount;
        navState.DirtyTick = -1;

        FinishPhysicsBake(state, navState);
    }

    private static int ComputeChunkWorldSettingsHash(ref NavigationWorld2D world)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + world.BoundsMin.GetHashCode();
            hash = hash * 31 + world.BoundsMax.GetHashCode();
            hash = hash * 31 + world.CellSize.GetHashCode();
            hash = hash * 31 + world.AgentRadius.GetHashCode();
            hash = hash * 31 + world.ObstacleMask.GetHashCode();
            hash = hash * 31 + world.IncludeDynamicBodies.GetHashCode();
            hash = hash * 31 + world.ChunkSize.GetHashCode();
            return hash;
        }
    }

    private static void FinishPhysicsBake(EntityWorld state, NavigationState navState)
    {
        // Force region hashes to invalidate so they get re-added
        navState.RegionHashes.Clear();
        navState.PreviousRegionIds.Clear();

        // If there are manual regions, mark dirty so they get integrated
        bool hasRegions = false;
        foreach (var _ in state.Filter<NavigationRegion2D, Transform2D>())
        {
            hasRegions = true;
            break;
        }

        navState.Map.IsDirty = hasRegions;
    }

    private static void RebuildNavMeshIfNeeded(EntityWorld state, NavigationState navState)
    {
        var regionBuffer = RegionBuffer;
        regionBuffer.Clear();

        foreach (var entity in state.Filter<NavigationRegion2D, Transform2D>())
        {
            regionBuffer.Add(entity);
        }
        regionBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        bool needsRebuild = false;

        var currentRegionIds = RegionIdScratch;
        currentRegionIds.Clear();
        foreach (var entity in regionBuffer)
        {
            currentRegionIds.Add(entity.Id);
            ref var region = ref state.GetComponent<NavigationRegion2D>(entity);

            if (!region.Enabled) continue;

            int hash = ComputeRegionHash(ref region, ref state.GetComponent<Transform2D>(entity));

            if (!navState.RegionHashes.TryGetValue(entity.Id, out int oldHash) || oldHash != hash)
            {
                needsRebuild = true;
            }

            navState.RegionHashes[entity.Id] = hash;
        }

        foreach (var prevId in navState.PreviousRegionIds)
        {
            if (!currentRegionIds.Contains(prevId))
            {
                needsRebuild = true;
                navState.RegionHashes.Remove(prevId);
            }
        }

        navState.PreviousRegionIds.Clear();
        foreach (var id in currentRegionIds)
            navState.PreviousRegionIds.Add(id);

        if (!needsRebuild && !navState.Map.IsDirty) return;

        // If we don't have a physics-baked mesh, do a full clear
        // If we DO have physics mesh, only clear regions (physics triangles are already in the map)
        if (!navState.HasPhysicsBakedMesh)
        {
            navState.Map.Clear();
            navState.Map.RegionCosts.Clear();
        }

        navState.AgentPaths.Clear();

        // Track where region triangles start — physics triangles already have adjacency
        int regionTriStart = navState.Map.Triangles.Count;

        var vertexScratch = VertexScratch;

        foreach (var entity in regionBuffer)
        {
            ref var region = ref state.GetComponent<NavigationRegion2D>(entity);
            if (!region.Enabled || region.VertexCount < 3) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);

            navState.Map.RegionCosts[entity.Id] = (region.TravelCost, region.EnterCost);

            int vCount = Math.Min(region.VertexCount, FixedPolygon16.Capacity);
            Float cos = Float.Cos(transform.GlobalRotation);
            Float sin = Float.Sin(transform.GlobalRotation);

            for (int i = 0; i < vCount; i++)
            {
                var local = region.Vertices[i];
                var scaled = local * transform.GlobalScale;
                var rotated = new Vector2(
                    scaled.X * cos - scaled.Y * sin,
                    scaled.X * sin + scaled.Y * cos
                );
                vertexScratch[i] = transform.GlobalPosition + rotated;
            }

            var triangles = Triangulator.Triangulate(vertexScratch, vCount);

            foreach (var (a, b, c) in triangles)
            {
                navState.Map.AddTriangle(vertexScratch[a], vertexScratch[b], vertexScratch[c], entity.Id);
            }
        }

        // Only build adjacency for new region triangles + their cross-connections.
        // Physics triangles (0..regionTriStart) already have grid-topology adjacency.
        navState.Map.BuildAdjacencyFrom(regionTriStart, (Float)MERGE_DISTANCE);
    }

    private static void UpdateAgents(EntityWorld state, NavigationState navState)
    {
        var agentBuffer = AgentBuffer;
        agentBuffer.Clear();

        foreach (var entity in state.Filter<NavigationAgent2D, Transform2D>())
        {
            agentBuffer.Add(entity);
        }
        agentBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        var physicsState = state.GetCustomData<RapierPhysicsState>();

        // Prune dead agent paths
        PruneAgentPaths(state, navState);

        foreach (var entity in agentBuffer)
        {
            ref var agent = ref state.GetComponent<NavigationAgent2D>(entity);

            // Skip idle agents entirely — they don't need path computation
            if (agent.IsNavigationFinished && agent.TargetPosition == agent.LastTargetPosition)
            {
                agent.Velocity = Vector2.Zero;
                continue;
            }

            ref var transform = ref state.GetComponent<Transform2D>(entity);

            var currentPos = transform.GlobalPosition;

            // Get or create path data
            if (!navState.AgentPaths.TryGetValue(entity.Id, out var pathData))
            {
                pathData = new AgentPathData();
                navState.AgentPaths[entity.Id] = pathData;
            }

            // Detect target change OR missing path data (after state sync,
            // NavigationState is transient and paths are lost — recompute to
            // keep client/server deterministic).
            agent.IsTargetPositionChanged = agent.TargetPosition != agent.LastTargetPosition;
            bool needsPath = agent.IsTargetPositionChanged
                || (pathData.PathPoints.Count == 0 && !agent.IsNavigationFinished
                    && agent.TargetPosition != Vector2.Zero);

            if (needsPath)
            {
                agent.LastTargetPosition = agent.TargetPosition;
                ComputePath(ref agent, pathData, currentPos, navState.Map, agent.Radius);
            }

            // Check if we've reached the actual target (straight-line to real target)
            var distToActualTarget = Vector2.Distance(currentPos, agent.TargetPosition);
            if (distToActualTarget <= agent.TargetDesiredDistance)
            {
                agent.IsNavigationFinished = true;
                agent.Velocity = Vector2.Zero;
                agent.NextPathPosition = agent.TargetPosition;
                agent.DistanceToTarget = (Float)0;
                continue;
            }

            // Advance along path with runtime shortcutting:
            // try to skip ahead to later waypoints if line-of-sight exists on the navmesh.
            if (pathData.PathPoints.Count > 0 && pathData.CurrentPathIndex < pathData.PathPoints.Count)
            {
                var nextPoint = pathData.PathPoints[pathData.CurrentPathIndex];
                var distToWaypoint = Vector2.Distance(currentPos, nextPoint);

                if (distToWaypoint <= agent.PathDesiredDistance)
                {
                    if (pathData.CurrentPathIndex == pathData.PathPoints.Count - 1)
                    {
                        pathData.PathPoints.Clear();
                        pathData.PathPoints.Add(agent.TargetPosition);
                        pathData.CurrentPathIndex = 0;
                    }
                    else
                    {
                        pathData.CurrentPathIndex++;
                    }
                }

                // Runtime shortcut: try to skip ahead using nav mesh LOS with margin.
                // Only check every 10 ticks to reduce IsPathClear cost.
                var gameTime = state.GetCustomData<IGameTime>();
                long tick = gameTime?.CurrentTick ?? 0;
                if ((tick + entity.Id) % 10 == 0) // stagger across agents
                {
                    var margin = agent.Radius > navState.Map.CellSize
                        ? agent.Radius : navState.Map.CellSize;

                    if (navState.Map.IsPathClear(currentPos, agent.TargetPosition, margin))
                    {
                        pathData.PathPoints.Clear();
                        pathData.PathPoints.Add(agent.TargetPosition);
                        pathData.CurrentPathIndex = 0;
                    }
                    else if (pathData.CurrentPathIndex < pathData.PathPoints.Count - 1)
                    {
                        for (int skip = pathData.PathPoints.Count - 1; skip > pathData.CurrentPathIndex; skip--)
                        {
                            if (navState.Map.IsPathClear(currentPos, pathData.PathPoints[skip], margin))
                            {
                                pathData.CurrentPathIndex = skip;
                                break;
                            }
                        }
                    }
                }

                if (pathData.CurrentPathIndex < pathData.PathPoints.Count)
                {
                    nextPoint = pathData.PathPoints[pathData.CurrentPathIndex];
                    agent.NextPathPosition = nextPoint;

                    var direction = nextPoint - currentPos;
                    var dist = direction.Magnitude;
                    agent.DistanceToTarget = ComputeRemainingDistance(pathData, currentPos);

                    if (dist > Float.Epsilon)
                    {
                        var desiredVelocity = direction / dist * agent.MaxSpeed;

                        // Avoidance: opt-in only. Auto-enabling via NavigationWorld2D caused
                        // desync because Rapier raycasts (float32) are non-deterministic
                        // across world rebuilds (state sync / rollback).
                        bool useAvoidance = agent.AvoidanceEnabled;
                        if (useAvoidance && physicsState?.World != null)
                        {
                            desiredVelocity = ApplyAvoidance(
                                currentPos, desiredVelocity, ref agent,
                                physicsState, entity.Id);
                        }

                        // Prevent backtracking: check LOS to target (staggered for perf)
                        if ((tick + entity.Id) % 10 == 0 &&
                            navState.Map.IsPathClear(currentPos, agent.TargetPosition,
                            agent.Radius > navState.Map.CellSize ? agent.Radius : navState.Map.CellSize))
                        {
                            var toTarget = agent.TargetPosition - currentPos;
                            var straightDist = toTarget.Magnitude;
                            if (straightDist > Float.Epsilon)
                            {
                                var toTargetNorm = toTarget / straightDist;
                                var fwd = desiredVelocity.X * toTargetNorm.X + desiredVelocity.Y * toTargetNorm.Y;
                                if (fwd < (Float)0)
                                {
                                    desiredVelocity = desiredVelocity - toTargetNorm * fwd;
                                    var lat = desiredVelocity.Magnitude;
                                    if (lat > Float.Epsilon)
                                        desiredVelocity = desiredVelocity / lat * agent.MaxSpeed;
                                    else
                                        desiredVelocity = toTarget / straightDist * agent.MaxSpeed;
                                }
                            }
                        }

                        agent.Velocity = desiredVelocity;
                    }
                    else
                    {
                        agent.Velocity = Vector2.Zero;
                    }

                    agent.IsNavigationFinished = false;
                }
            }
            else
            {
                // Path exhausted — check if we actually reached the target
                if (distToActualTarget <= agent.TargetDesiredDistance)
                {
                    agent.Velocity = Vector2.Zero;
                    agent.IsNavigationFinished = true;
                }
                else if (agent.IsTargetReachable)
                {
                    // Path consumed but target is still far — steer directly toward it
                    var direction = agent.TargetPosition - currentPos;
                    var dist = direction.Magnitude;
                    if (dist > Float.Epsilon)
                    {
                        agent.Velocity = direction / dist * agent.MaxSpeed;

                        bool useAvoidance = agent.AvoidanceEnabled;
                        if (useAvoidance && physicsState?.World != null)
                        {
                            agent.Velocity = ApplyAvoidance(
                                currentPos, agent.Velocity, ref agent,
                                physicsState, entity.Id);
                        }
                    }
                    agent.IsNavigationFinished = false;
                }
                else
                {
                    agent.Velocity = Vector2.Zero;
                    agent.IsNavigationFinished = true;
                }
            }
        }
    }

    [ThreadStatic] private static List<int>? _pruneBuffer;

    private static void PruneAgentPaths(EntityWorld state, NavigationState navState)
    {
        // Remove paths for entities that no longer have NavigationAgent2D
        var toRemove = _pruneBuffer ??= new List<int>();
        toRemove.Clear();
        foreach (var entityId in navState.AgentPaths.Keys)
        {
            var entity = new Entity(entityId);
            if (!state.HasComponent<NavigationAgent2D>(entity))
                toRemove.Add(entityId);
        }
        foreach (var id in toRemove)
            navState.AgentPaths.Remove(id);
    }

    private static void ComputePath(ref NavigationAgent2D agent, AgentPathData pathData, Vector2 currentPos, NavigationMap map, Float agentRadius = default)
    {
        pathData.CurrentPathIndex = 0;
        pathData.PathPoints.Clear();
        agent.IsNavigationFinished = false;
        agent.IsTargetReachable = false;

        if (map.Triangles.Count == 0) return;

        int startTri = map.FindTriangle(currentPos);
        int endTri = map.FindTriangle(agent.TargetPosition);

        Vector2 adjustedStart = currentPos;
        Vector2 adjustedEnd = agent.TargetPosition;
        bool startOffMesh = startTri < 0;

        if (startTri < 0)
        {
            var (tri, point) = map.FindClosestTriangle(currentPos);
            startTri = tri;
            adjustedStart = point;
        }

        if (endTri < 0)
        {
            var (tri, point) = map.FindClosestTriangle(agent.TargetPosition);
            endTri = tri;
            adjustedEnd = point;
        }

        if (startTri < 0 || endTri < 0) return;

        var triPath = map.FindTrianglePath(startTri, endTri);
        if (triPath == null) return;

        // Write waypoints directly into pathData to avoid allocating a temp list
        map.SmoothPath(triPath, adjustedStart, adjustedEnd, pathData.PathPoints, agentRadius);

        // If the agent is off-mesh (e.g. pushed into obstacle inflation zone),
        // prepend the closest on-mesh point so the agent walks back onto the
        // navmesh first. Rapier's wall-sliding will handle the collision.
        if (startOffMesh && pathData.PathPoints.Count > 0 && adjustedStart != currentPos)
        {
            pathData.PathPoints.Insert(0, adjustedStart);
        }

        pathData.CurrentPathIndex = 0;
        agent.IsTargetReachable = true;
        agent.IsNavigationFinished = false;
    }

    /// <summary>
    /// After path recomputation, advance CurrentPathIndex to the waypoint that
    /// makes the most forward progress — i.e. the closest waypoint to the agent
    /// that is also closer to the target than the agent is.
    /// This prevents backtracking to waypoints the agent already passed.
    /// </summary>
    private static Float ComputeRemainingDistance(AgentPathData pathData, Vector2 currentPos)
    {
        if (pathData.PathPoints.Count == 0) return (Float)0;

        Float dist = Vector2.Distance(currentPos, pathData.PathPoints[pathData.CurrentPathIndex]);
        for (int i = pathData.CurrentPathIndex; i < pathData.PathPoints.Count - 1; i++)
        {
            dist += Vector2.Distance(pathData.PathPoints[i], pathData.PathPoints[i + 1]);
        }
        return dist;
    }

    /// <summary>
    /// Ray-based obstacle avoidance using Rapier raycasts.
    /// Casts a fan of rays in the movement direction and adjusts velocity to steer away from hits.
    /// </summary>
    private static Vector2 ApplyAvoidance(
        Vector2 position, Vector2 desiredVelocity, ref NavigationAgent2D agent,
        RapierPhysicsState physicsState, int selfEntityId)
    {
        if (physicsState.World == null) return desiredVelocity;

        var speed = desiredVelocity.Magnitude;
        if (speed <= Float.Epsilon) return desiredVelocity;

        var forward = desiredVelocity / speed;
        var lookahead = (float)agent.AvoidanceLookahead;
        int rayCount = Math.Max(1, Math.Min(agent.AvoidanceRayCount, 16));

        ulong? selfCollider = null;
        if (physicsState.EntityToCollider.TryGetValue(selfEntityId, out ulong col))
            selfCollider = col;

        Vector2 avoidanceForce = Vector2.Zero;
        bool anyHit = false;
        Float totalWeight = (Float)0;

        Float halfSpread = Float.Pi / (Float)3; // 60 degree half-spread
        Float angleStep = rayCount > 1 ? (halfSpread * (Float)2) / (Float)(rayCount - 1) : (Float)0;
        Float startAngle = forward.ToAngle() - halfSpread;

        for (int i = 0; i < rayCount; i++)
        {
            Float angle = startAngle + angleStep * (Float)i;
            var dir = new Vector2(Float.Cos(angle), Float.Sin(angle));

            var ray = new RRay(
                new RVector((float)position.X, (float)position.Y),
                new RVector((float)dir.X, (float)dir.Y)
            );

            var hit = physicsState.World.CastRay(ray, lookahead, true, agent.AvoidanceMask, selfCollider);
            if (hit != null)
            {
                anyHit = true;
                Float hitDist = (Float)hit.toi;
                Float strength = (Float)1 - (hitDist / agent.AvoidanceLookahead);
                strength = Float.Clamp(strength, 0, 1);

                var normal = new Vector2(hit.normal.x, hit.normal.y);
                if (normal.SqrMagnitude > Float.Epsilon)
                {
                    avoidanceForce += normal * strength;
                }
                else
                {
                    avoidanceForce += new Vector2(-dir.Y, dir.X) * strength;
                }
                totalWeight += strength;
            }
        }

        if (!anyHit) return desiredVelocity;

        if (totalWeight > Float.Epsilon)
        {
            avoidanceForce = avoidanceForce / totalWeight;
        }

        var blendFactor = (Float)0.3f;
        var result = desiredVelocity.Normalized * ((Float)1 - blendFactor) + avoidanceForce.Normalized * blendFactor;

        if (result.SqrMagnitude > Float.Epsilon)
        {
            result = result.Normalized * speed;
        }

        return result;
    }

    private static int ComputeRegionHash(ref NavigationRegion2D region, ref Transform2D transform)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + region.VertexCount;
            hash = hash * 31 + region.NavigationLayers.GetHashCode();
            hash = hash * 31 + region.Enabled.GetHashCode();
            hash = hash * 31 + transform.GlobalPosition.GetHashCode();
            hash = hash * 31 + transform.GlobalRotation.GetHashCode();
            hash = hash * 31 + transform.GlobalScale.GetHashCode();

            if (region.VertexCount > 0)
            {
                hash = hash * 31 + region.Vertices[0].GetHashCode();
                if (region.VertexCount > 1)
                    hash = hash * 31 + region.Vertices[region.VertexCount - 1].GetHashCode();
            }

            return hash;
        }
    }

    /// <summary>
    /// Compute a hash of the physics state relevant to navigation baking.
    /// Includes static body positions/shapes and NavigationWorld2D settings.
    /// </summary>
    private static int ComputePhysicsBakeHash(EntityWorld state, ref NavigationWorld2D world)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + world.BoundsMin.GetHashCode();
            hash = hash * 31 + world.BoundsMax.GetHashCode();
            hash = hash * 31 + world.CellSize.GetHashCode();
            hash = hash * 31 + world.AgentRadius.GetHashCode();
            hash = hash * 31 + world.ObstacleMask.GetHashCode();
            hash = hash * 31 + world.IncludeDynamicBodies.GetHashCode();

            // Hash static body count and positions (sorted for determinism)
            var bodyBuffer = HashBuffer;
            bodyBuffer.Clear();
            foreach (var entity in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
            {
                bodyBuffer.Add(entity.Id);
            }
            bodyBuffer.Sort();

            hash = hash * 31 + bodyBuffer.Count;
            foreach (var id in bodyBuffer)
            {
                var entity = new Entity(id);
                ref var transform = ref state.GetComponent<Transform2D>(entity);
                hash = hash * 31 + transform.GlobalPosition.GetHashCode();
                hash = hash * 31 + transform.GlobalRotation.GetHashCode();

                ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
                hash = hash * 31 + shape.GetHashCode();
            }

            // Include dynamic bodies if configured
            if (world.IncludeDynamicBodies)
            {
                bodyBuffer.Clear();
                foreach (var entity in state.Filter<RigidBody2D, Transform2D, CollisionShape2D>())
                {
                    bodyBuffer.Add(entity.Id);
                }
                bodyBuffer.Sort();

                hash = hash * 31 + bodyBuffer.Count;
                foreach (var id in bodyBuffer)
                {
                    var entity = new Entity(id);
                    ref var transform = ref state.GetComponent<Transform2D>(entity);
                    hash = hash * 31 + transform.GlobalPosition.GetHashCode();
                }
            }

            // NavigationObstacle2D with AffectsNavigationMesh
            bodyBuffer.Clear();
            foreach (var entity in state.Filter<NavigationObstacle2D, Transform2D>())
            {
                ref var obstacle = ref state.GetComponent<NavigationObstacle2D>(entity);
                if (obstacle.AffectsNavigationMesh)
                    bodyBuffer.Add(entity.Id);
            }
            bodyBuffer.Sort();

            hash = hash * 31 + bodyBuffer.Count;
            foreach (var id in bodyBuffer)
            {
                var entity = new Entity(id);
                ref var transform = ref state.GetComponent<Transform2D>(entity);
                hash = hash * 31 + transform.GlobalPosition.GetHashCode();
            }

            return hash;
        }
    }
}
