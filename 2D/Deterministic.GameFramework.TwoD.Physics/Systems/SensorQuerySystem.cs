using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Utils.Logging;

using Float = Deterministic.GameFramework.Types.Float;

namespace Deterministic.GameFramework.Physics2D.Systems;

/// <summary>
/// Stateless sensor overlap system. Replaces Box2DPhysicsSystem for games that only
/// need Area2D overlap detection and nav-based character movement.
///
/// Each frame:
///   1. Builds a spatial hash grid from all collidable entities (zero persistent state).
///   2. For each Area2D sensor, queries nearby cells and tests overlap via narrowphase.
///   3. Writes entered/exited/overlapping results into Area2D components.
///   4. Integrates CharacterBody2D velocity → position (simple, no collision solver).
///
/// All state lives in ECS components — nothing to serialize, rollback, or invalidate.
/// Narrowphase uses deterministic Fixed64 math (no floating-point non-determinism).
/// </summary>
[ManualSystem]
public class SensorQuerySystem : ISystem
{
    // Spatial hash grid — rebuilt from scratch every frame, zero persistent state.
    private static readonly Float CellSize = (Float)8;
    private static readonly Float InvCellSize = Float.One / (Float)8;

    // Scratch buffers (ThreadStatic for safety if ever run concurrently)
    [ThreadStatic] private static Dictionary<long, List<int>>? _grid;
    [ThreadStatic] private static List<long>? _usedKeys;
    [ThreadStatic] private static List<List<int>>? _listPool;
    [ThreadStatic] private static List<Entity>? _entityBuffer;
    [ThreadStatic] private static HashSet<int>? _visitedSet;

    private static Dictionary<long, List<int>> Grid => _grid ??= new();
    private static List<long> UsedKeys => _usedKeys ??= new();
    private static List<List<int>> ListPool => _listPool ??= new();
    private static List<Entity> EntityBuffer => _entityBuffer ??= new();
    private static HashSet<int> VisitedSet => _visitedSet ??= new();

    /// <summary>
    /// Rate (Hz) at which the expensive sensor-overlap path runs. Sim itself still runs at
    /// the engine's full <see cref="IGameTime.FixedDeltaTime"/> cadence; this property
    /// throttles only grid build + overlap tests + enter/exit mask updates.
    /// Character integration (<see cref="StepCharacters"/>) always runs every tick so
    /// movement stays smooth.
    /// <para>
    /// <c>60</c> = sensor overlap every tick. Lower values (e.g. <c>30</c>) cut sim CPU
    /// at the cost of delayed enter/exit events; nav-driven gameplay tolerates this.
    /// </para>
    /// </summary>
    public static int SystemTickRate { get; set; } = 60;

    public void Update(EntityWorld state)
    {
        var gameTime = state.GetCustomData<IGameTime>();
        if (gameTime == null) return;

        StepCharacters(state, gameTime.FixedDeltaTime);

        // Skip the sensor-overlap path if we're not on a scheduled sub-tick. Interval is
        // derived from the sim's actual fixed step so the property stays valid if the
        // engine tick rate ever changes.
        int simTickRate = gameTime.FixedDeltaTime > (Float)0 ? (int)(Float.One / gameTime.FixedDeltaTime) : 60;
        int rate = SystemTickRate > 0 ? SystemTickRate : simTickRate;
        int interval = rate >= simTickRate ? 1 : simTickRate / rate;
        if ((gameTime.CurrentTick % interval) != 0) return;

        BuildGrid(state);
        ProcessSensors(state);
        ReturnGrid();
    }

    // ══════════════════════════════════════════════
    //  Character movement (replaces Box2D solver)
    // ══════════════════════════════════════════════

    private static void StepCharacters(EntityWorld state, Float dt)
    {
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>())
        {
            ref var body = ref state.GetComponent<CharacterBody2D>(entity);
            ref var transform = ref state.GetComponent<Transform2D>(entity);

            // Integrate velocity → position. Both player (SetMoveDirectionAction)
            // and nav-driven entities (cows/helpers via SwarmFollow/HelperSystem)
            // write to body.Velocity. We just apply it.
            if (body.Velocity.SqrMagnitude > (Float)0.0001f)
            {
                transform.Position = transform.Position + body.Velocity * dt;
            }
            body.RealVelocity = body.Velocity;
        }
    }

    // ══════════════════════════════════════════════
    //  Spatial hash grid (rebuilt every frame)
    // ══════════════════════════════════════════════

    private static void BuildGrid(EntityWorld state)
    {
        var grid = Grid;
        var usedKeys = UsedKeys;

        foreach (var entity in state.Filter<Transform2D, CollisionShape2D>())
        {
            ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
            if (shape.Disabled) continue;

            // Skip Area2D sensors — they query, they don't get inserted
            if (state.HasComponent<Area2D>(entity)) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            var pos = transform.GlobalPosition;

            // Compute AABB for the shape
            Float halfW, halfH;
            if (shape.Type == CollisionShapeType.Circle)
            {
                halfW = halfH = shape.Circle.Radius;
            }
            else if (shape.Type == CollisionShapeType.Rectangle)
            {
                halfW = shape.Rectangle.Size.X * (Float)0.5f;
                halfH = shape.Rectangle.Size.Y * (Float)0.5f;
            }
            else if (shape.Type == CollisionShapeType.Capsule)
            {
                halfW = shape.Capsule.Radius;
                halfH = shape.Capsule.Height * (Float)0.5f;
            }
            else continue;

            Float px = pos.X + shape.Position.X;
            Float py = pos.Y + shape.Position.Y;

            // Insert into all cells the AABB touches
            int minCX = (int)Float.Floor((px - halfW) * InvCellSize);
            int maxCX = (int)Float.Floor((px + halfW) * InvCellSize);
            int minCY = (int)Float.Floor((py - halfH) * InvCellSize);
            int maxCY = (int)Float.Floor((py + halfH) * InvCellSize);

            for (int cx = minCX; cx <= maxCX; cx++)
            {
                for (int cy = minCY; cy <= maxCY; cy++)
                {
                    long key = ((long)cx << 32) | (uint)cy;
                    if (!grid.TryGetValue(key, out var list))
                    {
                        list = RentList();
                        grid[key] = list;
                        usedKeys.Add(key);
                    }
                    list.Add(entity.Id);
                }
            }
        }
    }

    private static void ReturnGrid()
    {
        var grid = Grid;
        var pool = ListPool;
        var usedKeys = UsedKeys;
        // Walk the contiguous used-keys list instead of enumerating the dict's
        // internal buckets (which visits empty slots + incurs enumerator overhead).
        // Key → list lookup is a single hash probe; the whole thing fits in cache.
        var keys = usedKeys;
        for (int i = 0, n = keys.Count; i < n; i++)
        {
            if (grid.TryGetValue(keys[i], out var list))
            {
                list.Clear();
                pool.Add(list);
            }
        }
        grid.Clear();
        usedKeys.Clear();
    }

    private static List<int> RentList()
    {
        var pool = ListPool;
        if (pool.Count > 0)
        {
            var list = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return list;
        }
        return new List<int>(8);
    }

    // ══════════════════════════════════════════════
    //  Sensor overlap processing
    // ══════════════════════════════════════════════

    private static void ProcessSensors(EntityWorld state)
    {
        var grid = Grid;

        foreach (var sensorEntity in state.Filter<Area2D, Transform2D, CollisionShape2D>())
        {
            ref var area = ref state.GetComponent<Area2D>(sensorEntity);
            if (!area.Monitoring) continue;

            ref var sensorShape = ref state.GetComponent<CollisionShape2D>(sensorEntity);
            if (sensorShape.Disabled) continue;

            ref var sensorTransform = ref state.GetComponent<Transform2D>(sensorEntity);
            var sensorPos = sensorTransform.GlobalPosition;
            Float sx = sensorPos.X + sensorShape.Position.X;
            Float sy = sensorPos.Y + sensorShape.Position.Y;

            // Get collision filter
            uint sensorMask = area.CollisionMask;

            // Compute sensor AABB and query grid cells
            Float sensorRadius = GetBoundingRadius(ref sensorShape);
            int minCX = (int)Float.Floor((sx - sensorRadius) * InvCellSize);
            int maxCX = (int)Float.Floor((sx + sensorRadius) * InvCellSize);
            int minCY = (int)Float.Floor((sy - sensorRadius) * InvCellSize);
            int maxCY = (int)Float.Floor((sy + sensorRadius) * InvCellSize);

            // Collect candidate entity IDs. Dedup via a thread-static HashSet — the previous
            // impl used a stack-allocated Span<int> with linear scan, which is O(N²) and
            // silently truncated past 64 candidates. Profile showed this loop dominated
            // ProcessSensors (~20–30 % of per-tick sim work) under dense grid loads.
            var candidates = EntityBuffer;
            candidates.Clear();
            var visited = VisitedSet;
            visited.Clear();

            for (int cx = minCX; cx <= maxCX; cx++)
            {
                for (int cy = minCY; cy <= maxCY; cy++)
                {
                    long key = ((long)cx << 32) | (uint)cy;
                    if (!grid.TryGetValue(key, out var cell)) continue;

                    for (int i = 0; i < cell.Count; i++)
                    {
                        int candidateId = cell[i];
                        if (candidateId == sensorEntity.Id) continue;

                        if (!visited.Add(candidateId)) continue;

                        candidates.Add(new Entity(candidateId));
                    }
                }
            }

            // Sort candidates by entity ID for deterministic output
            candidates.Sort((a, b) => a.Id.CompareTo(b.Id));

            // Build new overlap list
            var newOverlaps = new List8<int>();
            byte enteredMask = 0;

            for (int i = 0; i < candidates.Count && newOverlaps.Count < 8; i++)
            {
                var candidate = candidates[i];
                if (!state.HasComponent<CollisionShape2D>(candidate)) continue;
                if (!state.HasComponent<Transform2D>(candidate)) continue;

                // Collision layer filtering: sensor mask must match target layer
                uint targetLayer = GetCollisionLayer(state, candidate);
                if ((sensorMask & targetLayer) == 0) continue;

                ref var targetShape = ref state.GetComponent<CollisionShape2D>(candidate);
                ref var targetTransform = ref state.GetComponent<Transform2D>(candidate);

                if (ShapesOverlap(
                    sx, sy, ref sensorShape,
                    targetTransform.GlobalPosition.X + targetShape.Position.X,
                    targetTransform.GlobalPosition.Y + targetShape.Position.Y,
                    ref targetShape))
                {
                    int idx = newOverlaps.Count;
                    newOverlaps.Add(candidate.Id);

                    // Check if this is a new entry (wasn't in previous frame's list)
                    bool wasPresent = false;
                    for (int j = 0; j < area.OverlappingEntities.Count; j++)
                    {
                        if (area.OverlappingEntities[j] == candidate.Id)
                        {
                            wasPresent = true;
                            break;
                        }
                    }
                    if (!wasPresent)
                        enteredMask |= (byte)(1 << idx);
                }
            }

            // Compute exited mask (entities in old list but not in new)
            byte exitedMask = 0;
            for (int j = 0; j < area.OverlappingEntities.Count; j++)
            {
                int oldId = area.OverlappingEntities[j];
                bool stillPresent = false;
                for (int k = 0; k < newOverlaps.Count; k++)
                {
                    if (newOverlaps[k] == oldId) { stillPresent = true; break; }
                }
                if (!stillPresent)
                    exitedMask |= (byte)(1 << j);
            }

            area.OverlappingEntities = newOverlaps;
            area.EnteredMask = enteredMask;
            area.ExitedMask = exitedMask;
            area.HasOverlappingBodies = newOverlaps.Count > 0;
        }
    }

    // ══════════════════════════════════════════════
    //  Narrowphase (deterministic Fixed64 math)
    // ══════════════════════════════════════════════

    private static bool ShapesOverlap(
        Float ax, Float ay, ref CollisionShape2D shapeA,
        Float bx, Float by, ref CollisionShape2D shapeB)
    {
        // Circle vs Circle
        if (shapeA.Type == CollisionShapeType.Circle && shapeB.Type == CollisionShapeType.Circle)
        {
            Float r = shapeA.Circle.Radius + shapeB.Circle.Radius;
            Float dx = ax - bx, dy = ay - by;
            return dx * dx + dy * dy <= r * r;
        }

        // Circle vs Rectangle (either order)
        if (shapeA.Type == CollisionShapeType.Circle && shapeB.Type == CollisionShapeType.Rectangle)
            return CircleRectOverlap(ax, ay, shapeA.Circle.Radius, bx, by, ref shapeB.Rectangle);
        if (shapeA.Type == CollisionShapeType.Rectangle && shapeB.Type == CollisionShapeType.Circle)
            return CircleRectOverlap(bx, by, shapeB.Circle.Radius, ax, ay, ref shapeA.Rectangle);

        // Rectangle vs Rectangle
        if (shapeA.Type == CollisionShapeType.Rectangle && shapeB.Type == CollisionShapeType.Rectangle)
        {
            Float hw1 = shapeA.Rectangle.Size.X * (Float)0.5f;
            Float hh1 = shapeA.Rectangle.Size.Y * (Float)0.5f;
            Float hw2 = shapeB.Rectangle.Size.X * (Float)0.5f;
            Float hh2 = shapeB.Rectangle.Size.Y * (Float)0.5f;
            return Float.Abs(ax - bx) <= hw1 + hw2 &&
                   Float.Abs(ay - by) <= hh1 + hh2;
        }

        // Capsule: treat as circle with bounding radius (conservative)
        Float radiusA = GetBoundingRadius(ref shapeA);
        Float radiusB = GetBoundingRadius(ref shapeB);
        Float ddx = ax - bx, ddy = ay - by;
        Float totalR = radiusA + radiusB;
        return ddx * ddx + ddy * ddy <= totalR * totalR;
    }

    private static bool CircleRectOverlap(
        Float cx, Float cy, Float cr,
        Float rx, Float ry, ref RectangleShape2D rect)
    {
        Float hw = rect.Size.X * (Float)0.5f;
        Float hh = rect.Size.Y * (Float)0.5f;
        // Clamp circle center to rect, then check distance
        Float closestX = Float.Clamp(cx, rx - hw, rx + hw);
        Float closestY = Float.Clamp(cy, ry - hh, ry + hh);
        Float dx = cx - closestX, dy = cy - closestY;
        return dx * dx + dy * dy <= cr * cr;
    }

    private static Float GetBoundingRadius(ref CollisionShape2D shape)
    {
        if (shape.Type == CollisionShapeType.Circle)
            return shape.Circle.Radius;
        if (shape.Type == CollisionShapeType.Rectangle)
        {
            Float hw = shape.Rectangle.Size.X * (Float)0.5f;
            Float hh = shape.Rectangle.Size.Y * (Float)0.5f;
            return Float.Sqrt(hw * hw + hh * hh);
        }
        if (shape.Type == CollisionShapeType.Capsule)
            return shape.Capsule.Height * (Float)0.5f + shape.Capsule.Radius;
        return Float.One;
    }

    private static uint GetCollisionLayer(EntityWorld state, Entity entity)
    {
        if (state.HasComponent<StaticBody2D>(entity))
            return state.GetComponent<StaticBody2D>(entity).CollisionLayer;
        if (state.HasComponent<CharacterBody2D>(entity))
            return state.GetComponent<CharacterBody2D>(entity).CollisionLayer;
        if (state.HasComponent<RigidBody2D>(entity))
            return state.GetComponent<RigidBody2D>(entity).CollisionLayer;
        if (state.HasComponent<Area2D>(entity))
            return state.GetComponent<Area2D>(entity).CollisionLayer;
        return 1; // default layer
    }
}
