using System;
using System.Collections.Generic;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Represents a triangulated navigation mesh built from NavigationRegion2D polygons.
/// This is the core data structure for pathfinding.
///
/// Design mirrors Godot's NavigationServer2D map concept:
/// - Regions contribute polygons to the map
/// - The map merges overlapping edges to form a connected graph
/// - A* pathfinding operates on the triangle adjacency graph
/// </summary>
public class NavigationMap
{
    // Triangle mesh data
    public readonly List<NavTriangle> Triangles = new();
    public readonly List<NavVertex> Vertices = new();

    // Adjacency: TriangleIndex -> list of (NeighborTriIndex, SharedEdgeCost)
    internal readonly List<List<NavEdge>> Adjacency = new();

    // Region tracking for cost lookups
    internal readonly List<int> TriangleRegionId = new(); // TriangleIndex -> RegionEntityId

    // Region costs: RegionEntityId -> (TravelCost, EnterCost)
    internal readonly Dictionary<int, (Float TravelCost, Float EnterCost)> RegionCosts = new();

    // Spatial lookup acceleration: grid cells -> triangle indices
    private readonly Dictionary<long, List<int>> _spatialGrid = new();
    private Float _cellSize = (Float)64;
    public Float CellSize => _cellSize;

    // Object pools to avoid GC allocations on rebake
    private readonly List<List<NavEdge>> _edgeListPool = new();
    private readonly List<List<int>> _intListPool = new();
    private readonly HashSet<long> _checkedPairsPool = new();

    // A* scratch arrays (grown to fit, reused across pathfind calls)
    private Float[]? _gScorePool;
    private Float[]? _fScorePool;
    private int[]? _cameFromPool;
    private bool[]? _inClosedPool;
    private MinHeap? _heapPool;

    // SmoothPath scratch
    private List<(Vector2 Left, Vector2 Right)>? _portalsScratch;

    // Tombstone sentinel: V0==V1==V2==-1 marks a dead triangle
    internal int InvalidatedCount { get; private set; }

    public bool IsDirty { get; set; } = true;

    /// <summary>
    /// Invalidate (tombstone) a range of triangles. Removes them from the spatial grid
    /// and clears all adjacency edges to/from them. The triangle slots remain but are
    /// skipped by FindTriangle/FindClosestTriangle. Use Compact() to reclaim slots.
    /// </summary>
    public void InvalidateTriangleRange(int start, int count)
    {
        for (int i = start; i < start + count && i < Triangles.Count; i++)
        {
            var tri = Triangles[i];
            if (tri.V0 < 0) continue; // Already invalidated

            // Remove from spatial grid
            var a = Vertices[tri.V0].Position;
            var b = Vertices[tri.V1].Position;
            var c = Vertices[tri.V2].Position;
            RemoveFromSpatialGrid(i, a, b, c);

            // Remove adjacency edges FROM neighbors TO this triangle
            foreach (var edge in Adjacency[i])
            {
                var neighborAdj = Adjacency[edge.NeighborIndex];
                for (int j = neighborAdj.Count - 1; j >= 0; j--)
                {
                    if (neighborAdj[j].NeighborIndex == i)
                    {
                        neighborAdj.RemoveAt(j);
                        break;
                    }
                }
            }

            // Clear this triangle's adjacency
            Adjacency[i].Clear();

            // Mark triangle as dead
            Triangles[i] = new NavTriangle { V0 = -1, V1 = -1, V2 = -1 };
            InvalidatedCount++;
        }
    }

    /// <summary>
    /// Compact the map by removing all invalidated (tombstoned) triangles.
    /// Rebuilds index mappings. Call when InvalidatedCount is high relative to Triangles.Count.
    /// Returns the old-to-new index mapping (or null if no compaction was needed).
    /// </summary>
    public int[]? Compact()
    {
        if (InvalidatedCount == 0) return null;

        // Build old→new index mapping
        var mapping = new int[Triangles.Count];
        int newIndex = 0;
        for (int i = 0; i < Triangles.Count; i++)
        {
            if (Triangles[i].V0 < 0)
            {
                mapping[i] = -1; // Dead
            }
            else
            {
                mapping[i] = newIndex++;
            }
        }

        // Compact triangles, vertices, adjacency, regionIds
        int writePos = 0;
        for (int i = 0; i < Triangles.Count; i++)
        {
            if (Triangles[i].V0 < 0) continue;
            if (writePos != i)
            {
                Triangles[writePos] = Triangles[i];
                Vertices[writePos * 3] = Vertices[i * 3];
                Vertices[writePos * 3 + 1] = Vertices[i * 3 + 1];
                Vertices[writePos * 3 + 2] = Vertices[i * 3 + 2];

                // Update vertex indices in triangle
                var tri = Triangles[writePos];
                tri.V0 = writePos * 3;
                tri.V1 = writePos * 3 + 1;
                tri.V2 = writePos * 3 + 2;
                Triangles[writePos] = tri;

                TriangleRegionId[writePos] = TriangleRegionId[i];

                // Remap adjacency edges
                var adjList = Adjacency[i];
                var destList = Adjacency[writePos];
                destList.Clear();
                foreach (var edge in adjList)
                {
                    int newNeighbor = mapping[edge.NeighborIndex];
                    if (newNeighbor >= 0)
                        destList.Add(new NavEdge { NeighborIndex = newNeighbor, Cost = edge.Cost });
                }
            }
            else
            {
                // Same position, just remap adjacency
                var adjList = Adjacency[writePos];
                for (int j = adjList.Count - 1; j >= 0; j--)
                {
                    int newNeighbor = mapping[adjList[j].NeighborIndex];
                    if (newNeighbor < 0)
                    {
                        adjList.RemoveAt(j);
                    }
                    else
                    {
                        adjList[j] = new NavEdge { NeighborIndex = newNeighbor, Cost = adjList[j].Cost };
                    }
                }
            }
            writePos++;
        }

        // Truncate lists
        int liveCount = writePos;
        Triangles.RemoveRange(liveCount, Triangles.Count - liveCount);
        Vertices.RemoveRange(liveCount * 3, Vertices.Count - liveCount * 3);
        TriangleRegionId.RemoveRange(liveCount, TriangleRegionId.Count - liveCount);

        // Recycle excess adjacency lists
        for (int i = liveCount; i < Adjacency.Count; i++)
        {
            Adjacency[i].Clear();
            _edgeListPool.Add(Adjacency[i]);
        }
        Adjacency.RemoveRange(liveCount, Adjacency.Count - liveCount);

        // Rebuild spatial grid from scratch (fast for ~500 triangles)
        foreach (var list in _spatialGrid.Values)
        {
            list.Clear();
            _intListPool.Add(list);
        }
        _spatialGrid.Clear();
        for (int i = 0; i < Triangles.Count; i++)
        {
            var tri = Triangles[i];
            AddToSpatialGrid(i, Vertices[tri.V0].Position, Vertices[tri.V1].Position, Vertices[tri.V2].Position);
        }

        InvalidatedCount = 0;
        return mapping;
    }

    public void Clear()
    {
        Triangles.Clear();
        Vertices.Clear();

        // Recycle adjacency lists into pool instead of discarding
        for (int i = 0; i < Adjacency.Count; i++)
        {
            Adjacency[i].Clear();
            _edgeListPool.Add(Adjacency[i]);
        }
        Adjacency.Clear();

        TriangleRegionId.Clear();
        RegionCosts.Clear();

        // Recycle spatial grid lists into pool
        foreach (var list in _spatialGrid.Values)
        {
            list.Clear();
            _intListPool.Add(list);
        }
        _spatialGrid.Clear();

        InvalidatedCount = 0;
        IsDirty = true;
    }

    private List<NavEdge> RentEdgeList()
    {
        if (_edgeListPool.Count > 0)
        {
            var list = _edgeListPool[_edgeListPool.Count - 1];
            _edgeListPool.RemoveAt(_edgeListPool.Count - 1);
            return list;
        }
        return new List<NavEdge>(6);
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

    /// <summary>
    /// Adds a convex polygon (already triangulated by caller) to the nav mesh.
    /// </summary>
    public void AddTriangle(Vector2 a, Vector2 b, Vector2 c, int regionEntityId)
    {
        int baseVertex = Vertices.Count;
        Vertices.Add(new NavVertex { Position = a });
        Vertices.Add(new NavVertex { Position = b });
        Vertices.Add(new NavVertex { Position = c });

        int triIndex = Triangles.Count;
        Triangles.Add(new NavTriangle
        {
            V0 = baseVertex,
            V1 = baseVertex + 1,
            V2 = baseVertex + 2,
            Centroid = (a + b + c) / (Float)3,
        });

        TriangleRegionId.Add(regionEntityId);
        Adjacency.Add(RentEdgeList());

        // Add to spatial grid
        AddToSpatialGrid(triIndex, a, b, c);
    }

    /// <summary>
    /// After all triangles are added, compute adjacency by finding shared edges.
    /// Uses the spatial grid to only compare nearby triangles instead of all pairs.
    /// Two triangles are neighbors if they share an edge (two vertices within merge distance).
    /// </summary>
    public void BuildAdjacency(Float mergeDistance)
    {
        BuildAdjacencyFrom(0, mergeDistance);
    }

    /// <summary>
    /// Build adjacency only for triangles at index >= startFrom.
    /// Checks new triangles against ALL triangles (including existing ones) for cross-connections,
    /// but skips pairs where both indices are below startFrom (already have adjacency).
    /// </summary>
    public void BuildAdjacencyFrom(int startFrom, Float mergeDistance)
    {
        var mergeDist2 = mergeDistance * mergeDistance;

        // Reuse pooled set to avoid GC allocation
        _checkedPairsPool.Clear();
        var checkedPairs = _checkedPairsPool;

        foreach (var cell in _spatialGrid.Values)
        {
            for (int a = 0; a < cell.Count; a++)
            {
                int i = cell[a];
                for (int b = a + 1; b < cell.Count; b++)
                {
                    int j = cell[b];

                    // Skip pairs where BOTH triangles are from the existing mesh
                    if (i < startFrom && j < startFrom) continue;

                    // Pack pair into a single long to deduplicate (smaller index always first)
                    int lo = i < j ? i : j;
                    int hi = i < j ? j : i;
                    long pairKey = ((long)lo << 32) | (uint)hi;

                    if (!checkedPairs.Add(pairKey)) continue;

                    if (AreAdjacent(lo, hi, mergeDist2))
                    {
                        var ci = Triangles[lo].Centroid;
                        var cj = Triangles[hi].Centroid;
                        var dist = Vector2.Distance(ci, cj);

                        var costI = GetTravelCost(lo);
                        var costJ = GetTravelCost(hi);
                        var avgCost = (costI + costJ) / (Float)2;

                        Float enterCost = (Float)0;
                        if (TriangleRegionId[lo] != TriangleRegionId[hi])
                        {
                            var (_, enterJ) = GetRegionCost(TriangleRegionId[hi]);
                            var (_, enterI) = GetRegionCost(TriangleRegionId[lo]);
                            enterCost = enterI > enterJ ? enterI : enterJ;
                        }

                        var edgeCost = dist * avgCost + enterCost;

                        Adjacency[lo].Add(new NavEdge { NeighborIndex = hi, Cost = edgeCost });
                        Adjacency[hi].Add(new NavEdge { NeighborIndex = lo, Cost = edgeCost });
                    }
                }
            }
        }

        IsDirty = false;
    }

    /// <summary>
    /// Find the triangle containing the given world-space point.
    /// Returns -1 if not found.
    /// </summary>
    public int FindTriangle(Vector2 point)
    {
        // First try spatial grid lookup
        long cellKey = GetCellKey(point);
        if (_spatialGrid.TryGetValue(cellKey, out var candidates))
        {
            foreach (var triIdx in candidates)
            {
                if (PointInTriangle(point, triIdx))
                    return triIdx;
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
                    foreach (var triIdx in neighborCandidates)
                    {
                        if (PointInTriangle(point, triIdx))
                            return triIdx;
                    }
                }
            }
        }

        // Fallback: brute force (for edge cases)
        for (int i = 0; i < Triangles.Count; i++)
        {
            if (Triangles[i].V0 < 0) continue; // Skip invalidated triangles
            if (PointInTriangle(point, i))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Find the closest point on any triangle to the given point.
    /// Used when the point is outside the nav mesh.
    /// </summary>
    public (int TriangleIndex, Vector2 ClosestPoint) FindClosestTriangle(Vector2 point)
    {
        int bestTri = -1;
        Float bestDist2 = (Float)999999;
        Vector2 bestPoint = point;

        for (int i = 0; i < Triangles.Count; i++)
        {
            if (Triangles[i].V0 < 0) continue; // Skip invalidated triangles
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

    /// <summary>
    /// A* pathfinding from startTri to endTri, returning a list of triangle indices.
    /// Uses a binary min-heap for O(log n) priority queue operations.
    /// </summary>
    public List<int>? FindTrianglePath(int startTri, int endTri)
    {
        if (startTri < 0 || endTri < 0 || startTri >= Triangles.Count || endTri >= Triangles.Count)
            return null;

        int triCount = Triangles.Count;

        if (startTri == endTri)
        {
            var single = new List<int>(1) { startTri };
            return single;
        }

        // Reuse pooled arrays, growing only when triangle count increases
        if (_gScorePool == null || _gScorePool.Length < triCount)
        {
            _gScorePool = new Float[triCount];
            _fScorePool = new Float[triCount];
            _cameFromPool = new int[triCount];
            _inClosedPool = new bool[triCount];
        }
        var gScore = _gScorePool;
        var fScore = _fScorePool!;
        var cameFrom = _cameFromPool!;
        var inClosed = _inClosedPool!;

        // Clear inClosed (gScore/fScore/cameFrom are fully written by the init loop)
        Array.Clear(inClosed, 0, triCount);

        for (int i = 0; i < triCount; i++)
        {
            gScore[i] = (Float)999999;
            fScore[i] = (Float)999999;
            cameFrom[i] = -1;
        }

        gScore[startTri] = (Float)0;
        fScore[startTri] = Heuristic(startTri, endTri);

        // Reuse pooled heap
        var heap = _heapPool ??= new MinHeap(triCount);
        heap.Reset(triCount);
        heap.Push(startTri, fScore[startTri]);

        while (heap.Count > 0)
        {
            int current = heap.Pop();

            if (current == endTri)
            {
                var path = new List<int>();
                int c = endTri;
                while (c != -1)
                {
                    path.Add(c);
                    c = cameFrom[c];
                }
                path.Reverse();
                return path;
            }

            if (inClosed[current]) continue;
            inClosed[current] = true;

            foreach (var edge in Adjacency[current])
            {
                if (inClosed[edge.NeighborIndex]) continue;

                var tentativeG = gScore[current] + edge.Cost;
                if (tentativeG < gScore[edge.NeighborIndex])
                {
                    cameFrom[edge.NeighborIndex] = current;
                    gScore[edge.NeighborIndex] = tentativeG;
                    fScore[edge.NeighborIndex] = tentativeG + Heuristic(edge.NeighborIndex, endTri);

                    heap.Push(edge.NeighborIndex, fScore[edge.NeighborIndex]);
                }
            }
        }

        return null; // No path found
    }

    /// <summary>
    /// Convert triangle path to world-space waypoints using the simple funnel algorithm.
    /// Returns a list of Vector2 waypoints from start to end.
    /// </summary>
    /// <summary>
    /// Smooth path and write results directly into the provided list (avoids allocation).
    /// When agentRadius > 0, portal edges are shrunk inward so waypoints stay
    /// away from triangle edges (prevents wall-hugging).
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

        // Reuse pooled portals list
        var portals = _portalsScratch ??= new List<(Vector2 Left, Vector2 Right)>(64);
        portals.Clear();

        portals.Add((start, start));
        for (int i = 0; i < trianglePath.Count - 1; i++)
        {
            var portal = GetSharedEdge(trianglePath[i], trianglePath[i + 1]);
            if (portal.HasValue)
            {
                var (left, right) = portal.Value;

                // Shrink portal inward by agent radius to keep waypoints off edges
                if (agentRadius > (Float)0)
                {
                    var edgeDir = right - left;
                    var edgeLen = edgeDir.Magnitude;
                    // Only shrink if portal is wide enough (> 2× radius)
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

        // Post-process: validate each segment stays on the navmesh.
        // If a segment cuts through blocked space, insert portal midpoints.
        if (result.Count > 2)
            ValidatePathSegments(result, trianglePath, portals);

        // Shortcut pass: greedily remove intermediate waypoints when direct
        // line-of-sight exists on the navmesh.
        if (result.Count > 2)
            ShortenPath(result);

        // Straighten pass: pull waypoints toward the direct line between neighbors,
        // producing smoother paths that naturally hug obstacles.
        if (result.Count > 2)
            StraightenPath(result);
    }

    /// <summary>
    /// Verify each path segment stays on the navmesh by checking intermediate points.
    /// If a segment leaves the navmesh (crosses blocked area), insert the portal
    /// midpoint between the two triangle-path entries to force the path around.
    /// </summary>
    private void ValidatePathSegments(List<Vector2> path, List<int> trianglePath, List<(Vector2 Left, Vector2 Right)> portals)
    {
        int maxPasses = 10; // prevent infinite loops
        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool modified = false;
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (!IsSegmentOnNavMesh(path[i], path[i + 1]))
                {
                    // Find the best portal midpoint to insert between these waypoints.
                    // Pick the portal closest to the midpoint of the failing segment.
                    var segMid = (path[i] + path[i + 1]) / (Float)2;
                    Vector2 bestMid = segMid;
                    Float bestDist = (Float)999999;

                    // portals[0] is (start,start) and portals[last] is (end,end) — skip those
                    for (int p = 1; p < portals.Count - 1; p++)
                    {
                        var portalMid = (portals[p].Left + portals[p].Right) / (Float)2;
                        var d = Vector2.DistanceSquared(portalMid, segMid);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestMid = portalMid;
                        }
                    }

                    // Only insert if the midpoint is meaningfully different from existing waypoints
                    var distToPrev = Vector2.DistanceSquared(bestMid, path[i]);
                    var distToNext = Vector2.DistanceSquared(bestMid, path[i + 1]);
                    if (distToPrev > (Float)1 && distToNext > (Float)1)
                    {
                        path.Insert(i + 1, bestMid);
                        modified = true;
                        break; // restart validation from the beginning
                    }
                }
            }
            if (!modified) break;
        }
    }

    /// <summary>
    /// Greedily remove intermediate waypoints when direct line-of-sight exists
    /// on the navmesh with a small margin to avoid wall-boundary segments.
    /// </summary>
    private void ShortenPath(List<Vector2> path)
    {
        var margin = _cellSize; // Small margin to prevent wall-hugging shortcuts

        int i = 0;
        while (i < path.Count - 2)
        {
            int bestSkip = i + 1;
            for (int j = path.Count - 1; j > i + 1; j--)
            {
                if (IsPathClear(path[i], path[j], margin))
                {
                    bestSkip = j;
                    break;
                }
            }

            if (bestSkip > i + 1)
                path.RemoveRange(i + 1, bestSkip - i - 1);
            i++;
        }
    }

    /// <summary>
    /// Iteratively pull each intermediate waypoint toward the straight line between
    /// its neighbors. This produces smooth, natural-looking paths that curve around
    /// obstacles instead of zigzagging through portal edges.
    /// Uses binary search to find the closest valid position to the ideal line.
    /// </summary>
    private void StraightenPath(List<Vector2> path)
    {
        const int MAX_ITERATIONS = 3;

        for (int iter = 0; iter < MAX_ITERATIONS; iter++)
        {
            bool changed = false;
            for (int i = 1; i < path.Count - 1; i++)
            {
                var prev = path[i - 1];
                var next = path[i + 1];
                var current = path[i];

                // Ideal position: closest point on line prev→next to current waypoint
                var lineDir = next - prev;
                var lineLenSq = lineDir.SqrMagnitude;
                if (lineLenSq <= Float.Epsilon) continue;

                var t = ((current.X - prev.X) * lineDir.X + (current.Y - prev.Y) * lineDir.Y) / lineLenSq;
                t = Float.Clamp(t, (Float)0, (Float)1);
                var ideal = prev + lineDir * t;

                // Binary search: find furthest point toward ideal that keeps both
                // segments on the navmesh
                var bestPos = current;
                Float lo = (Float)0, hi = (Float)1;

                for (int step = 0; step < 5; step++)
                {
                    var mid = (lo + hi) / (Float)2;
                    var candidate = current + (ideal - current) * mid;

                    if (IsSegmentOnNavMesh(prev, candidate) && IsSegmentOnNavMesh(candidate, next))
                    {
                        bestPos = candidate;
                        lo = mid; // try closer to ideal
                    }
                    else
                    {
                        hi = mid; // back off
                    }
                }

                if (bestPos != current)
                {
                    path[i] = bestPos;
                    changed = true;
                }
            }
            if (!changed) break;
        }

        // Final cleanup: remove waypoints that are now redundant (collinear)
        for (int i = path.Count - 2; i >= 1; i--)
        {
            if (IsSegmentOnNavMesh(path[i - 1], path[i + 1]))
                path.RemoveAt(i);
        }
    }

    /// <summary>
    /// Check if a straight-line path between two points stays on the navmesh.
    /// When margin > 0, also checks offset lines on both sides of the segment.
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
    /// Check if a line segment stays on the navmesh by densely sampling points
    /// and verifying each is inside a triangle. Uses small steps to catch narrow
    /// blocked gaps between triangles.
    /// </summary>
    private bool IsSegmentOnNavMesh(Vector2 from, Vector2 to)
    {
        var diff = to - from;
        var dist = diff.Magnitude;
        if (dist <= Float.Epsilon) return true;

        // Sample at intervals smaller than the smallest triangle edge.
        // Use cellSize/4 to catch even narrow blocked strips.
        var stepSize = _cellSize / (Float)4;
        if (stepSize <= (Float)0) stepSize = (Float)1;
        int steps = (int)(dist / stepSize) + 1;
        steps = Math.Max(steps, 8);   // at least 8 samples
        steps = Math.Min(steps, 200); // cap to avoid perf issues

        var dir = diff / dist;

        for (int s = 1; s < steps; s++)
        {
            Float t = (Float)s / (Float)steps;
            var point = from + diff * t;
            if (FindTriangle(point) < 0)
                return false;
        }
        return true;
    }

    public List<Vector2> SmoothPath(List<int> trianglePath, Vector2 start, Vector2 end)
    {
        if (trianglePath == null || trianglePath.Count == 0)
            return new List<Vector2> { start, end };

        if (trianglePath.Count == 1)
            return new List<Vector2> { start, end };

        // Build portal edges between adjacent triangles
        var portals = new List<(Vector2 Left, Vector2 Right)>(trianglePath.Count + 1);

        // Start portal
        portals.Add((start, start));

        for (int i = 0; i < trianglePath.Count - 1; i++)
        {
            var portal = GetSharedEdge(trianglePath[i], trianglePath[i + 1]);
            if (portal.HasValue)
                portals.Add(portal.Value);
        }

        // End portal
        portals.Add((end, end));

        // Simple Funnel Algorithm (Lee's string-pulling)
        var path = new List<Vector2> { start };
        int apexIndex = 0, leftIndex = 0, rightIndex = 0;
        var apex = start;
        var portalLeft = start;
        var portalRight = start;

        for (int i = 1; i < portals.Count; i++)
        {
            var left = portals[i].Left;
            var right = portals[i].Right;

            // Update right vertex
            if (TriArea2(apex, portalRight, right) <= (Float)0)
            {
                if (apex == portalRight || TriArea2(apex, portalLeft, right) > (Float)0)
                {
                    portalRight = right;
                    rightIndex = i;
                }
                else
                {
                    path.Add(portalLeft);
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

            // Update left vertex
            if (TriArea2(apex, portalLeft, left) >= (Float)0)
            {
                if (apex == portalLeft || TriArea2(apex, portalRight, left) < (Float)0)
                {
                    portalLeft = left;
                    leftIndex = i;
                }
                else
                {
                    path.Add(portalRight);
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

        if (path.Count == 0 || path[path.Count - 1] != end)
            path.Add(end);

        return path;
    }

    // --- Private helpers ---

    private Float Heuristic(int triA, int triB)
    {
        return Vector2.Distance(Triangles[triA].Centroid, Triangles[triB].Centroid);
    }

    private Float GetTravelCost(int triIndex)
    {
        int regionId = TriangleRegionId[triIndex];
        if (RegionCosts.TryGetValue(regionId, out var costs))
            return costs.TravelCost;
        return (Float)1;
    }

    private (Float TravelCost, Float EnterCost) GetRegionCost(int regionEntityId)
    {
        if (RegionCosts.TryGetValue(regionEntityId, out var costs))
            return costs;
        return ((Float)1, (Float)0);
    }

    /// <summary>
    /// Check if two triangles share a connection — either 2 coincident vertices (standard adjacency)
    /// or collinear overlapping edges (handles T-junctions from greedy-merged rectangles).
    /// </summary>
    private bool AreAdjacent(int triA, int triB, Float mergeDist2)
    {
        // Fast path: check for 2 shared vertices (exact or near-exact match)
        int shared = 0;
        for (int va = 0; va < 3; va++)
        {
            var posA = Vertices[Triangles[triA].GetVertex(va)].Position;
            for (int vb = 0; vb < 3; vb++)
            {
                var posB = Vertices[Triangles[triB].GetVertex(vb)].Position;
                if (Vector2.DistanceSquared(posA, posB) <= mergeDist2)
                {
                    shared++;
                    break;
                }
            }
        }
        if (shared >= 2) return true;

        // Slow path: check for collinear overlapping edges (T-junction support).
        // Two edges are adjacent if they lie on the same line and overlap.
        for (int ea = 0; ea < 3; ea++)
        {
            var a0 = Vertices[Triangles[triA].GetVertex(ea)].Position;
            var a1 = Vertices[Triangles[triA].GetVertex((ea + 1) % 3)].Position;

            for (int eb = 0; eb < 3; eb++)
            {
                var b0 = Vertices[Triangles[triB].GetVertex(eb)].Position;
                var b1 = Vertices[Triangles[triB].GetVertex((eb + 1) % 3)].Position;

                if (EdgesOverlapCollinear(a0, a1, b0, b1, mergeDist2))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if two edges are collinear (on the same line) and overlap by a non-trivial amount.
    /// </summary>
    private static bool EdgesOverlapCollinear(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, Float mergeDist2)
    {
        var dir = a1 - a0;
        var lenSq = Vector2.Dot(dir, dir);
        if (lenSq <= mergeDist2) return false; // Degenerate edge

        // Check that b0 and b1 are close to the line through a0-a1
        var perp0 = PerpendicularDistSq(a0, a1, b0, dir, lenSq);
        var perp1 = PerpendicularDistSq(a0, a1, b1, dir, lenSq);
        if (perp0 > mergeDist2 || perp1 > mergeDist2) return false;

        // Project all 4 points onto the edge direction
        var invLen = (Float)1 / lenSq; // dot products are in units of lenSq
        var tA0 = (Float)0;
        var tA1 = (Float)1;
        var tB0 = Vector2.Dot(b0 - a0, dir) * invLen;
        var tB1 = Vector2.Dot(b1 - a0, dir) * invLen;

        // Sort B projections
        if (tB0 > tB1) (tB0, tB1) = (tB1, tB0);

        // Check overlap: min of maxes > max of mins, with some tolerance
        var overlapMin = tA0 > tB0 ? tA0 : tB0;
        var overlapMax = tA1 < tB1 ? tA1 : tB1;
        var minOverlap = (Float)0.01f; // Require at least 1% overlap of edge A
        return (overlapMax - overlapMin) > minOverlap;
    }

    private static Float PerpendicularDistSq(Vector2 lineP0, Vector2 lineP1, Vector2 b0, Vector2 dir, Float lenSq)
    {
        // Distance from point b0 to line (lineP0, lineP1) squared
        var toPoint = b0 - lineP0;
        var cross = toPoint.X * dir.Y - toPoint.Y * dir.X;
        return (cross * cross) / lenSq;
    }

    private bool PointInTriangle(Vector2 p, int triIdx)
    {
        var tri = Triangles[triIdx];
        var a = Vertices[tri.V0].Position;
        var b = Vertices[tri.V1].Position;
        var c = Vertices[tri.V2].Position;

        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);

        bool hasNeg = (d1 < (Float)0) || (d2 < (Float)0) || (d3 < (Float)0);
        bool hasPos = (d1 > (Float)0) || (d2 > (Float)0) || (d3 > (Float)0);

        return !(hasNeg && hasPos);
    }

    private static Float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
    }

    private Vector2 ClosestPointOnTriangle(Vector2 p, int triIdx)
    {
        var tri = Triangles[triIdx];
        var a = Vertices[tri.V0].Position;
        var b = Vertices[tri.V1].Position;
        var c = Vertices[tri.V2].Position;

        // Check if inside
        if (PointInTriangle(p, triIdx)) return p;

        // Find closest point on each edge
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

    private (Vector2 Left, Vector2 Right)? GetSharedEdge(int triA, int triB)
    {
        // Find the two vertices shared between triangles (no allocation)
        Vector2 s0 = default, s1 = default;
        int sharedCount = 0;

        for (int va = 0; va < 3; va++)
        {
            var posA = Vertices[Triangles[triA].GetVertex(va)].Position;
            for (int vb = 0; vb < 3; vb++)
            {
                var posB = Vertices[Triangles[triB].GetVertex(vb)].Position;
                if (Vector2.DistanceSquared(posA, posB) <= (Float)0.01f)
                {
                    if (sharedCount == 0) s0 = posA;
                    else { s1 = posA; goto found; }
                    sharedCount++;
                    break;
                }
            }
        }

        // If vertex match didn't find 2, try collinear edge overlap for T-junctions
        if (sharedCount < 2)
        {
            if (!FindCollinearSharedEdge(triA, triB, out s0, out s1))
                return null;
        }

        found:
        // Determine left/right relative to direction from triA centroid to triB centroid
        var dir = Triangles[triB].Centroid - Triangles[triA].Centroid;
        var cross = (s1.X - s0.X) * dir.Y - (s1.Y - s0.Y) * dir.X;

        if (cross > (Float)0)
            return (s0, s1);
        else
            return (s1, s0);
    }

    /// <summary>
    /// For T-junction adjacency: find the overlapping segment of two collinear edges.
    /// Returns the two endpoints of the overlap.
    /// </summary>
    private bool FindCollinearSharedEdge(int triA, int triB, out Vector2 p0, out Vector2 p1)
    {
        Float mergeDist2 = (Float)1;
        for (int ea = 0; ea < 3; ea++)
        {
            var a0 = Vertices[Triangles[triA].GetVertex(ea)].Position;
            var a1 = Vertices[Triangles[triA].GetVertex((ea + 1) % 3)].Position;

            for (int eb = 0; eb < 3; eb++)
            {
                var b0 = Vertices[Triangles[triB].GetVertex(eb)].Position;
                var b1 = Vertices[Triangles[triB].GetVertex((eb + 1) % 3)].Position;

                if (GetEdgeOverlapSegment(a0, a1, b0, b1, mergeDist2, out p0, out p1))
                    return true;
            }
        }
        p0 = p1 = default;
        return false;
    }

    /// <summary>
    /// If two edges are collinear and overlapping, returns the overlap segment endpoints.
    /// </summary>
    private static bool GetEdgeOverlapSegment(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1,
        Float mergeDist2, out Vector2 p0, out Vector2 p1)
    {
        p0 = p1 = default;
        var dir = a1 - a0;
        var lenSq = Vector2.Dot(dir, dir);
        if (lenSq <= mergeDist2) return false;

        var perp0 = PerpendicularDistSq(a0, a1, b0, dir, lenSq);
        var perp1 = PerpendicularDistSq(a0, a1, b1, dir, lenSq);
        if (perp0 > mergeDist2 || perp1 > mergeDist2) return false;

        var invLen = (Float)1 / lenSq;
        var tB0 = Vector2.Dot(b0 - a0, dir) * invLen;
        var tB1 = Vector2.Dot(b1 - a0, dir) * invLen;
        if (tB0 > tB1) (tB0, tB1) = (tB1, tB0);

        var overlapMin = (Float)0 > tB0 ? (Float)0 : tB0;
        var overlapMax = (Float)1 < tB1 ? (Float)1 : tB1;
        if ((overlapMax - overlapMin) <= (Float)0.01f) return false;

        p0 = a0 + dir * overlapMin;
        p1 = a0 + dir * overlapMax;
        return true;
    }

    private static Float TriArea2(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
    }

    private void RemoveFromSpatialGrid(int triIndex, Vector2 a, Vector2 b, Vector2 c)
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
                if (_spatialGrid.TryGetValue(key, out var list))
                {
                    list.Remove(triIndex);
                }
            }
        }
    }

    private void AddToSpatialGrid(int triIndex, Vector2 a, Vector2 b, Vector2 c)
    {
        // Add triangle to all grid cells it overlaps
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

    private long GetCellKey(Vector2 point)
    {
        int cx = (int)(point.X / _cellSize);
        int cy = (int)(point.Y / _cellSize);
        return PackCellKey(cx, cy);
    }

    private static long PackCellKey(int cx, int cy)
    {
        return ((long)cx << 32) | (uint)cy;
    }

    private static Float Min3(Float a, Float b, Float c)
    {
        var min = a < b ? a : b;
        return min < c ? min : c;
    }

    private static Float Max3(Float a, Float b, Float c)
    {
        var max = a > b ? a : b;
        return max > c ? max : c;
    }
}

public struct NavVertex
{
    public Vector2 Position;
}

public struct NavTriangle
{
    public int V0, V1, V2;
    public Vector2 Centroid;

    public int GetVertex(int index) => index switch
    {
        0 => V0,
        1 => V1,
        2 => V2,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}

public struct NavEdge
{
    public int NeighborIndex;
    public Float Cost;
}

/// <summary>
/// Binary min-heap keyed by Float priority.
/// Supports duplicate node entries (lazy deletion via closed-set check in A*).
/// </summary>
internal sealed class MinHeap
{
    private (int Node, Float Priority)[] _data;
    private int _count;

    public int Count => _count;

    public MinHeap(int capacity)
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
