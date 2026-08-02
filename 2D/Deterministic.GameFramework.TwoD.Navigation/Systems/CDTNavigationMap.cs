using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Deterministic.GameFramework.Detour;
using Deterministic.GameFramework.Utils.Logging;
using Float = Deterministic.GameFramework.Types.Float;
using Vector2 = Deterministic.GameFramework.Types.Vector2;
using Vector3 = Deterministic.GameFramework.Types.Vector3;
using CDT = Deterministic.GameFramework.CDT;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Navigation map built on a Constrained Delaunay Triangulation (CDT).
/// Uses Detour for pathfinding (A*, funnel/straight-path, raycast) on the CDT mesh.
/// </summary>
public class CDTNavigationMap
{
    // -- CDT state --

    private List<Vector2> _vertices = new();
    private List<CDT.Triangle> _triangles = new();
    private HashSet<CDT.Edge> _constraintEdges = new();
    private Vector2[] _centroids = Array.Empty<Vector2>();
    private Dictionary<long, List<int>> _spatialGrid = new();
    private Float _cellSize = (Float)64;

    /// <summary>Spatial grid cell size. Used as default margin for IsPathClear.</summary>
    public Float CellSize => _cellSize;

    // Object pool for spatial grid int lists
    private readonly List<List<int>> _intListPool = new();

    /// <summary>Whether the map has been built and is ready for queries.</summary>
    public bool IsBuilt { get; private set; }

    /// <summary>
    /// Clear all built data so the map is rebuilt from scratch on the next bake.
    /// Called after state restore to ensure deterministic navmesh reconstruction.
    /// </summary>
    public void Clear()
    {
        _vertices.Clear();
        _triangles.Clear();
        _constraintEdges.Clear();
        _centroids = Array.Empty<Vector2>();
        foreach (var list in _spatialGrid.Values)
        {
            list.Clear();
            _intListPool.Add(list);
        }
        _spatialGrid.Clear();
        _dtNavMesh = null;
        _dtNavMeshQuery = null;
        IsBuilt = false;
    }

    /// <summary>Number of triangles in the mesh.</summary>
    public int TriangleCount => _triangles.Count;

    /// <summary>
    /// Computes a deterministic hash of the built mesh geometry (vertices + triangles).
    /// Used to detect client/server map divergence via the ECS-stored MapHash.
    /// </summary>
    public int ComputeMapHash()
    {
        if (!IsBuilt) return 0;
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + _vertices.Count;
            hash = hash * 31 + _triangles.Count;
            for (int i = 0; i < _vertices.Count; i++)
            {
                hash = hash * 31 + _vertices[i].X.RawValue.GetHashCode();
                hash = hash * 31 + _vertices[i].Y.RawValue.GetHashCode();
            }
            for (int i = 0; i < _triangles.Count; i++)
            {
                var t = _triangles[i];
                hash = hash * 31 + t.V0;
                hash = hash * 31 + t.V1;
                hash = hash * 31 + t.V2;
            }
            return hash;
        }
    }

    /// <summary>Read-only access to triangulation vertices.</summary>
    public IReadOnlyList<Vector2> Vertices => _vertices;

    /// <summary>Read-only access to triangulation triangles.</summary>
    public IReadOnlyList<CDT.Triangle> Triangles => _triangles;

    /// <summary>Read-only access to constraint edges.</summary>
    public IReadOnlyCollection<CDT.Edge> ConstraintEdges => _constraintEdges;

    // -- Detour state --

    private DtNavMesh? _dtNavMesh;
    private DtNavMeshQuery? _dtNavMeshQuery;

    /// <summary>The underlying Detour navmesh, used by DtCrowd.</summary>
    public DtNavMesh? DetourNavMesh => _dtNavMesh;
    private readonly IDtQueryFilter _dtFilter = new DtQueryDefaultFilter();

    // Scratch for ComputeSmoothedPath
    private long[]? _polyPathScratch;
    private DtStraightPath[]? _straightPathScratch;

    // ------------------------------------------------------
    //  Pathfinding scratch — pooled per-CDTNavigationMap instance
    //  (each match-world owns its own Map, so these never
    //   cross match boundaries). All are Reset/Clear-ed
    //   to a clean state before every pathfinding call, so
    //   pooled reuse is bit-identical to fresh allocation.
    // ------------------------------------------------------
    private readonly CDTMinHeap _heapScratch = new(16);
    private Float[] _gScoreScratch = Array.Empty<Float>();
    private Float[] _fScoreScratch = Array.Empty<Float>();
    private int[] _cameFromScratch = Array.Empty<int>();
    private bool[] _inClosedScratch = Array.Empty<bool>();
    private readonly List<int> _triPathScratch = new(64);
    private readonly List<(Vector2 Left, Vector2 Right)> _portalsScratch = new(66);

    // ======================================================
    //  2D <-> 3D helpers
    // ======================================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 To3D(Vector2 v) => new Vector3(v.X, (Float)0, v.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 To2D(Vector3 v) => new Vector2(v.X, v.Z);

    // ======================================================
    //  Build
    // ======================================================

    /// <summary>
    /// Build the navigation mesh from vertices and constraint edges using CDT.
    /// Constraint edges represent obstacle boundaries that cannot be crossed.
    /// After CDT triangulation, a Detour navmesh is constructed for pathfinding.
    /// </summary>
    /// <summary>Maximum edge length for mesh refinement. Steiner points are inserted
    /// on a grid at this spacing to prevent huge triangles in open areas.</summary>
    private Float _maxEdgeLength = (Float)10;

    public void Build(List<Vector2> vertices, List<CDT.Edge> constraintEdges)
    {
        // 1. Generate Steiner points to refine the mesh.
        //    Without refinement, CDT produces huge boundary triangles that break
        //    Detour's edge-midpoint cost estimation and the funnel algorithm.
        var refinedVertices = new List<Vector2>(vertices);
        AddSteinerPoints(refinedVertices, constraintEdges, _maxEdgeLength);

        // 2. Create and run CDT
        var cdt = new CDT.Triangulation(
            CDT.VertexInsertionOrder.AsProvided,
            CDT.IntersectingConstraintEdges.TryResolve,
            (Float)0.001f);

        cdt.InsertVertices(refinedVertices);
        cdt.InsertEdges(constraintEdges);
        cdt.EraseOuterTrianglesAndHoles();

        // 2. Copy results into internal arrays
        _vertices = new List<Vector2>(cdt.Vertices);
        _triangles = new List<CDT.Triangle>(cdt.Triangles);
        _constraintEdges = new HashSet<CDT.Edge>(cdt.FixedEdges);

        // 3. Precompute centroids
        _centroids = new Vector2[_triangles.Count];
        var three = (Float)3;
        for (int i = 0; i < _triangles.Count; i++)
        {
            var t = _triangles[i];
            var v0 = _vertices[t.V0];
            var v1 = _vertices[t.V1];
            var v2 = _vertices[t.V2];
            _centroids[i] = new Vector2(
                (v0.X + v1.X + v2.X) / three,
                (v0.Y + v1.Y + v2.Y) / three);
        }

        // 4. Build spatial grid (kept for FindTriangle fallback)
        RebuildSpatialGrid();

        // 5. Build Detour navmesh from CDT triangulation
        BuildDetourNavMesh();

        // 6. Mark as ready
        IsBuilt = true;
    }

    // ======================================================
    //  Mesh refinement (Steiner point insertion)
    // ======================================================

    /// <summary>
    /// Insert Steiner points on a grid to prevent huge triangles in open areas.
    /// Points inside obstacle polygons (defined by constraint edges) are excluded.
    /// </summary>
    private static void AddSteinerPoints(List<Vector2> vertices, List<CDT.Edge> constraintEdges, Float spacing)
    {
        if (vertices.Count < 3 || spacing <= (Float)0) return;

        // Compute bounds from existing vertices
        Float minX = vertices[0].X, maxX = vertices[0].X;
        Float minY = vertices[0].Y, maxY = vertices[0].Y;
        for (int i = 1; i < vertices.Count; i++)
        {
            if (vertices[i].X < minX) minX = vertices[i].X;
            if (vertices[i].X > maxX) maxX = vertices[i].X;
            if (vertices[i].Y < minY) minY = vertices[i].Y;
            if (vertices[i].Y > maxY) maxY = vertices[i].Y;
        }

        // Build list of obstacle polygons from constraint edges.
        // Each closed loop of constraint edges is a polygon.
        // We use the edges directly for point-in-polygon testing.
        var obstaclePolys = ExtractClosedPolygons(vertices, constraintEdges);

        // Quantization scale for dedup with existing vertices
        var existingSet = new HashSet<long>();
        foreach (var v in vertices)
            existingSet.Add(QuantizePoint(v, spacing));

        // Generate grid points, skipping those inside obstacles or duplicates
        Float halfSpace = spacing / (Float)2;
        for (Float x = minX + halfSpace; x < maxX; x += spacing)
        {
            for (Float y = minY + halfSpace; y < maxY; y += spacing)
            {
                var pt = new Vector2(x, y);
                long key = QuantizePoint(pt, spacing);
                if (existingSet.Contains(key)) continue;

                // Skip if inside any obstacle polygon
                bool insideObstacle = false;
                for (int p = 0; p < obstaclePolys.Count; p++)
                {
                    if (PointInConvexPolygon(pt, obstaclePolys[p], vertices))
                    {
                        insideObstacle = true;
                        break;
                    }
                }
                if (insideObstacle) continue;

                existingSet.Add(key);
                vertices.Add(pt);
            }
        }
    }

    private static long QuantizePoint(Vector2 p, Float spacing)
    {
        // Quantize to half-spacing grid to catch near-duplicates
        int qx = (int)(p.X / spacing * (Float)2);
        int qy = (int)(p.Y / spacing * (Float)2);
        return ((long)qx << 32) | (uint)qy;
    }

    /// <summary>
    /// Extract closed polygons from constraint edges. Each obstacle is a closed
    /// loop of edges. The world boundary is also a closed polygon (first 4 vertices).
    /// We skip the world boundary (it's the outermost polygon, index 0-3).
    /// </summary>
    private static List<List<int>> ExtractClosedPolygons(List<Vector2> vertices, List<CDT.Edge> edges)
    {
        var result = new List<List<int>>();

        // Build adjacency: for each vertex, which edges connect to it
        var adj = new Dictionary<int, List<int>>();
        foreach (var e in edges)
        {
            if (!adj.TryGetValue(e.V1, out var list1))
                adj[e.V1] = list1 = new List<int>();
            list1.Add(e.V2);

            if (!adj.TryGetValue(e.V2, out var list2))
                adj[e.V2] = list2 = new List<int>();
            list2.Add(e.V1);
        }

        // Walk each closed loop
        var visited = new HashSet<long>();
        foreach (var e in edges)
        {
            long edgeKey = ((long)Math.Min(e.V1, e.V2) << 32) | (uint)Math.Max(e.V1, e.V2);
            if (!visited.Add(edgeKey)) continue;

            // Try to walk a closed polygon starting from this edge
            var poly = new List<int> { e.V1, e.V2 };
            int current = e.V2;
            int prev = e.V1;
            bool closed = false;

            for (int step = 0; step < 100; step++) // safety limit
            {
                if (!adj.TryGetValue(current, out var neighbors)) break;

                int next = -1;
                foreach (var n in neighbors)
                {
                    if (n == prev) continue;
                    long nKey = ((long)Math.Min(current, n) << 32) | (uint)Math.Max(current, n);
                    if (visited.Contains(nKey) && n != e.V1) continue;
                    next = n;
                    break;
                }

                if (next == -1) break;
                if (next == e.V1) { closed = true; break; }

                long nextKey = ((long)Math.Min(current, next) << 32) | (uint)Math.Max(current, next);
                visited.Add(nextKey);
                poly.Add(next);
                prev = current;
                current = next;
            }

            if (closed && poly.Count >= 3)
            {
                // Skip the world boundary (typically vertices 0-3)
                bool isWorldBoundary = poly.Count == 4 &&
                    poly[0] < 4 && poly[1] < 4 && poly[2] < 4 && poly[3] < 4;
                if (!isWorldBoundary)
                    result.Add(poly);
            }
        }

        return result;
    }

    /// <summary>
    /// Check if point is inside a convex polygon using cross-product winding test.
    /// Works for both CW and CCW polygons.
    /// </summary>
    private static bool PointInConvexPolygon(Vector2 pt, List<int> polyIndices, List<Vector2> vertices)
    {
        int n = polyIndices.Count;
        if (n < 3) return false;

        bool? positive = null;
        for (int i = 0; i < n; i++)
        {
            var a = vertices[polyIndices[i]];
            var b = vertices[polyIndices[(i + 1) % n]];
            var cross = (b.X - a.X) * (pt.Y - a.Y) - (b.Y - a.Y) * (pt.X - a.X);
            if (cross == (Float)0) continue;
            bool isPositive = cross > (Float)0;
            if (positive == null) positive = isPositive;
            else if (positive != isPositive) return false;
        }

        return positive != null;
    }

    // ======================================================
    //  Detour navmesh construction
    // ======================================================

    private void BuildDetourNavMesh()
    {
        int vertCount = _vertices.Count;
        int polyCount = _triangles.Count;

        if (vertCount < 3 || polyCount < 1)
        {
            _dtNavMesh = null;
            _dtNavMeshQuery = null;
            return;
        }

        // Compute bounds from all vertices (in 3D: X=X, Y=0, Z=Y)
        Float minX = _vertices[0].X, maxX = _vertices[0].X;
        Float minZ = _vertices[0].Y, maxZ = _vertices[0].Y;
        for (int i = 1; i < vertCount; i++)
        {
            var v = _vertices[i];
            if (v.X < minX) minX = v.X;
            if (v.X > maxX) maxX = v.X;
            if (v.Y < minZ) minZ = v.Y;
            if (v.Y > maxZ) maxZ = v.Y;
        }

        // Add padding
        Float pad = (Float)1;
        var bmin = new Vector3(minX - pad, (Float)(-1), minZ - pad);
        var bmax = new Vector3(maxX + pad, (Float)1, maxZ + pad);

        // Cell size: scale based on bounds extent, at least 0.1
        Float extentX = maxX - minX + pad * (Float)2;
        Float extentZ = maxZ - minZ + pad * (Float)2;
        Float maxExtent = extentX > extentZ ? extentX : extentZ;
        Float cs = maxExtent / (Float)1000;
        if (cs < (Float)0.1f) cs = (Float)0.1f;
        Float ch = cs;

        // Quantize vertices: int[] with (x,y,z) per vertex
        int[] verts = new int[vertCount * 3];
        for (int i = 0; i < vertCount; i++)
        {
            var v = _vertices[i];
            verts[i * 3 + 0] = (int)((v.X - bmin.X) / cs);
            verts[i * 3 + 1] = 0; // height = 0 for 2D
            verts[i * 3 + 2] = (int)((v.Y - bmin.Z) / cs);
        }

        // Build polys array: [polyCount * 2 * nvp] where nvp = 3
        // For each triangle: [V0, V1, V2, neighbor0, neighbor1, neighbor2]
        const int nvp = 3;
        int[] polys = new int[polyCount * 2 * nvp];
        const int MESH_NULL_IDX = 0xffff;

        for (int i = 0; i < polyCount; i++)
        {
            var tri = _triangles[i];
            int baseIdx = i * 2 * nvp;

            // Vertex indices — reverse winding (CW) for Detour's left-handed coordinate system.
            // CDT produces CCW in right-handed 2D. Detour expects CW in right-handed
            // (which is CCW in its left-handed Y-up system).
            polys[baseIdx + 0] = tri.V0;
            polys[baseIdx + 1] = tri.V2; // swapped
            polys[baseIdx + 2] = tri.V1; // swapped

            // Neighbor indices must also be reordered to match the new edge order.
            // Original: edge0=V0→V1, edge1=V1→V2, edge2=V2→V0
            // Reversed: edge0=V0→V2 (was edge2), edge1=V2→V1 (was edge1), edge2=V1→V0 (was edge0)
            int[] slotRemap = { 2, 1, 0 }; // new slot → original CDT slot
            for (int slot = 0; slot < 3; slot++)
            {
                int cdtSlot = slotRemap[slot];
                int neighbor = tri.GetNeighbor(cdtSlot);
                int edgeV1 = tri.GetVertex(cdtSlot);
                int edgeV2 = tri.GetVertex((cdtSlot + 1) % 3);

                if (neighbor == CDT.CDTConstants.NoNeighbor
                    || _constraintEdges.Contains(new CDT.Edge(edgeV1, edgeV2)))
                {
                    polys[baseIdx + nvp + slot] = MESH_NULL_IDX;
                }
                else
                {
                    polys[baseIdx + nvp + slot] = neighbor;
                }
            }
        }

        // Diagnostic: verify CDT vs Detour connectivity
        #if DEBUG
        for (int i = 0; i < polyCount; i++)
        {
            var tri = _triangles[i];
            int b = i * 2 * nvp;
            int n0 = polys[b + 3], n1 = polys[b + 4], n2 = polys[b + 5];
            // Check CDT A* can traverse vs Detour can traverse
            for (int slot = 0; slot < 3; slot++)
            {
                int cdtNeighbor = tri.GetNeighbor(slot);
                int dtNeighbor = polys[b + nvp + slot];
                int ev1 = tri.GetVertex(slot);
                int ev2 = tri.GetVertex((slot + 1) % 3);
                bool isConstraint = _constraintEdges.Contains(new CDT.Edge(ev1, ev2));
                bool cdtBlocked = (cdtNeighbor == CDT.CDTConstants.NoNeighbor) || isConstraint;
                bool dtBlocked = (dtNeighbor == MESH_NULL_IDX);
                if (cdtBlocked != dtBlocked)
                {
                    ILogger.LogDebug($"MISMATCH tri={i} slot={slot} edge=({ev1},{ev2}) cdtN={cdtNeighbor} dtN={dtNeighbor} constraint={isConstraint}");
                }
            }
        }
        #endif

        // Poly flags and areas
        int[] polyFlags = new int[polyCount];
        int[] polyAreas = new int[polyCount];
        for (int i = 0; i < polyCount; i++)
        {
            polyFlags[i] = 1; // walkable
            polyAreas[i] = 0;
        }

        // Create DtNavMeshCreateParams
        var navParams = new DtNavMeshCreateParams
        {
            nvp = nvp,
            vertCount = vertCount,
            polyCount = polyCount,
            verts = verts,
            polys = polys,
            polyFlags = polyFlags,
            polyAreas = polyAreas,
            bmin = bmin,
            bmax = bmax,
            cs = cs,
            ch = ch,
            walkableHeight = (Float)2,
            walkableRadius = (Float)0,
            walkableClimb = (Float)0,
            buildBvTree = true,
        };

        // Build the mesh data
        DtMeshData meshData = DtNavMeshBuilder.CreateNavMeshData(navParams);
        if (meshData == null)
        {
            _dtNavMesh = null;
            _dtNavMeshQuery = null;
            return;
        }

        // Create DtNavMesh using the simpler Init(data, maxVertsPerPoly, flags) overload
        var dtNavMesh = new DtNavMesh();
        var status = dtNavMesh.Init(meshData, nvp, 0);
        if (status.Failed())
        {
            _dtNavMesh = null;
            _dtNavMeshQuery = null;
            return;
        }

        _dtNavMesh = dtNavMesh;
        _dtNavMeshQuery = new DtNavMeshQuery(_dtNavMesh);
    }

    // ======================================================
    //  FindTriangle
    // ======================================================

    /// <summary>
    /// Find the triangle containing the given world-space point.
    /// Returns >= 0 if on navmesh, -1 if not found.
    /// Uses Detour FindNearestPoly with fallback to spatial grid.
    /// </summary>
    public int FindTriangle(Vector2 point)
    {
        if (_dtNavMeshQuery != null)
        {
            var pos3 = To3D(point);
            var halfExtents = new Vector3((Float)2, (Float)1, (Float)2);
            var status = _dtNavMeshQuery.FindNearestPoly(pos3, halfExtents, _dtFilter,
                out long nearestRef, out _, out bool isOverPoly);
            if (status.Succeeded() && nearestRef != 0 && isOverPoly)
            {
                return DtDetour.DecodePolyIdPoly(nearestRef);
            }
        }

        // Fallback: spatial grid + point-in-triangle
        return FindTriangleFallback(point);
    }

    private int FindTriangleFallback(Vector2 point)
    {
        // First try spatial grid lookup for the exact cell
        long cellKey = GetCellKey(point);
        if (_spatialGrid.TryGetValue(cellKey, out var candidates))
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (PointInTriangle(point, candidates[i]))
                    return candidates[i];
            }
        }

        // Check neighboring cells (point might be on cell border)
        int cx = (int)(point.X / _cellSize);
        int cy = (int)(point.Y / _cellSize);
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                long neighborKey = PackCellKey(cx + dx, cy + dy);
                if (_spatialGrid.TryGetValue(neighborKey, out var neighborCandidates))
                {
                    for (int i = 0; i < neighborCandidates.Count; i++)
                    {
                        if (PointInTriangle(point, neighborCandidates[i]))
                            return neighborCandidates[i];
                    }
                }
            }
        }

        // Brute force fallback
        for (int i = 0; i < _triangles.Count; i++)
        {
            if (PointInTriangle(point, i))
                return i;
        }

        return -1;
    }

    // ======================================================
    //  FindClosestTriangle
    // ======================================================

    /// <summary>
    /// Find the closest point on any triangle to the given point.
    /// Used when the point is outside the nav mesh.
    /// </summary>
    public (int TriangleIndex, Vector2 ClosestPoint) FindClosestTriangle(Vector2 point)
    {
        if (_dtNavMeshQuery != null)
        {
            var pos3 = To3D(point);
            // Use larger extents for off-mesh search
            var halfExtents = new Vector3((Float)100, (Float)10, (Float)100);
            var status = _dtNavMeshQuery.FindNearestPoly(pos3, halfExtents, _dtFilter,
                out long nearestRef, out Vector3 nearestPt, out _);
            if (status.Succeeded() && nearestRef != 0)
            {
                int polyIdx = DtDetour.DecodePolyIdPoly(nearestRef);
                return (polyIdx, To2D(nearestPt));
            }
        }

        // Fallback: brute-force closest point on all triangles
        int bestTri = -1;
        Float bestDist2 = (Float)999999;
        Vector2 bestPoint = point;

        for (int i = 0; i < _triangles.Count; i++)
        {
            var closest = ClosestPointOnTriangle(point, i);
            var d2 = Vector2.DistanceSquared(point, closest);
            if (d2 < bestDist2)
            {
                bestDist2 = d2;
                bestTri = i;
                bestPoint = closest;
            }
        }

        return (bestTri, bestPoint);
    }

    // ======================================================
    //  ComputeSmoothedPath (Detour FindPath + FindStraightPath)
    // ======================================================

    /// <summary>
    /// Compute a smoothed path from start to end using Detour pathfinding.
    /// Returns true if a path was found. Results written to the 'result' list.
    /// Replaces the old FindTrianglePath + SmoothPath sequence.
    /// </summary>
    public bool ComputeSmoothedPath(Vector2 startPos, Vector2 endPos, List<Vector2> result, Float agentRadius = default)
    {
        result.Clear();

        if (!IsBuilt) return false;

        int startTri = FindTriangle(startPos);
        int endTri = FindTriangle(endPos);

        Vector2 adjustedStart = startPos;
        Vector2 adjustedEnd = endPos;
        bool startOffMesh = startTri < 0;

        if (startTri < 0)
            (startTri, adjustedStart) = FindClosestTriangle(startPos);
        if (endTri < 0)
            (endTri, adjustedEnd) = FindClosestTriangle(endPos);

        if (startTri < 0 || endTri < 0) return false;

        // CDT A* — centroid-based heuristic always finds optimal triangle paths.
        // Use the pooled scratch list so no allocation occurs on the hot path.
        var triPath = _triPathScratch;
        if (!FindTrianglePathInto(startTri, endTri, triPath)) return false;

        // CDT funnel (pure string-pulling, no ShortenPath/StraightenPath).
        // With mesh refinement, portal edges are short enough for the funnel
        // to produce correct waypoints. Without ShortenPath, no risk of
        // accidentally removing wall-avoidance waypoints.
        SmoothPathCore(triPath, adjustedStart, adjustedEnd, result, agentRadius);

        #if DEBUG
        if (result.Count <= 2 && triPath.Count > 2)
            ILogger.LogDebug($"FUNNEL_COLLAPSE: tris={_triangles.Count} triPath={triPath.Count} start=({(float)adjustedStart.X:F0},{(float)adjustedStart.Y:F0}) end=({(float)adjustedEnd.X:F0},{(float)adjustedEnd.Y:F0})");
        #endif

        if (startOffMesh && result.Count > 0 && adjustedStart != startPos)
            result.Insert(0, adjustedStart);

        return result.Count > 0;
    }

    // ======================================================
    //  IsPathClear (Detour Raycast)
    // ======================================================

    /// <summary>
    /// Check if a straight-line path between two points stays on the navmesh.
    /// Uses Detour raycast. When margin > 0, also checks offset lines on both sides.
    /// </summary>
    public bool IsPathClear(Vector2 from, Vector2 to, Float margin = default)
    {
        if (!IsSegmentOnNavMesh(from, to))
            return false;
        if (margin <= (Float)0)
            return true;

        var diff = to - from;
        var dist = diff.Magnitude;
        if (dist <= Float.Epsilon) return true;

        var perp = new Vector2(-diff.Y / dist, diff.X / dist) * margin;
        return IsSegmentOnNavMesh(from + perp, to + perp)
            && IsSegmentOnNavMesh(from - perp, to - perp);
    }

    /// <summary>
    /// Check if a line segment stays entirely on the navmesh.
    /// Uses Detour raycast when available (mesh is now refined with Steiner points),
    /// falls back to CDT triangle-walk if Detour isn't initialized.
    /// </summary>
    public bool IsSegmentOnNavMesh(Vector2 from, Vector2 to)
    {
        if (_dtNavMeshQuery != null)
        {
            var from3 = To3D(from);
            var to3 = To3D(to);
            var halfExtents = new Vector3((Float)2, (Float)1, (Float)2);

            var status = _dtNavMeshQuery.FindNearestPoly(from3, halfExtents, _dtFilter,
                out long startRef, out _, out _);
            if (status.Failed() || startRef == 0)
                return IsSegmentOnNavMeshFallback(from, to); // Detour can't find start poly

            Span<long> rayPath = stackalloc long[64];
            status = _dtNavMeshQuery.Raycast(startRef, from3, to3, _dtFilter,
                out Float t, out _, rayPath, out _, 64);

            if (status.Succeeded())
                return t >= (Float)1; // t >= 1 means ray reached end without hitting wall

            // Raycast failed, fall back
            return IsSegmentOnNavMeshFallback(from, to);
        }

        return IsSegmentOnNavMeshFallback(from, to);
    }

    // ======================================================
    //  Legacy methods (kept for compatibility / fallback)
    // ======================================================

    /// <summary>
    /// A* pathfinding from startTri to endTri, returning a list of triangle indices.
    /// LEGACY: Prefer ComputeSmoothedPath which uses Detour internally.
    /// Allocates a fresh List — callers on the hot path should use
    /// <see cref="FindTrianglePathInto"/> with a caller-owned buffer.
    /// </summary>
    public List<int>? FindTrianglePath(int startTri, int endTri)
    {
        var output = new List<int>(8);
        return FindTrianglePathInto(startTri, endTri, output) ? output : null;
    }

    /// <summary>
    /// A* pathfinding that writes the triangle-index path into <paramref name="output"/>.
    /// Returns true on success, false if no path exists or indices are out of range.
    /// Uses pooled A* scratch (heap, gScore/fScore, cameFrom, inClosed) that is
    /// reset to a clean state at entry, so pooled reuse is bit-identical to
    /// allocating fresh buffers.
    /// </summary>
    internal bool FindTrianglePathInto(int startTri, int endTri, List<int> output)
    {
        output.Clear();

        if (startTri < 0 || endTri < 0 || startTri >= _triangles.Count || endTri >= _triangles.Count)
            return false;

        if (startTri == endTri)
        {
            output.Add(startTri);
            return true;
        }

        int triCount = _triangles.Count;

        // Grow pooled scratch if needed. Each buffer is fully re-initialised
        // below, so any stale state from a prior call is overwritten.
        if (_gScoreScratch.Length < triCount) _gScoreScratch = new Float[triCount];
        if (_fScoreScratch.Length < triCount) _fScoreScratch = new Float[triCount];
        if (_cameFromScratch.Length < triCount) _cameFromScratch = new int[triCount];
        if (_inClosedScratch.Length < triCount) _inClosedScratch = new bool[triCount];

        var gScore = _gScoreScratch;
        var fScore = _fScoreScratch;
        var cameFrom = _cameFromScratch;
        var inClosed = _inClosedScratch;

        var maxScore = (Float)999999;
        for (int i = 0; i < triCount; i++)
        {
            gScore[i] = maxScore;
            fScore[i] = maxScore;
            cameFrom[i] = -1;
            inClosed[i] = false;
        }

        gScore[startTri] = (Float)0;
        fScore[startTri] = Heuristic(startTri, endTri);

        var heap = _heapScratch;
        heap.Reset(triCount);
        heap.Push(startTri, fScore[startTri]);

        while (heap.Count > 0)
        {
            int current = heap.Pop();

            if (current == endTri)
            {
                int c = endTri;
                while (c != -1)
                {
                    output.Add(c);
                    c = cameFrom[c];
                }
                output.Reverse();
                return true;
            }

            if (inClosed[current]) continue;
            inClosed[current] = true;

            var tri = _triangles[current];

            for (int slot = 0; slot < 3; slot++)
            {
                int neighbor = tri.GetNeighbor(slot);
                if (neighbor == CDT.CDTConstants.NoNeighbor) continue;
                if (inClosed[neighbor]) continue;

                int edgeV1 = tri.GetVertex(slot);
                int edgeV2 = tri.GetVertex((slot + 1) % 3);
                if (_constraintEdges.Contains(new CDT.Edge(edgeV1, edgeV2)))
                    continue;

                var cost = Vector2.Distance(_centroids[current], _centroids[neighbor]);
                var tentativeG = gScore[current] + cost;

                if (tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, endTri);
                    heap.Push(neighbor, fScore[neighbor]);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Core funnel algorithm only — no ShortenPath/StraightenPath/ValidatePathSegments.
    /// Safe to use with Detour poly paths because it never removes wall-avoidance waypoints.
    /// </summary>
    private void SmoothPathCore(List<int> trianglePath, Vector2 start, Vector2 end, List<Vector2> result, Float agentRadius)
    {
        result.Clear();

        if (trianglePath == null || trianglePath.Count <= 1)
        {
            result.Add(start);
            result.Add(end);
            return;
        }

        // Maximum portal width — portals wider than this get clipped to the
        // region near the start→end line. This prevents the funnel from staying
        // infinitely wide on CDT meshes with huge boundary triangles.
        Float maxPortalWidth = (Float)10;

        // Pooled portals list (instance-owned, cleared before use).
        // Funnel algorithm reads portals[i].Left/Right sequentially, so stale
        // trailing entries from a prior call cannot affect ordering.
        var portals = _portalsScratch;
        portals.Clear();
        portals.Add((start, start));

        for (int i = 0; i < trianglePath.Count - 1; i++)
        {
            var portal = GetPortalEdge(trianglePath[i], trianglePath[i + 1]);
            if (portal.HasValue)
            {
                var (left, right) = portal.Value;

                // Clip wide portals: project both start and end onto the portal
                // edge, then clip to contain both projections plus padding.
                // This prevents the funnel from staying infinitely wide on CDT
                // meshes with huge boundary triangles.
                var edgeDir = right - left;
                var edgeLen = edgeDir.Magnitude;
                if (edgeLen > maxPortalWidth)
                {
                    var ab = right - left;
                    var lengthSq = Vector2.Dot(ab, ab);
                    if (lengthSq > (Float)0)
                    {
                        var tStart = Float.Clamp(Vector2.Dot(start - left, ab) / lengthSq, (Float)0, (Float)1);
                        var tEnd = Float.Clamp(Vector2.Dot(end - left, ab) / lengthSq, (Float)0, (Float)1);
                        var tMin = tStart < tEnd ? tStart : tEnd;
                        var tMax = tStart > tEnd ? tStart : tEnd;

                        // Expand range by half of maxPortalWidth in parameter space
                        var pad = (maxPortalWidth / (Float)2) / edgeLen;
                        tMin = Float.Max(tMin - pad, (Float)0);
                        tMax = Float.Min(tMax + pad, (Float)1);

                        left = left + ab * tMin;
                        right = left + ab * (tMax - tMin);
                    }
                }

                if (agentRadius > (Float)0)
                {
                    edgeDir = right - left;
                    edgeLen = edgeDir.Magnitude;
                    if (edgeLen > agentRadius * (Float)2)
                    {
                        var shrink = edgeDir / edgeLen * agentRadius;
                        left = left + shrink;
                        right = right - shrink;
                    }
                }
                portals.Add((left, right));
            }
        }

        portals.Add((end, end));

        // Simple Funnel Algorithm (Lee's string-pulling)
        result.Add(start);
        int apexIndex = 0, leftIndex = 0, rightIndex = 0;
        var apex = start;
        var portalLeft = start;
        var portalRight = start;

        for (int i = 1; i < portals.Count; i++)
        {
            var left = portals[i].Left;
            var right = portals[i].Right;

            if (TriArea2(apex, portalRight, right) <= (Float)0)
            {
                if (apex == portalRight || TriArea2(apex, portalLeft, right) > (Float)0)
                {
                    portalRight = right;
                    rightIndex = i;
                }
                else
                {
                    result.Add(portalLeft);
                    apex = portalLeft;
                    apexIndex = leftIndex;
                    portalLeft = apex;
                    portalRight = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex + 1;
                    continue;
                }
            }

            if (TriArea2(apex, portalLeft, left) >= (Float)0)
            {
                if (apex == portalLeft || TriArea2(apex, portalRight, left) < (Float)0)
                {
                    portalLeft = left;
                    leftIndex = i;
                }
                else
                {
                    result.Add(portalRight);
                    apex = portalRight;
                    apexIndex = rightIndex;
                    portalLeft = apex;
                    portalRight = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex + 1;
                    continue;
                }
            }
        }

        if (result.Count == 0 || result[result.Count - 1] != end)
            result.Add(end);
    }

    /// <summary>
    /// Convert a triangle path to world-space waypoints using the funnel algorithm.
    /// LEGACY: Prefer ComputeSmoothedPath which uses Detour internally.
    /// </summary>
    public void SmoothPath(List<int> trianglePath, Vector2 start, Vector2 end, List<Vector2> result, Float agentRadius = default)
    {
        result.Clear();

        if (trianglePath == null || trianglePath.Count == 0 || trianglePath.Count == 1)
        {
            result.Add(start);
            result.Add(end);
            return;
        }

        var portals = new List<(Vector2 Left, Vector2 Right)>(64);

        portals.Add((start, start));

        for (int i = 0; i < trianglePath.Count - 1; i++)
        {
            var portal = GetPortalEdge(trianglePath[i], trianglePath[i + 1]);
            if (portal.HasValue)
            {
                var (left, right) = portal.Value;

                if (agentRadius > (Float)0)
                {
                    var edgeDir = right - left;
                    var edgeLen = edgeDir.Magnitude;
                    if (edgeLen > agentRadius * (Float)2)
                    {
                        var shrink = edgeDir / edgeLen * agentRadius;
                        left = left + shrink;
                        right = right - shrink;
                    }
                }

                portals.Add((left, right));
            }
        }

        portals.Add((end, end));

        result.Add(start);
        int apexIndex = 0, leftIndex = 0, rightIndex = 0;
        var apex = start;
        var portalLeft = start;
        var portalRight = start;

        for (int i = 1; i < portals.Count; i++)
        {
            var left = portals[i].Left;
            var right = portals[i].Right;

            if (TriArea2(apex, portalRight, right) <= (Float)0)
            {
                if (apex == portalRight || TriArea2(apex, portalLeft, right) > (Float)0)
                {
                    portalRight = right;
                    rightIndex = i;
                }
                else
                {
                    result.Add(portalLeft);
                    apex = portalLeft;
                    apexIndex = leftIndex;
                    portalLeft = apex;
                    portalRight = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex + 1;
                    continue;
                }
            }

            if (TriArea2(apex, portalLeft, left) >= (Float)0)
            {
                if (apex == portalLeft || TriArea2(apex, portalRight, left) < (Float)0)
                {
                    portalLeft = left;
                    leftIndex = i;
                }
                else
                {
                    result.Add(portalRight);
                    apex = portalRight;
                    apexIndex = rightIndex;
                    portalLeft = apex;
                    portalRight = apex;
                    leftIndex = apexIndex;
                    rightIndex = apexIndex;
                    i = apexIndex + 1;
                    continue;
                }
            }
        }

        if (result.Count == 0 || result[result.Count - 1] != end)
            result.Add(end);
    }

    // ======================================================
    //  Fallback segment check (triangle walk)
    // ======================================================

    private bool IsSegmentOnNavMeshFallback(Vector2 from, Vector2 to)
    {
        int currentTri = FindTriangleFallback(from);
        if (currentTri < 0) return false;

        if (PointInTriangle(to, currentTri)) return true;

        const int MAX_ITERATIONS = 500;
        for (int iter = 0; iter < MAX_ITERATIONS; iter++)
        {
            var tri = _triangles[currentTri];

            int exitSlot = -1;
            for (int slot = 0; slot < 3; slot++)
            {
                int ev1 = tri.GetVertex(slot);
                int ev2 = tri.GetVertex((slot + 1) % 3);
                var a = _vertices[ev1];
                var b = _vertices[ev2];

                var toSide = CDT.CDTUtility.Orient2D(to, a, b);
                if (toSide >= (Float)0) continue;

                var fromSide = CDT.CDTUtility.Orient2D(from, a, b);
                if (fromSide < (Float)0) continue;

                var crossA = CDT.CDTUtility.Orient2D(a, from, to);
                var crossB = CDT.CDTUtility.Orient2D(b, from, to);
                if (crossA > (Float)0 && crossB > (Float)0) continue;
                if (crossA < (Float)0 && crossB < (Float)0) continue;

                exitSlot = slot;
                break;
            }

            if (exitSlot < 0)
            {
                return PointInTriangle(to, currentTri);
            }

            int exitV1 = tri.GetVertex(exitSlot);
            int exitV2 = tri.GetVertex((exitSlot + 1) % 3);
            if (_constraintEdges.Contains(new CDT.Edge(exitV1, exitV2)))
                return false;

            int neighbor = tri.GetNeighbor(exitSlot);
            if (neighbor == CDT.CDTConstants.NoNeighbor)
                return false;

            if (PointInTriangle(to, neighbor))
                return true;

            currentTri = neighbor;
        }

        return false;
    }

    // ======================================================
    //  Private Helpers
    // ======================================================

    private (Vector2 Left, Vector2 Right)? GetPortalEdge(int triA, int triB)
    {
        var t = _triangles[triA];
        for (int i = 0; i < 3; i++)
        {
            if (t.GetNeighbor(i) == triB)
            {
                var edgeA = _vertices[t.GetVertex(i)];
                var edgeB = _vertices[t.GetVertex((i + 1) % 3)];

                // The third vertex (not on the shared edge) is inside triA.
                // Use it to determine which side is "inside" (left in CCW).
                var interior = _vertices[t.GetVertex((i + 2) % 3)];

                // Cross product: (edgeB-edgeA) × (interior-edgeA)
                // If positive (CCW), interior is to the left of edge A→B,
                // meaning left=edgeA, right=edgeB when traversing FROM triA.
                var cross = (edgeB.X - edgeA.X) * (interior.Y - edgeA.Y)
                          - (edgeB.Y - edgeA.Y) * (interior.X - edgeA.X);

                return cross > (Float)0 ? (edgeA, edgeB) : (edgeB, edgeA);
            }
        }
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Float Heuristic(int triA, int triB)
    {
        return Vector2.Distance(_centroids[triA], _centroids[triB]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool PointInTriangle(Vector2 p, int triIdx)
    {
        var t = _triangles[triIdx];
        var v0 = _vertices[t.V0];
        var v1 = _vertices[t.V1];
        var v2 = _vertices[t.V2];

        var loc = CDT.CDTUtility.LocatePointTriangle(p, v0, v1, v2);
        return loc != CDT.PtTriLocation.Outside;
    }

    private Vector2 ClosestPointOnTriangle(Vector2 p, int triIdx)
    {
        var t = _triangles[triIdx];
        var a = _vertices[t.V0];
        var b = _vertices[t.V1];
        var c = _vertices[t.V2];

        if (PointInTriangle(p, triIdx)) return p;

        var closest = ClosestPointOnSegment(p, a, b);
        var bestDist2 = Vector2.DistanceSquared(p, closest);

        var c2 = ClosestPointOnSegment(p, b, c);
        var d2 = Vector2.DistanceSquared(p, c2);
        if (d2 < bestDist2) { closest = c2; bestDist2 = d2; }

        var c3 = ClosestPointOnSegment(p, c, a);
        var d3 = Vector2.DistanceSquared(p, c3);
        if (d3 < bestDist2) { closest = c3; }

        return closest;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSq = Vector2.Dot(ab, ab);
        if (lengthSq <= (Float)0) return a;

        var t = Vector2.Dot(p - a, ab) / lengthSq;
        if (t < (Float)0) t = (Float)0;
        if (t > (Float)1) t = (Float)1;

        return a + ab * t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Float TriArea2(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
    }

    // -- Spatial Grid --

    private void RebuildSpatialGrid()
    {
        foreach (var list in _spatialGrid.Values)
        {
            list.Clear();
            _intListPool.Add(list);
        }
        _spatialGrid.Clear();

        for (int i = 0; i < _triangles.Count; i++)
        {
            var t = _triangles[i];
            AddToSpatialGrid(i, _vertices[t.V0], _vertices[t.V1], _vertices[t.V2]);
        }
    }

    private void AddToSpatialGrid(int triIndex, Vector2 a, Vector2 b, Vector2 c)
    {
        var minX = Min3(a.X, b.X, c.X);
        var minY = Min3(a.Y, b.Y, c.Y);
        var maxX = Max3(a.X, b.X, c.X);
        var maxY = Max3(a.Y, b.Y, c.Y);

        int cx0 = (int)(minX / _cellSize) - 1;
        int cy0 = (int)(minY / _cellSize) - 1;
        int cx1 = (int)(maxX / _cellSize) + 1;
        int cy1 = (int)(maxY / _cellSize) + 1;

        for (int cx = cx0; cx <= cx1; cx++)
        {
            for (int cy = cy0; cy <= cy1; cy++)
            {
                long key = PackCellKey(cx, cy);
                if (!_spatialGrid.TryGetValue(key, out var list))
                {
                    list = RentIntList();
                    _spatialGrid[key] = list;
                }
                list.Add(triIndex);
            }
        }
    }

    private List<int> RentIntList()
    {
        if (_intListPool.Count > 0)
        {
            var list = _intListPool[_intListPool.Count - 1];
            _intListPool.RemoveAt(_intListPool.Count - 1);
            return list;
        }
        return new List<int>(4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetCellKey(Vector2 point)
    {
        int cx = (int)(point.X / _cellSize);
        int cy = (int)(point.Y / _cellSize);
        return PackCellKey(cx, cy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long PackCellKey(int cx, int cy)
    {
        return ((long)cx << 32) | (uint)cy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Float Min3(Float a, Float b, Float c)
    {
        var min = a < b ? a : b;
        return min < c ? min : c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Float Max3(Float a, Float b, Float c)
    {
        var max = a > b ? a : b;
        return max > c ? max : c;
    }
}

// ======================================================
//  MinHeap for CDTNavigationMap A* (legacy)
// ======================================================

/// <summary>
/// Binary min-heap keyed by Float priority.
/// Supports duplicate node entries (lazy deletion via closed-set check in A*).
/// </summary>
internal sealed class CDTMinHeap
{
    private (int Node, Float Priority)[] _data;
    private int _count;

    public int Count => _count;

    public CDTMinHeap(int capacity)
    {
        _data = new (int, Float)[Math.Max(capacity, 16)];
        _count = 0;
    }

    public void Reset(int capacity)
    {
        if (_data.Length < capacity)
            _data = new (int, Float)[capacity];
        _count = 0;
    }

    public void Push(int node, Float priority)
    {
        if (_count == _data.Length)
        {
            var newData = new (int, Float)[_data.Length * 2];
            Array.Copy(_data, newData, _data.Length);
            _data = newData;
        }

        _data[_count] = (node, priority);
        SiftUp(_count);
        _count++;
    }

    public int Pop()
    {
        var node = _data[0].Node;
        _count--;
        _data[0] = _data[_count];
        if (_count > 0) SiftDown(0);
        return node;
    }

    private void SiftUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_data[i].Priority < _data[parent].Priority)
            {
                (_data[i], _data[parent]) = (_data[parent], _data[i]);
                i = parent;
            }
            else break;
        }
    }

    private void SiftDown(int i)
    {
        while (true)
        {
            int left = 2 * i + 1;
            int right = 2 * i + 2;
            int smallest = i;

            if (left < _count && _data[left].Priority < _data[smallest].Priority)
                smallest = left;
            if (right < _count && _data[right].Priority < _data[smallest].Priority)
                smallest = right;

            if (smallest == i) break;
            (_data[i], _data[smallest]) = (_data[smallest], _data[i]);
            i = smallest;
        }
    }
}
