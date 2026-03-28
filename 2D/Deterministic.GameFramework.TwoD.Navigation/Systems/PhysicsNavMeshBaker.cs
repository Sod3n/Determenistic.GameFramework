using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Generates a navigation mesh from the physics world by rasterizing obstacle
/// AABBs onto a grid, then merging adjacent walkable cells into large rectangles
/// to minimize triangle count.
///
/// Algorithm:
/// 1. Divide the NavigationWorld2D bounds into a grid based on CellSize
/// 2. Start with all cells walkable
/// 3. For each obstacle entity, compute its AABB inflated by AgentRadius
/// 4. Mark all grid cells overlapping the inflated AABB as blocked
/// 5. Greedy-merge walkable cells into maximal rectangles (~100-500 vs ~25K cells)
/// 6. Triangulate each rectangle (2 triangles) and build adjacency via spatial grid
/// </summary>
internal static class PhysicsNavMeshBaker
{
    private const float MERGE_DISTANCE = 1.0f;

    // Reusable scratch buffers to avoid GC allocations per bake
    [ThreadStatic] private static bool[]? _gridScratch;
    [ThreadStatic] private static bool[]? _usedScratch;
    [ThreadStatic] private static List<GridRect>? _rectsScratch;

    /// <summary>
    /// Bake a navigation mesh from ECS obstacle data into the given NavigationMap.
    /// </summary>
    public static void Bake(
        NavigationMap map,
        ref NavigationWorld2D world,
        EntityWorld state)
    {
        var boundsMin = world.BoundsMin;
        var boundsMax = world.BoundsMax;
        var cellSize = world.CellSize;
        var agentRadius = world.AgentRadius;

        if (cellSize <= (Float)0) return;

        // Calculate grid dimensions
        int gridW = (int)((boundsMax.X - boundsMin.X) / cellSize) + 1;
        int gridH = (int)((boundsMax.Y - boundsMin.Y) / cellSize) + 1;

        // Clamp grid to reasonable size
        gridW = Math.Min(gridW, 512);
        gridH = Math.Min(gridH, 512);

        if (gridW <= 0 || gridH <= 0) return;

        // Reuse grid buffer to avoid GC allocation
        int gridSize = gridW * gridH;
        if (_gridScratch == null || _gridScratch.Length < gridSize)
            _gridScratch = new bool[gridSize];
        var grid = _gridScratch;
        for (int i = 0; i < gridSize; i++)
            grid[i] = true;

        // Rasterize each obstacle's inflated AABB onto the grid
        RasterizeObstacles(grid, gridW, gridH, boundsMin, cellSize, agentRadius, world.ObstacleMask, world.IncludeDynamicBodies, state);

        // Greedy-merge walkable cells into maximal rectangles
        // This reduces triangle count from ~25K to ~200-500
        var rects = GreedyMerge(grid, gridW, gridH);

        const int PHYSICS_BAKE_REGION_ID = -1;
        map.RegionCosts[PHYSICS_BAKE_REGION_ID] = ((Float)1, (Float)0);

        // Triangulate each merged rectangle (2 triangles per rect)
        foreach (var rect in rects)
        {
            var min = new Vector2(
                boundsMin.X + (Float)rect.X * cellSize,
                boundsMin.Y + (Float)rect.Y * cellSize
            );
            var max = new Vector2(
                min.X + (Float)rect.W * cellSize,
                min.Y + (Float)rect.H * cellSize
            );

            var v0 = min;
            var v1 = new Vector2(max.X, min.Y);
            var v2 = max;
            var v3 = new Vector2(min.X, max.Y);

            map.AddTriangle(v0, v1, v2, PHYSICS_BAKE_REGION_ID);
            map.AddTriangle(v0, v2, v3, PHYSICS_BAKE_REGION_ID);
        }

        // Build adjacency via spatial grid — fast because we now have ~200-500 triangles
        // instead of ~25K. Shared edges between adjacent rectangles are found by vertex matching.
        map.BuildAdjacency((Float)MERGE_DISTANCE);
    }

    /// <summary>
    /// Greedy rectangle merging for walkable cells.
    /// Scans left-to-right, top-to-bottom, and expands each walkable cell
    /// into the largest possible rectangle, then marks those cells as consumed.
    /// </summary>
    /// <summary>
    /// Max cells per rectangle dimension. Limits portal width so the funnel
    /// algorithm doesn't produce massive detours through oversized triangles.
    /// 16 cells × cellSize gives reasonable portal widths (e.g. 128 units at cellSize=8).
    /// </summary>
    private const int MAX_RECT_CELLS = 16;

    private static List<GridRect> GreedyMerge(bool[] grid, int w, int h)
    {
        int size = w * h;
        if (_usedScratch == null || _usedScratch.Length < size)
            _usedScratch = new bool[size];
        var used = _usedScratch;
        Array.Clear(used, 0, size);

        var rects = _rectsScratch ??= new List<GridRect>();
        rects.Clear();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!grid[y * w + x] || used[y * w + x]) continue;

                // Find max width from (x, y), capped to keep portals reasonable
                int maxW = 0;
                while (x + maxW < w && maxW < MAX_RECT_CELLS &&
                       grid[y * w + x + maxW] && !used[y * w + x + maxW])
                    maxW++;

                // Expand downward, also capped
                int maxH = 1;
                bool canExpand = true;
                while (canExpand && y + maxH < h && maxH < MAX_RECT_CELLS)
                {
                    for (int dx = 0; dx < maxW; dx++)
                    {
                        int idx = (y + maxH) * w + x + dx;
                        if (!grid[idx] || used[idx])
                        {
                            canExpand = false;
                            break;
                        }
                    }
                    if (canExpand) maxH++;
                }

                // Mark used
                for (int dy = 0; dy < maxH; dy++)
                {
                    for (int dx = 0; dx < maxW; dx++)
                    {
                        used[(y + dy) * w + x + dx] = true;
                    }
                }

                rects.Add(new GridRect { X = x, Y = y, W = maxW, H = maxH });
            }
        }

        return rects;
    }

    /// <summary>
    /// Bake a single chunk of the world. Rasterizes only the sub-grid covered by
    /// [chunkMin, chunkMax] and returns cached rectangle vertices for replay.
    /// </summary>
    public static ChunkBakeResult BakeChunk(
        ref NavigationWorld2D world,
        EntityWorld state,
        Vector2 chunkMin,
        Vector2 chunkMax)
    {
        var cellSize = world.CellSize;
        var agentRadius = world.AgentRadius;

        int gridW = (int)((chunkMax.X - chunkMin.X) / cellSize) + 1;
        int gridH = (int)((chunkMax.Y - chunkMin.Y) / cellSize) + 1;
        gridW = Math.Min(gridW, 512);
        gridH = Math.Min(gridH, 512);

        var result = new ChunkBakeResult();
        if (gridW <= 0 || gridH <= 0) return result;

        int gridSize = gridW * gridH;
        if (_gridScratch == null || _gridScratch.Length < gridSize)
            _gridScratch = new bool[gridSize];
        var grid = _gridScratch;
        for (int i = 0; i < gridSize; i++)
            grid[i] = true;

        // Rasterize obstacles onto the chunk-local grid
        RasterizeObstacles(grid, gridW, gridH, chunkMin, cellSize, agentRadius,
            world.ObstacleMask, world.IncludeDynamicBodies, state);

        var rects = GreedyMerge(grid, gridW, gridH);

        foreach (var rect in rects)
        {
            var min = new Vector2(
                chunkMin.X + (Float)rect.X * cellSize,
                chunkMin.Y + (Float)rect.Y * cellSize);
            var max = new Vector2(
                min.X + (Float)rect.W * cellSize,
                min.Y + (Float)rect.H * cellSize);

            result.Rectangles.Add((
                min,
                new Vector2(max.X, min.Y),
                max,
                new Vector2(min.X, max.Y)));
        }

        return result;
    }

    /// <summary>
    /// Compute a hash of obstacles overlapping a specific chunk AABB.
    /// Only entities whose inflated AABB intersects [chunkMin, chunkMax] contribute.
    /// </summary>
    public static int ComputeChunkObstacleHash(
        EntityWorld state,
        Vector2 chunkMin, Vector2 chunkMax,
        ref NavigationWorld2D world)
    {
        unchecked
        {
            int hash = 17;
            var agentRadius = world.AgentRadius;

            // StaticBody2D
            foreach (var entity in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
            {
                ref var body = ref state.GetComponent<StaticBody2D>(entity);
                if ((body.CollisionLayer & world.ObstacleMask) == 0) continue;

                ref var transform = ref state.GetComponent<Transform2D>(entity);
                ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
                if (shape.Disabled) continue;

                var aabb = ComputeInflatedAABB(ref transform, ref shape, agentRadius);
                if (!AABBOverlaps(aabb.min, aabb.max, chunkMin, chunkMax)) continue;

                hash = hash * 31 + entity.Id;
                hash = hash * 31 + transform.GlobalPosition.GetHashCode();
                hash = hash * 31 + transform.GlobalRotation.GetHashCode();
                hash = hash * 31 + shape.GetHashCode();
            }

            // RigidBody2D (optional)
            if (world.IncludeDynamicBodies)
            {
                foreach (var entity in state.Filter<RigidBody2D, Transform2D, CollisionShape2D>())
                {
                    ref var body = ref state.GetComponent<RigidBody2D>(entity);
                    if ((body.CollisionLayer & world.ObstacleMask) == 0) continue;

                    ref var transform = ref state.GetComponent<Transform2D>(entity);
                    ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
                    if (shape.Disabled) continue;

                    var aabb = ComputeInflatedAABB(ref transform, ref shape, agentRadius);
                    if (!AABBOverlaps(aabb.min, aabb.max, chunkMin, chunkMax)) continue;

                    hash = hash * 31 + entity.Id;
                    hash = hash * 31 + transform.GlobalPosition.GetHashCode();
                }
            }

            // NavigationObstacle2D
            foreach (var entity in state.Filter<NavigationObstacle2D, Transform2D>())
            {
                ref var obstacle = ref state.GetComponent<NavigationObstacle2D>(entity);
                if (!obstacle.AffectsNavigationMesh) continue;

                ref var transform = ref state.GetComponent<Transform2D>(entity);
                var radius = obstacle.Radius + agentRadius;
                var pos = transform.GlobalPosition;
                var obsMin = new Vector2(pos.X - radius, pos.Y - radius);
                var obsMax = new Vector2(pos.X + radius, pos.Y + radius);
                if (!AABBOverlaps(obsMin, obsMax, chunkMin, chunkMax)) continue;

                hash = hash * 31 + entity.Id;
                hash = hash * 31 + transform.GlobalPosition.GetHashCode();
            }

            return hash;
        }
    }

    private static (Vector2 min, Vector2 max) ComputeInflatedAABB(
        ref Transform2D transform, ref CollisionShape2D shape, Float agentRadius)
    {
        var worldPos = transform.GlobalPosition + shape.Position;
        Float halfExtentX, halfExtentY;

        if (shape.Type == CollisionShapeType.Circle)
        {
            halfExtentX = shape.Circle.Radius;
            halfExtentY = shape.Circle.Radius;
        }
        else if (shape.Type == CollisionShapeType.Rectangle)
        {
            halfExtentX = shape.Rectangle.Size.X / (Float)2;
            halfExtentY = shape.Rectangle.Size.Y / (Float)2;
            var rot = transform.GlobalRotation + shape.Rotation;
            if (rot != (Float)0)
            {
                var cos = Float.Abs(Float.Cos(rot));
                var sin = Float.Abs(Float.Sin(rot));
                var newHalfX = halfExtentX * cos + halfExtentY * sin;
                var newHalfY = halfExtentX * sin + halfExtentY * cos;
                halfExtentX = newHalfX;
                halfExtentY = newHalfY;
            }
        }
        else if (shape.Type == CollisionShapeType.Capsule)
        {
            halfExtentX = shape.Capsule.Radius;
            halfExtentY = shape.Capsule.Height / (Float)2;
            var rot = transform.GlobalRotation + shape.Rotation;
            if (rot != (Float)0)
            {
                var cos = Float.Abs(Float.Cos(rot));
                var sin = Float.Abs(Float.Sin(rot));
                var newHalfX = halfExtentX * cos + halfExtentY * sin;
                var newHalfY = halfExtentX * sin + halfExtentY * cos;
                halfExtentX = newHalfX;
                halfExtentY = newHalfY;
            }
        }
        else
        {
            halfExtentX = agentRadius;
            halfExtentY = agentRadius;
        }

        halfExtentX += agentRadius;
        halfExtentY += agentRadius;

        return (
            new Vector2(worldPos.X - halfExtentX, worldPos.Y - halfExtentY),
            new Vector2(worldPos.X + halfExtentX, worldPos.Y + halfExtentY));
    }

    private static bool AABBOverlaps(Vector2 aMin, Vector2 aMax, Vector2 bMin, Vector2 bMax)
    {
        return aMin.X <= bMax.X && aMax.X >= bMin.X &&
               aMin.Y <= bMax.Y && aMax.Y >= bMin.Y;
    }

    /// <summary>
    /// Single-pass obstacle hashing: iterates all entities once and distributes
    /// hash contributions to each overlapping chunk. O(entities × overlapping_chunks_per_entity)
    /// instead of O(chunks × entities).
    /// </summary>
    public static void HashObstaclesIntoChunks(
        EntityWorld state, ref NavigationWorld2D world,
        Vector2 boundsMin, Float chunkSize,
        int chunkGridW, int chunkGridH,
        Dictionary<long, int> chunkHashes)
    {
        var agentRadius = world.AgentRadius;

        // StaticBody2D
        foreach (var entity in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            if ((body.CollisionLayer & world.ObstacleMask) == 0) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
            if (shape.Disabled) continue;

            var aabb = ComputeInflatedAABB(ref transform, ref shape, agentRadius);
            int entityHash;
            unchecked
            {
                entityHash = entity.Id * 397;
                entityHash ^= transform.GlobalPosition.GetHashCode();
                entityHash ^= transform.GlobalRotation.GetHashCode() * 31;
                entityHash ^= shape.GetHashCode() * 17;
            }
            DistributeHashToChunks(aabb.min, aabb.max, boundsMin, chunkSize, chunkGridW, chunkGridH, entityHash, chunkHashes);
        }

        // RigidBody2D (optional)
        if (world.IncludeDynamicBodies)
        {
            foreach (var entity in state.Filter<RigidBody2D, Transform2D, CollisionShape2D>())
            {
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                if ((body.CollisionLayer & world.ObstacleMask) == 0) continue;

                ref var transform = ref state.GetComponent<Transform2D>(entity);
                ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
                if (shape.Disabled) continue;

                var aabb = ComputeInflatedAABB(ref transform, ref shape, agentRadius);
                int entityHash;
                unchecked
                {
                    entityHash = entity.Id * 397;
                    entityHash ^= transform.GlobalPosition.GetHashCode();
                }
                DistributeHashToChunks(aabb.min, aabb.max, boundsMin, chunkSize, chunkGridW, chunkGridH, entityHash, chunkHashes);
            }
        }

        // NavigationObstacle2D
        foreach (var entity in state.Filter<NavigationObstacle2D, Transform2D>())
        {
            ref var obstacle = ref state.GetComponent<NavigationObstacle2D>(entity);
            if (!obstacle.AffectsNavigationMesh) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            var radius = obstacle.Radius + agentRadius;
            var pos = transform.GlobalPosition;
            var obsMin = new Vector2(pos.X - radius, pos.Y - radius);
            var obsMax = new Vector2(pos.X + radius, pos.Y + radius);

            int entityHash;
            unchecked
            {
                entityHash = entity.Id * 397;
                entityHash ^= transform.GlobalPosition.GetHashCode();
            }
            DistributeHashToChunks(obsMin, obsMax, boundsMin, chunkSize, chunkGridW, chunkGridH, entityHash, chunkHashes);
        }
    }

    private static void DistributeHashToChunks(
        Vector2 aabbMin, Vector2 aabbMax,
        Vector2 boundsMin, Float chunkSize,
        int chunkGridW, int chunkGridH,
        int entityHash,
        Dictionary<long, int> chunkHashes)
    {
        // Find chunk range overlapping this AABB
        int cx0 = (int)((aabbMin.X - boundsMin.X) / chunkSize);
        int cy0 = (int)((aabbMin.Y - boundsMin.Y) / chunkSize);
        int cx1 = (int)((aabbMax.X - boundsMin.X) / chunkSize);
        int cy1 = (int)((aabbMax.Y - boundsMin.Y) / chunkSize);

        cx0 = Math.Max(0, cx0);
        cy0 = Math.Max(0, cy0);
        cx1 = Math.Min(chunkGridW - 1, cx1);
        cy1 = Math.Min(chunkGridH - 1, cy1);

        unchecked
        {
            for (int cy = cy0; cy <= cy1; cy++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    long key = ((long)cx << 32) | (uint)cy;
                    if (chunkHashes.TryGetValue(key, out int existing))
                        chunkHashes[key] = existing * 31 + entityHash;
                }
            }
        }
    }

    private struct GridRect
    {
        public int X, Y, W, H;
    }

    /// <summary>
    /// Rasterize obstacle AABBs onto the grid using ECS state directly (no Rapier queries).
    /// </summary>
    private static void RasterizeObstacles(
        bool[] grid, int gridW, int gridH,
        Vector2 boundsMin, Float cellSize, Float agentRadius,
        uint obstacleMask, bool includeDynamic,
        EntityWorld state)
    {
        // StaticBody2D obstacles
        foreach (var entity in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            if ((body.CollisionLayer & obstacleMask) == 0) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var shape = ref state.GetComponent<CollisionShape2D>(entity);

            StampObstacleAABB(grid, gridW, gridH, boundsMin, cellSize, agentRadius, ref transform, ref shape);
        }

        // RigidBody2D obstacles (optional)
        if (includeDynamic)
        {
            foreach (var entity in state.Filter<RigidBody2D, Transform2D, CollisionShape2D>())
            {
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                if ((body.CollisionLayer & obstacleMask) == 0) continue;

                ref var transform = ref state.GetComponent<Transform2D>(entity);
                ref var shape = ref state.GetComponent<CollisionShape2D>(entity);

                StampObstacleAABB(grid, gridW, gridH, boundsMin, cellSize, agentRadius, ref transform, ref shape);
            }
        }

        // NavigationObstacle2D with AffectsNavigationMesh
        foreach (var entity in state.Filter<NavigationObstacle2D, Transform2D>())
        {
            ref var obstacle = ref state.GetComponent<NavigationObstacle2D>(entity);
            if (!obstacle.AffectsNavigationMesh) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);

            var radius = obstacle.Radius + agentRadius;
            var pos = transform.GlobalPosition;

            MarkBlockedCells(grid, gridW, gridH, boundsMin, cellSize,
                pos.X - radius, pos.Y - radius,
                pos.X + radius, pos.Y + radius);
        }
    }

    /// <summary>
    /// Compute the world-space AABB of a collision shape (inflated by agentRadius)
    /// and mark overlapping grid cells as blocked.
    /// </summary>
    private static void StampObstacleAABB(
        bool[] grid, int gridW, int gridH,
        Vector2 boundsMin, Float cellSize, Float agentRadius,
        ref Transform2D transform, ref CollisionShape2D shape)
    {
        if (shape.Disabled) return;

        var worldPos = transform.GlobalPosition + shape.Position;
        Float halfExtentX, halfExtentY;

        if (shape.Type == CollisionShapeType.Circle)
        {
            var r = shape.Circle.Radius;
            halfExtentX = r;
            halfExtentY = r;
        }
        else if (shape.Type == CollisionShapeType.Rectangle)
        {
            halfExtentX = shape.Rectangle.Size.X / (Float)2;
            halfExtentY = shape.Rectangle.Size.Y / (Float)2;

            var rot = transform.GlobalRotation + shape.Rotation;
            if (rot != (Float)0)
            {
                var cos = Float.Abs(Float.Cos(rot));
                var sin = Float.Abs(Float.Sin(rot));
                var newHalfX = halfExtentX * cos + halfExtentY * sin;
                var newHalfY = halfExtentX * sin + halfExtentY * cos;
                halfExtentX = newHalfX;
                halfExtentY = newHalfY;
            }
        }
        else if (shape.Type == CollisionShapeType.Capsule)
        {
            var r = shape.Capsule.Radius;
            var halfH = shape.Capsule.Height / (Float)2;
            halfExtentX = r;
            halfExtentY = halfH;

            var rot = transform.GlobalRotation + shape.Rotation;
            if (rot != (Float)0)
            {
                var cos = Float.Abs(Float.Cos(rot));
                var sin = Float.Abs(Float.Sin(rot));
                var newHalfX = halfExtentX * cos + halfExtentY * sin;
                var newHalfY = halfExtentX * sin + halfExtentY * cos;
                halfExtentX = newHalfX;
                halfExtentY = newHalfY;
            }
        }
        else
        {
            halfExtentX = agentRadius;
            halfExtentY = agentRadius;
        }

        halfExtentX += agentRadius;
        halfExtentY += agentRadius;

        MarkBlockedCells(grid, gridW, gridH, boundsMin, cellSize,
            worldPos.X - halfExtentX, worldPos.Y - halfExtentY,
            worldPos.X + halfExtentX, worldPos.Y + halfExtentY);
    }

    /// <summary>
    /// Mark all grid cells overlapping the given world-space AABB as blocked.
    /// </summary>
    private static void MarkBlockedCells(
        bool[] grid, int gridW, int gridH,
        Vector2 boundsMin, Float cellSize,
        Float minX, Float minY, Float maxX, Float maxY)
    {
        int x0 = (int)((minX - boundsMin.X) / cellSize);
        int y0 = (int)((minY - boundsMin.Y) / cellSize);
        int x1 = (int)((maxX - boundsMin.X) / cellSize);
        int y1 = (int)((maxY - boundsMin.Y) / cellSize);

        x0 = Math.Max(0, x0);
        y0 = Math.Max(0, y0);
        x1 = Math.Min(gridW - 1, x1);
        y1 = Math.Min(gridH - 1, y1);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                grid[y * gridW + x] = false;
            }
        }
    }
}
