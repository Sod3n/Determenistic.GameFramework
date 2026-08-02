using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;

using CDT = Deterministic.GameFramework.CDT;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Converts physics obstacles into constrained polygons for CDT triangulation.
/// Each obstacle becomes an inflated polygon (Minkowski sum with agent radius).
/// The world boundary becomes a CCW rectangle. Obstacles use CW winding.
/// Vertices are deduplicated via quantized coordinate hashing to prevent
/// CDT <see cref="CDT.DuplicateVertexError"/>.
/// </summary>
internal static class ObstaclePolygonExtractor
{
    /// <summary>Number of sides used to approximate circles and NavigationObstacle2D radii.</summary>
    private const int CircleSegments = 8;

    /// <summary>Number of vertices per semicircle cap on capsule shapes.</summary>
    private const int CapsuleCapSegments = 4;

    /// <summary>
    /// Quantization scale for vertex deduplication.
    /// Vertices within 1/100th of a unit are considered identical.
    /// </summary>
    private const int QuantizationScale = 100;

    /// <summary>
    /// Extract obstacle polygons from the ECS world as vertices and constraint edges
    /// suitable for CDT insertion.
    /// </summary>
    /// <param name="state">The ECS world containing obstacle entities.</param>
    /// <param name="world">Navigation world configuration (bounds, agent radius, masks).</param>
    /// <returns>Combined vertex list and constraint edge list.</returns>
    public static (List<Vector2> Vertices, List<CDT.Edge> Edges) ExtractObstaclePolygons(
        EntityWorld state, ref NavigationWorld2D world)
    {
        var vertices = new List<Vector2>();
        var edges = new List<CDT.Edge>();
        var vertexMap = new Dictionary<long, int>();

        var agentRadius = world.AgentRadius;

        // 1. World boundary polygon (CCW winding)
        AddWorldBoundary(vertices, edges, vertexMap, ref world);

        // 2. StaticBody2D obstacles
        foreach (var entity in state.Filter<StaticBody2D, Transform2D, CollisionShape2D>())
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            if ((body.CollisionLayer & world.ObstacleMask) == 0) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
            if (shape.Disabled) continue;

            AddShapePolygon(vertices, edges, vertexMap, ref transform, ref shape, agentRadius);
        }

        // 3. RigidBody2D obstacles (optional)
        if (world.IncludeDynamicBodies)
        {
            foreach (var entity in state.Filter<RigidBody2D, Transform2D, CollisionShape2D>())
            {
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                if ((body.CollisionLayer & world.ObstacleMask) == 0) continue;

                ref var transform = ref state.GetComponent<Transform2D>(entity);
                ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
                if (shape.Disabled) continue;

                AddShapePolygon(vertices, edges, vertexMap, ref transform, ref shape, agentRadius);
            }
        }

        // 4. NavigationObstacle2D entities
        foreach (var entity in state.Filter<NavigationObstacle2D, Transform2D>())
        {
            ref var obstacle = ref state.GetComponent<NavigationObstacle2D>(entity);
            if (!obstacle.AffectsNavigationMesh) continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            var inflatedRadius = obstacle.Radius + agentRadius;
            var center = transform.GlobalPosition;

            AddCirclePolygon(vertices, edges, vertexMap, center, inflatedRadius, CircleSegments);
        }

        return (vertices, edges);
    }

    /// <summary>
    /// Compute a deterministic hash from all obstacle positions and shapes.
    /// Used to detect when the obstacle layout has changed and a rebake is needed.
    /// </summary>
    public static int ComputeObstacleHash(EntityWorld state, ref NavigationWorld2D world)
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

                hash = hash * 31 + entity.Id;
                hash = HashFloat(hash, transform.GlobalPosition.X);
                hash = HashFloat(hash, transform.GlobalPosition.Y);
                hash = HashFloat(hash, transform.GlobalRotation);
                hash = HashShape(hash, ref shape);
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

                    hash = hash * 31 + entity.Id;
                    hash = HashFloat(hash, transform.GlobalPosition.X);
                    hash = HashFloat(hash, transform.GlobalPosition.Y);
                    hash = HashFloat(hash, transform.GlobalRotation);
                    hash = HashShape(hash, ref shape);
                }
            }

            // NavigationObstacle2D
            foreach (var entity in state.Filter<NavigationObstacle2D, Transform2D>())
            {
                ref var obstacle = ref state.GetComponent<NavigationObstacle2D>(entity);
                if (!obstacle.AffectsNavigationMesh) continue;

                ref var transform = ref state.GetComponent<Transform2D>(entity);

                hash = hash * 31 + entity.Id;
                hash = HashFloat(hash, transform.GlobalPosition.X);
                hash = HashFloat(hash, transform.GlobalPosition.Y);
                hash = HashFloat(hash, obstacle.Radius);
            }

            return hash;
        }
    }

    /// <summary>Deterministic hash for a fixed-point Float (uses raw bits, not GetHashCode).</summary>
    private static int HashFloat(int hash, Float value)
    {
        long raw = value.RawValue;
        return hash * 31 + (int)raw ^ (int)(raw >> 32);
    }

    /// <summary>Deterministic hash for a CollisionShape2D (uses raw field values).</summary>
    private static int HashShape(int hash, ref CollisionShape2D shape)
    {
        hash = hash * 31 + shape.Type.Value;
        hash = HashFloat(hash, shape.Position.X);
        hash = HashFloat(hash, shape.Position.Y);
        hash = HashFloat(hash, shape.Rotation);
        if (shape.Type == CollisionShapeType.Circle)
        {
            hash = HashFloat(hash, shape.Circle.Radius);
        }
        else if (shape.Type == CollisionShapeType.Rectangle)
        {
            hash = HashFloat(hash, shape.Rectangle.Size.X);
            hash = HashFloat(hash, shape.Rectangle.Size.Y);
        }
        else if (shape.Type == CollisionShapeType.Capsule)
        {
            hash = HashFloat(hash, shape.Capsule.Radius);
            hash = HashFloat(hash, shape.Capsule.Height);
        }
        return hash;
    }

    // ──────────────────────────────────────────────
    //  World boundary
    // ──────────────────────────────────────────────

    /// <summary>
    /// Add the world boundary as a CCW rectangle (4 vertices, 4 edges).
    /// This defines the outer walkable area.
    /// </summary>
    private static void AddWorldBoundary(
        List<Vector2> vertices, List<CDT.Edge> edges,
        Dictionary<long, int> vertexMap, ref NavigationWorld2D world)
    {
        var min = world.BoundsMin;
        var max = world.BoundsMax;

        // CCW winding: bottom-left -> bottom-right -> top-right -> top-left
        var v0 = new Vector2(min.X, min.Y);
        var v1 = new Vector2(max.X, min.Y);
        var v2 = new Vector2(max.X, max.Y);
        var v3 = new Vector2(min.X, max.Y);

        int i0 = AddVertex(vertices, vertexMap, v0);
        int i1 = AddVertex(vertices, vertexMap, v1);
        int i2 = AddVertex(vertices, vertexMap, v2);
        int i3 = AddVertex(vertices, vertexMap, v3);

        edges.Add(new CDT.Edge(i0, i1));
        edges.Add(new CDT.Edge(i1, i2));
        edges.Add(new CDT.Edge(i2, i3));
        edges.Add(new CDT.Edge(i3, i0));
    }

    // ──────────────────────────────────────────────
    //  Shape dispatch
    // ──────────────────────────────────────────────

    /// <summary>
    /// Convert a CollisionShape2D into an inflated obstacle polygon and add it
    /// to the vertex/edge lists. Obstacles use CW winding.
    /// </summary>
    private static void AddShapePolygon(
        List<Vector2> vertices, List<CDT.Edge> edges,
        Dictionary<long, int> vertexMap,
        ref Transform2D transform, ref CollisionShape2D shape,
        Float agentRadius)
    {
        var worldPos = transform.GlobalPosition + shape.Position;

        if (shape.Type == CollisionShapeType.Circle)
        {
            var inflatedRadius = shape.Circle.Radius + agentRadius;
            AddCirclePolygon(vertices, edges, vertexMap, worldPos, inflatedRadius, CircleSegments);
        }
        else if (shape.Type == CollisionShapeType.Rectangle)
        {
            AddRectanglePolygon(vertices, edges, vertexMap,
                worldPos, shape.Rectangle.Size, transform.GlobalRotation + shape.Rotation, agentRadius);
        }
        else if (shape.Type == CollisionShapeType.Capsule)
        {
            AddCapsulePolygon(vertices, edges, vertexMap,
                worldPos, shape.Capsule.Radius, shape.Capsule.Height,
                transform.GlobalRotation + shape.Rotation, agentRadius);
        }
        // Unknown shape types are silently skipped
    }

    // ──────────────────────────────────────────────
    //  Circle polygon
    // ──────────────────────────────────────────────

    /// <summary>
    /// Approximate a circle as an N-sided polygon. CW winding for obstacle holes.
    /// </summary>
    private static void AddCirclePolygon(
        List<Vector2> vertices, List<CDT.Edge> edges,
        Dictionary<long, int> vertexMap,
        Vector2 center, Float radius, int segments)
    {
        var indices = new int[segments];
        var angleStep = Float.TwoPi / (Float)segments;

        for (int i = 0; i < segments; i++)
        {
            // CW winding: negate the angle so vertices go clockwise
            var angle = -(angleStep * (Float)i);
            var x = center.X + radius * Float.Cos(angle);
            var y = center.Y + radius * Float.Sin(angle);
            indices[i] = AddVertex(vertices, vertexMap, new Vector2(x, y));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            edges.Add(new CDT.Edge(indices[i], indices[next]));
        }
    }

    // ──────────────────────────────────────────────
    //  Rectangle polygon
    // ──────────────────────────────────────────────

    /// <summary>
    /// Convert a rectangle obstacle into an inflated polygon.
    /// For axis-aligned rectangles (rotation == 0), expands half-extents by agentRadius.
    /// For rotated rectangles, uses the AABB of the rotated rectangle expanded by agentRadius.
    /// This slightly over-approximates rotated obstacles but is simple and correct.
    /// CW winding for obstacle holes.
    /// </summary>
    private static void AddRectanglePolygon(
        List<Vector2> vertices, List<CDT.Edge> edges,
        Dictionary<long, int> vertexMap,
        Vector2 center, Vector2 size, Float rotation, Float agentRadius)
    {
        var halfW = size.X / (Float)2;
        var halfH = size.Y / (Float)2;

        Float halfExtentX, halfExtentY;

        if (rotation != (Float)0)
        {
            // Rotated rectangle: compute AABB of rotated corners then inflate
            // Same approach as PhysicsNavMeshBaker.ComputeInflatedAABB
            var cos = Float.Abs(Float.Cos(rotation));
            var sin = Float.Abs(Float.Sin(rotation));
            halfExtentX = halfW * cos + halfH * sin + agentRadius;
            halfExtentY = halfW * sin + halfH * cos + agentRadius;
        }
        else
        {
            // Axis-aligned: simply expand half-extents
            halfExtentX = halfW + agentRadius;
            halfExtentY = halfH + agentRadius;
        }

        // CW winding: bottom-left -> top-left -> top-right -> bottom-right
        var v0 = new Vector2(center.X - halfExtentX, center.Y - halfExtentY);
        var v1 = new Vector2(center.X - halfExtentX, center.Y + halfExtentY);
        var v2 = new Vector2(center.X + halfExtentX, center.Y + halfExtentY);
        var v3 = new Vector2(center.X + halfExtentX, center.Y - halfExtentY);

        int i0 = AddVertex(vertices, vertexMap, v0);
        int i1 = AddVertex(vertices, vertexMap, v1);
        int i2 = AddVertex(vertices, vertexMap, v2);
        int i3 = AddVertex(vertices, vertexMap, v3);

        edges.Add(new CDT.Edge(i0, i1));
        edges.Add(new CDT.Edge(i1, i2));
        edges.Add(new CDT.Edge(i2, i3));
        edges.Add(new CDT.Edge(i3, i0));
    }

    // ──────────────────────────────────────────────
    //  Capsule polygon
    // ──────────────────────────────────────────────

    /// <summary>
    /// Approximate a capsule as a polygon: 4 vertices for the rectangular sides
    /// plus semicircle caps (CapsuleCapSegments vertices per cap).
    /// The capsule is oriented vertically by default (height along Y axis),
    /// then rotated by the given rotation.
    /// CW winding for obstacle holes.
    /// </summary>
    private static void AddCapsulePolygon(
        List<Vector2> vertices, List<CDT.Edge> edges,
        Dictionary<long, int> vertexMap,
        Vector2 center, Float capsuleRadius, Float height,
        Float rotation, Float agentRadius)
    {
        var inflatedRadius = capsuleRadius + agentRadius;
        var halfBodyH = height / (Float)2 - capsuleRadius;
        if (halfBodyH < (Float)0) halfBodyH = (Float)0;

        // Total vertices = 2 sides * 1 + 2 caps * CapsuleCapSegments
        // Build polygon starting from right side of top, going CW:
        //   top cap (right to left) -> left side top -> bottom cap (left to right) -> right side bottom
        var polyVerts = new List<Vector2>();

        var cos = Float.Cos(rotation);
        var sin = Float.Sin(rotation);

        // Helper to rotate a local-space point around the center
        void AddRotatedPoint(Float lx, Float ly)
        {
            var wx = center.X + lx * cos - ly * sin;
            var wy = center.Y + lx * sin + ly * cos;
            polyVerts.Add(new Vector2(wx, wy));
        }

        // Top cap center is at local (0, +halfBodyH)
        // Arc from angle 0 (right) going CW to angle PI (left)
        // CW means decreasing angle in standard math, but we want to go from right to left
        // across the top, so angles go from 0 to +PI (but CW polygon winding means we emit
        // them in this order: right side first)
        for (int i = 0; i <= CapsuleCapSegments; i++)
        {
            // Angle from 0 to PI across the top cap
            var t = (Float)i / (Float)CapsuleCapSegments;
            var angle = Float.Pi * t;
            // Top cap at (0, +halfBodyH), arc goes from (inflatedRadius, halfBodyH) to (-inflatedRadius, halfBodyH)
            // In CW winding we go right-to-left across the top
            var lx = inflatedRadius * Float.Cos(angle);
            var ly = halfBodyH + inflatedRadius * Float.Sin(angle);
            AddRotatedPoint(lx, ly);
        }

        // Bottom cap center is at local (0, -halfBodyH)
        // Arc from PI (left) going CW to 2*PI (right)
        for (int i = 0; i <= CapsuleCapSegments; i++)
        {
            var t = (Float)i / (Float)CapsuleCapSegments;
            var angle = Float.Pi + Float.Pi * t;
            var lx = inflatedRadius * Float.Cos(angle);
            var ly = -halfBodyH + inflatedRadius * Float.Sin(angle);
            AddRotatedPoint(lx, ly);
        }

        // Add deduplicated vertices and CW edges
        var indexCount = polyVerts.Count;
        var indices = new int[indexCount];
        for (int i = 0; i < indexCount; i++)
        {
            indices[i] = AddVertex(vertices, vertexMap, polyVerts[i]);
        }

        for (int i = 0; i < indexCount; i++)
        {
            int next = (i + 1) % indexCount;
            // Skip degenerate edges where deduplication merged two adjacent vertices
            if (indices[i] != indices[next])
            {
                edges.Add(new CDT.Edge(indices[i], indices[next]));
            }
        }
    }

    // ──────────────────────────────────────────────
    //  Vertex deduplication
    // ──────────────────────────────────────────────

    /// <summary>
    /// Add a vertex to the list, deduplicating by quantized coordinates.
    /// Returns the index of the existing or newly added vertex.
    /// </summary>
    private static int AddVertex(
        List<Vector2> vertices, Dictionary<long, int> vertexMap, Vector2 v)
    {
        long key = QuantizeVertex(v);
        if (vertexMap.TryGetValue(key, out int existingIndex))
            return existingIndex;

        int index = vertices.Count;
        vertices.Add(v);
        vertexMap[key] = index;
        return index;
    }

    /// <summary>
    /// Pack a vertex position into a single long for deduplication.
    /// Multiplies by QuantizationScale (100), casts to int, then packs x|y.
    /// </summary>
    private static long QuantizeVertex(Vector2 v)
    {
        int qx = (int)(v.X * (Float)QuantizationScale);
        int qy = (int)(v.Y * (Float)QuantizationScale);
        return ((long)qx << 32) | (uint)qy;
    }
}
