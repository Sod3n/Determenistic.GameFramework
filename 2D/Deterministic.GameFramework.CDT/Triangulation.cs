/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

// Ported from CDT C++ library: Triangulation.h / Triangulation.hpp
// Public API surface, constructors, and the addSuperTriangle implementation.
// Algorithm internals (insertVertex, insertEdge, etc.) live in other partial class files.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Float = Deterministic.GameFramework.Types.Float;
using Vector2 = Deterministic.GameFramework.Types.Vector2;

namespace Deterministic.GameFramework.CDT
{
    // ──────────────────────────────────────────────
    //  Internal helper structs used by the algorithm
    // ──────────────────────────────────────────────

    /// <summary>
    /// State for an iteration of triangulating a pseudo-polygon.
    /// Ported from C++ <c>TriangulatePseudoPolygonTask</c> typedef.
    /// Fields: (iA, iB) are indices into the pseudo-polygon vertex list,
    /// iT is the triangle to fill, iParent/iInParent link back to the parent triangle.
    /// </summary>
    internal struct TriangulatePseudoPolygonTask
    {
        public int IA;
        public int IB;
        public int IT;
        public int IParent;
        public int IInParent;

        public TriangulatePseudoPolygonTask(int ia, int ib, int it, int iParent, int iInParent)
        {
            IA = ia;
            IB = ib;
            IT = it;
            IParent = iParent;
            IInParent = iInParent;
        }
    }

    /// <summary>
    /// State for an iteration of conforming to an edge.
    /// Ported from C++ <c>ConformToEdgeTask</c> typedef.
    /// </summary>
    internal struct ConformToEdgeTask
    {
        public Edge Edge;
        public List<Edge> Originals;
        public ushort OverlapCount;

        public ConformToEdgeTask(Edge edge, List<Edge> originals, ushort overlapCount)
        {
            Edge = edge;
            Originals = originals;
            OverlapCount = overlapCount;
        }
    }

    // ──────────────────────────────────────────────
    //  Triangulation class (partial — public API)
    // ──────────────────────────────────────────────

    /// <summary>
    /// Data structure representing a 2D constrained Delaunay triangulation.
    /// <para>
    /// Ported from CDT C++ <c>Triangulation&lt;T, TNearPointLocator&gt;</c>.
    /// The C++ template parameters are replaced with concrete types:
    /// <c>T</c> = <see cref="Float"/> (deterministic fixed-point),
    /// <c>TNearPointLocator</c> = <see cref="LocatorKDTree"/>.
    /// </para>
    /// </summary>
    public partial class Triangulation
    {
        // ── Public fields (match C++ public members) ──

        /// <summary>Triangulation vertices.</summary>
        public List<Vector2> Vertices;

        /// <summary>Triangulation triangles.</summary>
        public List<Triangle> Triangles;

        /// <summary>Constraint (fixed) edges.</summary>
        public HashSet<Edge> FixedEdges;

        /// <summary>
        /// Stores count of overlapping boundaries for a fixed edge.
        /// Only has entries for fixed edges that represent overlapping boundaries.
        /// Needed for depth calculations and hole-removal with overlapping boundaries.
        /// </summary>
        public Dictionary<Edge, ushort> OverlapCount;

        /// <summary>
        /// Stores list of original edges represented by a given fixed edge.
        /// Only has entries for edges where multiple original fixed edges overlap
        /// or where a fixed edge is part of an original edge created by conforming
        /// Delaunay triangulation vertex insertion.
        /// </summary>
        public Dictionary<Edge, List<Edge>> PieceToOriginals;

        // ── Private fields ──

        /// <summary>One triangle adjacent to each vertex (used for walking).</summary>
        private List<int> _vertTris;

        /// <summary>Near-point locator (KD-tree) for efficient vertex insertion.</summary>
        private LocatorKDTree _nearPtLocator;

        /// <summary>
        /// Number of target (non-super-geometry) vertices that existed at
        /// super-geometry creation time. Equals <see cref="CDTConstants.SuperTriangleVertexCount"/>
        /// after <see cref="AddSuperTriangle"/>, or the total vertex count
        /// after <see cref="InitializeWithCustomSuperGeometry"/>.
        /// Used to offset edge vertex indices.
        /// </summary>
        private int _nTargetVerts;

        /// <summary>What kind of super-geometry was used to bootstrap the triangulation.</summary>
        private SuperGeometryType _superGeomType;

        /// <summary>Strategy for the order in which vertices are inserted.</summary>
        private VertexInsertionOrder _vertexInsertionOrder;

        /// <summary>Strategy for treating intersecting constraint edges.</summary>
        private IntersectingConstraintEdges _intersectingEdgesStrategy;

        /// <summary>
        /// Distance within which a point is considered to lie on a constraint edge.
        /// Used when adding constraints to the triangulation.
        /// </summary>
        private Float _minDistToConstraintEdge;

        // ── Constructors ──

        /// <summary>Default constructor.</summary>
        public Triangulation()
        {
            Vertices = new List<Vector2>();
            Triangles = new List<Triangle>();
            FixedEdges = new HashSet<Edge>();
            OverlapCount = new Dictionary<Edge, ushort>();
            PieceToOriginals = new Dictionary<Edge, List<Edge>>();
            _vertTris = new List<int>();
            _nearPtLocator = new LocatorKDTree();
            _nTargetVerts = 0;
            _superGeomType = SuperGeometryType.SuperTriangle;
            _vertexInsertionOrder = VertexInsertionOrder.Auto;
            _intersectingEdgesStrategy = IntersectingConstraintEdges.NotAllowed;
            _minDistToConstraintEdge = (Float)0;
        }

        /// <summary>
        /// Constructor with vertex insertion order strategy.
        /// </summary>
        /// <param name="vertexInsertionOrder">Strategy for ordering vertex insertions.</param>
        public Triangulation(VertexInsertionOrder vertexInsertionOrder)
            : this()
        {
            _vertexInsertionOrder = vertexInsertionOrder;
        }

        /// <summary>
        /// Constructor with vertex insertion order, intersecting edges strategy, and
        /// minimum distance to constraint edge.
        /// </summary>
        public Triangulation(
            VertexInsertionOrder vertexInsertionOrder,
            IntersectingConstraintEdges intersectingEdgesStrategy,
            Float minDistToConstraintEdge)
            : this()
        {
            _vertexInsertionOrder = vertexInsertionOrder;
            _intersectingEdgesStrategy = intersectingEdgesStrategy;
            _minDistToConstraintEdge = minDistToConstraintEdge;
        }

        /// <summary>
        /// Full constructor with all parameters including a custom near-point locator.
        /// Internal because <see cref="LocatorKDTree"/> is an internal type.
        /// </summary>
        internal Triangulation(
            VertexInsertionOrder vertexInsertionOrder,
            LocatorKDTree nearPtLocator,
            IntersectingConstraintEdges intersectingEdgesStrategy,
            Float minDistToConstraintEdge)
            : this()
        {
            _nearPtLocator = nearPtLocator;
            _vertexInsertionOrder = vertexInsertionOrder;
            _intersectingEdgesStrategy = intersectingEdgesStrategy;
            _minDistToConstraintEdge = minDistToConstraintEdge;
        }

        // ── Public properties ──

        /// <summary>
        /// Check if the triangulation was finalized with an <c>Erase...</c> method
        /// and the super-triangle was removed. Further modification is not possible.
        /// </summary>
        public bool IsFinalized => _vertTris.Count == 0 && Vertices.Count > 0;

        // ══════════════════════════════════════════════
        //  Public API methods
        // ══════════════════════════════════════════════

        /// <summary>
        /// Insert vertices into the triangulation.
        /// On the first call a super-triangle is automatically created.
        /// </summary>
        /// <param name="vertices">Vertices to insert.</param>
        public void InsertVertices(IReadOnlyList<Vector2> vertices)
        {
            if (IsFinalized)
                throw new FinalizedError();

            bool isFirstTime = Vertices.Count == 0;

            // ── Pre-allocate ──
            int nNewVertices = vertices.Count;
            int exactCapacityTriangles = Triangles.Count + 2 * nNewVertices;
            int exactCapacityVertices = Vertices.Count + nNewVertices;
            if (isFirstTime)
            {
                exactCapacityTriangles += 1;
                exactCapacityVertices += CDTConstants.SuperTriangleVertexCount;
            }

            int capacityTriangles = exactCapacityTriangles;
            int capacityVertices = exactCapacityVertices;

            // Over-allocate slightly when resolving intersections and vertex count is large,
            // because a vertex is added for each intersection and total count is unknown.
            const int overAllocationVerticesThreshold = 1000;
            bool isOverPreAllocated =
                _intersectingEdgesStrategy == IntersectingConstraintEdges.TryResolve
                && nNewVertices >= overAllocationVerticesThreshold;
            if (isOverPreAllocated)
            {
                // Use integer scaling: multiply by 11/10 to approximate 1.1x
                capacityTriangles = capacityTriangles * 11 / 10;
                capacityVertices = capacityVertices * 11 / 10;
            }

            if (Triangles.Capacity < capacityTriangles)
                Triangles.Capacity = capacityTriangles;
            if (Vertices.Capacity < capacityVertices)
                Vertices.Capacity = capacityVertices;
            if (_vertTris.Capacity < capacityVertices)
                _vertTris.Capacity = capacityVertices;

            Box2d box = Box2d.Empty;
            if (isFirstTime)
            {
                box.EnvelopPoints(vertices);
                AddSuperTriangle(box);
            }

            TryInitNearestPointLocator();
            int nExistingVerts = Vertices.Count;

            for (int i = 0; i < vertices.Count; i++)
            {
                AddNewVertex(vertices[i], CDTConstants.NoNeighbor);
            }

            switch (_vertexInsertionOrder)
            {
                case VertexInsertionOrder.AsProvided:
                    InsertVertices_AsProvided(nExistingVerts);
                    break;
                case VertexInsertionOrder.Auto:
                    if (isFirstTime)
                        InsertVertices_KDTreeBFS(nExistingVerts, box);
                    else
                        InsertVertices_Randomized(nExistingVerts);
                    break;
            }

            Debug.Assert(Vertices.Count == exactCapacityVertices);
            Debug.Assert(Triangles.Count == exactCapacityTriangles);
        }

        /// <summary>
        /// Insert constraint edges for constrained Delaunay triangulation.
        /// Uses only original vertices: no new vertices are added.
        /// <para>
        /// Each fixed edge is inserted by deleting the triangles it crosses,
        /// followed by the triangulation of the polygons on each side of the edge.
        /// </para>
        /// </summary>
        /// <param name="edges">Constraint edges to insert.</param>
        public void InsertEdges(IReadOnlyList<Edge> edges)
        {
            if (IsFinalized)
                throw new FinalizedError();

            var tppIterations = new List<TriangulatePseudoPolygonTask>();
            var remaining = new List<Edge>();

            for (int i = 0; i < edges.Count; i++)
            {
                // Offset by _nTargetVerts to account for super-triangle vertices
                Edge edge = new Edge(
                    edges[i].V1 + _nTargetVerts,
                    edges[i].V2 + _nTargetVerts);
                InsertEdge(edge, edge, remaining, tppIterations);
            }
        }

        /// <summary>
        /// Insert constraint edges for conforming Delaunay triangulation.
        /// May add new vertices (edge midpoints are recursively added until the
        /// original edge is represented by a sequence of pieces).
        /// </summary>
        /// <param name="edges">Edges to conform to.</param>
        public void ConformToEdges(IReadOnlyList<Edge> edges)
        {
            if (IsFinalized)
                throw new FinalizedError();

            TryInitNearestPointLocator();

            var remaining = new List<ConformToEdgeTask>();

            for (int i = 0; i < edges.Count; i++)
            {
                // Offset by _nTargetVerts to account for super-triangle vertices
                Edge e = new Edge(
                    edges[i].V1 + _nTargetVerts,
                    edges[i].V2 + _nTargetVerts);
                ConformToEdge(e, new List<Edge> { e }, 0, remaining);
            }
        }

        /// <summary>
        /// Erase triangles adjacent to the super-triangle.
        /// Does nothing if custom geometry was used.
        /// </summary>
        public void EraseSuperTriangle()
        {
            if (_superGeomType != SuperGeometryType.SuperTriangle)
                return;

            var toErase = new HashSet<int>();
            for (int iT = 0; iT < Triangles.Count; iT++)
            {
                if (Triangles[iT].TouchesSuperTriangle())
                    toErase.Add(iT);
            }
            FinalizeTriangulation(toErase);
        }

        /// <summary>
        /// Erase triangles outside of constrained boundary using flood-fill growing.
        /// </summary>
        public void EraseOuterTriangles()
        {
            Debug.Assert(_vertTris[0] != CDTConstants.NoNeighbor);
            var seed = new Stack<int>();
            seed.Push(_vertTris[0]);
            HashSet<int> toErase = GrowToBoundary(seed);
            FinalizeTriangulation(toErase);
        }

        /// <summary>
        /// Erase triangles outside of constrained boundary and auto-detected holes.
        /// Detecting holes relies on layer peeling based on layer depth.
        /// Supports overlapping or touching boundaries.
        /// </summary>
        public void EraseOuterTrianglesAndHoles()
        {
            List<int> triDepths = CalculateTriangleDepths();
            var toErase = new HashSet<int>();
            for (int iT = 0; iT < Triangles.Count; iT++)
            {
                if (triDepths[iT] % 2 == 0)
                    toErase.Add(iT);
            }
            FinalizeTriangulation(toErase);
        }

        /// <summary>
        /// Call this method after directly setting custom super-geometry via
        /// <see cref="Vertices"/> and <see cref="Triangles"/> members.
        /// </summary>
        public void InitializeWithCustomSuperGeometry()
        {
            _nearPtLocator.Initialize(Vertices);
            _nTargetVerts = Vertices.Count;
            _superGeomType = SuperGeometryType.Custom;
        }

        /// <summary>
        /// Calculate depth of each triangle in the constraint triangulation.
        /// Supports overlapping boundaries.
        /// <para>
        /// Performs depth peeling from the super-triangle outward:
        /// depth 0 = outside outermost boundary,
        /// depth 1 = inside boundary but outside hole,
        /// depth 2 = inside hole, etc.
        /// </para>
        /// </summary>
        /// <returns>List where element at index i stores depth of i-th triangle.</returns>
        public List<int> CalculateTriangleDepths()
        {
            int triCount = Triangles.Count;
            var triDepths = new List<int>(triCount);
            for (int i = 0; i < triCount; i++)
                triDepths.Add(ushort.MaxValue);

            var seeds = new Stack<int>();
            seeds.Push(_vertTris[0]);
            int layerDepth = 0;
            int deepestSeedDepth = 0;

            var seedsByDepth = new Dictionary<int, HashSet<int>>();
            do
            {
                Dictionary<int, int> newSeeds = PeelLayer(seeds, layerDepth, triDepths);

                seedsByDepth.Remove(layerDepth);
                foreach (var kvp in newSeeds)
                {
                    deepestSeedDepth = Math.Max(deepestSeedDepth, kvp.Value);
                    if (!seedsByDepth.TryGetValue(kvp.Value, out var depthSet))
                    {
                        depthSet = new HashSet<int>();
                        seedsByDepth[kvp.Value] = depthSet;
                    }
                    depthSet.Add(kvp.Key);
                }

                seeds = new Stack<int>();
                if (seedsByDepth.TryGetValue(layerDepth + 1, out var nextLayerSeeds))
                {
                    foreach (int s in nextLayerSeeds)
                        seeds.Push(s);
                }
                layerDepth++;
            } while (seeds.Count > 0 || deepestSeedDepth > layerDepth);

            return triDepths;
        }

        /// <summary>
        /// Flip an edge between two triangles.
        /// <para>
        /// Advanced method for manually modifying the triangulation from outside.
        /// Only use when you know what you are doing.
        /// </para>
        /// </summary>
        /// <param name="iT">First triangle index.</param>
        /// <param name="iTopo">Second triangle index (opposite).</param>
        public void FlipEdge(int iT, int iTopo)
        {
            Triangle t = Triangles[iT];
            Triangle tOpo = Triangles[iTopo];

            // find vertices and neighbors
            int i = CDTUtility.OpposedVertexInd(in t, iTopo);
            int v1 = t.GetVertex(i);
            int v2 = t.GetVertex(CDTUtility.Ccw(i));
            int n1 = t.GetNeighbor(i);
            int n3 = t.GetNeighbor(CDTUtility.Cw(i));

            i = CDTUtility.OpposedVertexInd(in tOpo, iT);
            int v3 = tOpo.GetVertex(i);
            int v4 = tOpo.GetVertex(CDTUtility.Ccw(i));
            int n4 = tOpo.GetNeighbor(i);
            int n2 = tOpo.GetNeighbor(CDTUtility.Cw(i));

            // change vertices and neighbors
            Triangles[iT] = new Triangle(v4, v1, v3, n3, iTopo, n4);
            Triangles[iTopo] = new Triangle(v2, v3, v1, n2, iT, n1);

            // adjust neighboring triangles
            ChangeNeighbor(n1, iT, iTopo);
            ChangeNeighbor(n4, iTopo, iT);

            // only adjust adjacent triangles if triangulation is not finalized
            if (!IsFinalized)
            {
                SetAdjacentTriangle(v4, iT);
                SetAdjacentTriangle(v2, iTopo);
            }
        }

        /// <summary>
        /// Flip edge between two triangles given all the information
        /// (triangle vertices and neighbors).
        /// </summary>
        public void FlipEdge(
            int iT, int iTopo,
            int v1, int v2, int v3, int v4,
            int n1, int n2, int n3, int n4)
        {
            Triangles[iT] = new Triangle(v4, v1, v3, n3, iTopo, n4);
            Triangles[iTopo] = new Triangle(v2, v3, v1, n2, iT, n1);

            ChangeNeighbor(n1, iT, iTopo);
            ChangeNeighbor(n4, iTopo, iT);

            if (!IsFinalized)
            {
                SetAdjacentTriangle(v4, iT);
                SetAdjacentTriangle(v2, iTopo);
            }
        }

        /// <summary>
        /// Remove triangles with specified indices.
        /// Adjusts internal triangulation state accordingly.
        /// </summary>
        /// <param name="removedTriangles">Indices of triangles to remove.</param>
        public void RemoveTriangles(HashSet<int> removedTriangles)
        {
            if (removedTriangles.Count == 0)
                return;

            // Remove triangles and calculate triangle index mapping
            var triIndMap = new Dictionary<int, int>();
            int iTnew = 0;
            for (int iT = 0; iT < Triangles.Count; iT++)
            {
                if (removedTriangles.Contains(iT))
                    continue;
                triIndMap[iT] = iTnew;
                Triangles[iTnew] = Triangles[iT];
                iTnew++;
            }
            Triangles.RemoveRange(Triangles.Count - removedTriangles.Count, removedTriangles.Count);

            // Adjust triangle neighbors
            for (int iT = 0; iT < Triangles.Count; iT++)
            {
                Triangle t = Triangles[iT];
                for (int ni = 0; ni < 3; ni++)
                {
                    int n = t.GetNeighbor(ni);
                    if (removedTriangles.Contains(n))
                    {
                        t.SetNeighbor(ni, CDTConstants.NoNeighbor);
                    }
                    else if (n != CDTConstants.NoNeighbor)
                    {
                        t.SetNeighbor(ni, triIndMap[n]);
                    }
                }
                Triangles[iT] = t;
            }
        }

        /// <summary>Access internal vertex-adjacent-triangle list.</summary>
        internal List<int> VertTrisInternal => _vertTris;

        // ══════════════════════════════════════════════
        //  Private: super-triangle creation (fully ported)
        // ══════════════════════════════════════════════

        /// <summary>
        /// Create a super-triangle that encloses the bounding box of all input points.
        /// Adds 3 super-vertices (indices 0, 1, 2) and 1 initial triangle.
        /// </summary>
        private void AddSuperTriangle(Box2d box)
        {
            _nTargetVerts = CDTConstants.SuperTriangleVertexCount;
            _superGeomType = SuperGeometryType.SuperTriangle;

            Float two = (Float)2;
            Vector2 center = new Vector2(
                (box.Min.X + box.Max.X) / two,
                (box.Min.Y + box.Max.Y) / two);

            Float w = box.Max.X - box.Min.X;
            Float h = box.Max.Y - box.Min.Y;
            Float r = Float.Max(w, h); // incircle radius upper bound

            // Make sure radius is big enough:
            // - for tiny bounding boxes: use 1.0 as the smallest radius
            // - multiply radius by 2.0 for extra safety margin
            Float one = (Float)1;
            r = Float.Max(two * r, one);

            // For very large floating point numbers rounding can lead to wrong
            // super-triangle coordinates. Primitive handling of this rare corner-case.
            while (center.Y == center.Y - r)
                r = r * two;

            Float R = two * r; // excircle radius
            Float cos30 = (Float)0.8660254037844386f; // sqrt(3) / 2
            Float shiftX = R * cos30;

            Vector2 posV1 = new Vector2(center.X - shiftX, center.Y - r);
            Vector2 posV2 = new Vector2(center.X + shiftX, center.Y - r);
            Vector2 posV3 = new Vector2(center.X, center.Y + R);

            AddNewVertex(posV1, 0);
            AddNewVertex(posV2, 0);
            AddNewVertex(posV3, 0);

            AddTriangle(new Triangle(0, 1, 2,
                CDTConstants.NoNeighbor, CDTConstants.NoNeighbor, CDTConstants.NoNeighbor));

            if (_vertexInsertionOrder != VertexInsertionOrder.Auto)
            {
                _nearPtLocator.Initialize(Vertices);
            }
        }

        // ══════════════════════════════════════════════
        //  Private helpers (small, fully ported here)
        // ══════════════════════════════════════════════

        /// <summary>Add a new vertex with an associated triangle.</summary>
        private void AddNewVertex(Vector2 pos, int iT)
        {
            Vertices.Add(pos);
            _vertTris.Add(iT);
        }

        /// <summary>Add a triangle to the list and return its index.</summary>
        private int AddTriangle(Triangle t)
        {
            int iT = Triangles.Count;
            Triangles.Add(t);
            return iT;
        }

        /// <summary>Add a default/empty triangle to the list and return its index.</summary>
        private int AddTriangle()
        {
            return AddTriangle(Triangle.Empty);
        }

        /// <summary>Set the adjacent triangle for a vertex.</summary>
        private void SetAdjacentTriangle(int v, int t)
        {
            Debug.Assert(t != CDTConstants.NoNeighbor);
            _vertTris[v] = t;
            Debug.Assert(Triangles[t].ContainsVertex(v));
        }

        /// <summary>Pivot vertex's adjacent triangle clockwise.</summary>
        private void PivotVertexTriangleCW(int v)
        {
            Debug.Assert(_vertTris[v] != CDTConstants.NoNeighbor);
            _vertTris[v] = Triangles[_vertTris[v]].Next(v).TriInd;
            Debug.Assert(_vertTris[v] != CDTConstants.NoNeighbor);
            Debug.Assert(Triangles[_vertTris[v]].ContainsVertex(v));
        }

        /// <summary>Change a triangle's neighbor from oldNeighbor to newNeighbor.</summary>
        private void ChangeNeighbor(int iT, int oldNeighbor, int newNeighbor)
        {
            if (iT == CDTConstants.NoNeighbor)
                return;
            Triangle t = Triangles[iT];
            if (t.N0 == oldNeighbor) t.N0 = newNeighbor;
            else if (t.N1 == oldNeighbor) t.N1 = newNeighbor;
            else t.N2 = newNeighbor;
            Triangles[iT] = t;
        }

        /// <summary>
        /// Change the neighbor of triangle <paramref name="iT"/> that shares
        /// the edge (iVedge1, iVedge2) to <paramref name="newNeighbor"/>.
        /// </summary>
        private void ChangeNeighbor(int iT, int iVedge1, int iVedge2, int newNeighbor)
        {
            Triangle t = Triangles[iT];
            int ni = CDTUtility.EdgeNeighborInd(in t, iVedge1, iVedge2);
            t.SetNeighbor(ni, newNeighbor);
            Triangles[iT] = t;
        }

        /// <summary>Add vertex to nearest-point locator if locator is initialized.</summary>
        private void TryAddVertexToLocator(int v)
        {
            if (!_nearPtLocator.Empty)
                _nearPtLocator.AddPoint(v, Vertices);
        }

        /// <summary>
        /// Perform lazy initialization of nearest-point locator after the KD-tree
        /// BFS bulk load, if necessary.
        /// </summary>
        private void TryInitNearestPointLocator()
        {
            if (Vertices.Count > 0 && _nearPtLocator.Empty)
            {
                _nearPtLocator.Initialize(Vertices);
            }
        }

        /// <summary>Mark an edge as fixed. If already fixed, increment overlap count.</summary>
        private void FixEdge(Edge edge)
        {
            if (!FixedEdges.Add(edge))
            {
                // Edge is already fixed: increment the overlap counter
                if (OverlapCount.TryGetValue(edge, out ushort count))
                    OverlapCount[edge] = (ushort)(count + 1);
                else
                    OverlapCount[edge] = 1;
            }
        }

        /// <summary>Mark an edge as fixed and record its relationship to an original edge.</summary>
        private void FixEdge(Edge edge, Edge originalEdge)
        {
            FixEdge(edge);
            if (edge != originalEdge)
            {
                if (!PieceToOriginals.TryGetValue(edge, out var originals))
                {
                    originals = new List<Edge>();
                    PieceToOriginals[edge] = originals;
                }
                // insert_unique: only add if not already present
                if (!originals.Contains(originalEdge))
                    originals.Add(originalEdge);
            }
        }

        /// <summary>Check if edge (a, b) exists in the triangulation.</summary>
        private bool HasEdge(int a, int b)
        {
            return EdgeTriangles(a, b).Item1 != CDTConstants.NoNeighbor;
        }

        // ══════════════════════════════════════════════
        //  Private: finalization (removing super-triangle)
        // ══════════════════════════════════════════════

        /// <summary>
        /// Remap an edge by subtracting the super-triangle vertex count from both indices.
        /// </summary>
        private static Edge RemapNoSuperTriangle(Edge e)
        {
            return new Edge(
                e.V1 - CDTConstants.SuperTriangleVertexCount,
                e.V2 - CDTConstants.SuperTriangleVertexCount);
        }

        /// <summary>
        /// Remove super-triangle (if used) and the specified triangles.
        /// Adjusts internal triangulation state accordingly.
        /// </summary>
        private void FinalizeTriangulation(HashSet<int> removedTriangles)
        {
            _vertTris = new List<int>();

            // Remove super-triangle
            if (_superGeomType == SuperGeometryType.SuperTriangle)
            {
                Vertices.RemoveRange(0, CDTConstants.SuperTriangleVertexCount);

                // Remap fixed edges
                var updatedFixedEdges = new HashSet<Edge>();
                foreach (Edge e in FixedEdges)
                    updatedFixedEdges.Add(RemapNoSuperTriangle(e));
                FixedEdges = updatedFixedEdges;

                // Remap overlap count
                var updatedOverlapCount = new Dictionary<Edge, ushort>();
                foreach (var kvp in OverlapCount)
                    updatedOverlapCount[RemapNoSuperTriangle(kvp.Key)] = kvp.Value;
                OverlapCount = updatedOverlapCount;

                // Remap piece-to-originals
                var updatedPieceToOriginals = new Dictionary<Edge, List<Edge>>();
                foreach (var kvp in PieceToOriginals)
                {
                    var remappedOriginals = new List<Edge>(kvp.Value.Count);
                    for (int i = 0; i < kvp.Value.Count; i++)
                        remappedOriginals.Add(RemapNoSuperTriangle(kvp.Value[i]));
                    updatedPieceToOriginals[RemapNoSuperTriangle(kvp.Key)] = remappedOriginals;
                }
                PieceToOriginals = updatedPieceToOriginals;
            }

            // Remove triangles
            RemoveTriangles(removedTriangles);

            // Adjust triangle vertices: account for removed super-triangle vertices
            if (_superGeomType == SuperGeometryType.SuperTriangle)
            {
                for (int iT = 0; iT < Triangles.Count; iT++)
                {
                    Triangle t = Triangles[iT];
                    t.V0 -= CDTConstants.SuperTriangleVertexCount;
                    t.V1 -= CDTConstants.SuperTriangleVertexCount;
                    t.V2 -= CDTConstants.SuperTriangleVertexCount;
                    Triangles[iT] = t;
                }
            }
        }

        /// <summary>
        /// Grow from seed triangles to boundary: flood-fill that stops at fixed edges.
        /// Returns set of traversed triangles.
        /// </summary>
        private HashSet<int> GrowToBoundary(Stack<int> seeds)
        {
            var traversed = new HashSet<int>();
            while (seeds.Count > 0)
            {
                int iT = seeds.Pop();
                traversed.Add(iT);
                Triangle t = Triangles[iT];
                for (int i = 0; i < 3; i++)
                {
                    Edge opEdge = new Edge(
                        t.GetVertex(CDTUtility.Ccw(i)),
                        t.GetVertex(CDTUtility.Cw(i)));
                    if (FixedEdges.Contains(opEdge))
                        continue;
                    int iN = t.GetNeighbor(CDTUtility.OpoNbr(i));
                    if (iN != CDTConstants.NoNeighbor && !traversed.Contains(iN))
                        seeds.Push(iN);
                }
            }
            return traversed;
        }

        /// <summary>
        /// Depth-peel a layer in triangulation, used when calculating triangle depths.
        /// Traverses from seed triangles, assigns the given depth, and records seeds
        /// for the next layer behind constraint edges.
        /// </summary>
        /// <returns>Map of triangle index to its depth (seeds for deeper layers).</returns>
        private Dictionary<int, int> PeelLayer(
            Stack<int> seeds,
            int layerDepth,
            List<int> triDepths)
        {
            var newSeeds = new Dictionary<int, int>();
            while (seeds.Count > 0)
            {
                int iT = seeds.Pop();
                triDepths[iT] = layerDepth;
                Triangle t = Triangles[iT];
                for (int i = 0; i < 3; i++)
                {
                    Edge opEdge = new Edge(
                        t.GetVertex(CDTUtility.Ccw(i)),
                        t.GetVertex(CDTUtility.Cw(i)));
                    int iN = t.GetNeighbor(CDTUtility.OpoNbr(i));
                    if (iN == CDTConstants.NoNeighbor || triDepths[iN] != ushort.MaxValue)
                        continue;
                    if (FixedEdges.Contains(opEdge))
                    {
                        int newDepth = layerDepth;
                        OverlapCount.TryGetValue(opEdge, out ushort ovCount);
                        newDepth += 1 + ovCount;
                        newSeeds[iN] = newDepth;
                    }
                    else
                    {
                        seeds.Push(iN);
                    }
                }
            }
            return newSeeds;
        }

        // ══════════════════════════════════════════════
        //  Private: vertex insertion strategies
        // ══════════════════════════════════════════════

        /// <summary>Insert vertices in the order they were provided.</summary>
        private void InsertVertices_AsProvided(int superGeomVertCount)
        {
            for (int iV = superGeomVertCount; iV < Vertices.Count; iV++)
            {
                InsertVertex(iV);
            }
        }

        /// <summary>Insert vertices in a randomized order.</summary>
        private void InsertVertices_Randomized(int superGeomVertCount)
        {
            int vertexCount = Vertices.Count - superGeomVertCount;
            var ii = new List<int>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
                ii.Add(superGeomVertCount + i);

            var rng = new SplitMix64(0);
            ShuffleHelper.Shuffle(ii, ref rng);

            for (int i = 0; i < ii.Count; i++)
            {
                InsertVertex(ii[i]);
            }
        }

        /// <summary>
        /// Insert vertices using KD-tree BFS order for the initial bulk-load.
        /// This produces a well-distributed insertion order that avoids worst-case
        /// behavior in the walking point-location.
        /// </summary>
        private void InsertVertices_KDTreeBFS(int superGeomVertCount, Box2d box)
        {
            int vertexCount = Vertices.Count - superGeomVertCount;
            if (vertexCount <= 0)
                return;

            int[] ii = ShuffleHelper.Iota(vertexCount, superGeomVertCount);

            // BFS queue for KD-tree traversal
            int queueCapacity = MaxQueueLengthBFSKDTree(vertexCount);
            var queue = new FixedCapacityQueue<KDTreeBFSItem>(queueCapacity);
            queue.Push(new KDTreeBFSItem(0, ii.Length, box.Min, box.Max, 0));

            Comparison<int> cmpX = (a, b) =>
            {
                Float ax = Vertices[a].X;
                Float bx = Vertices[b].X;
                return ax < bx ? -1 : ax > bx ? 1 : 0;
            };
            Comparison<int> cmpY = (a, b) =>
            {
                Float ay = Vertices[a].Y;
                Float by = Vertices[b].Y;
                return ay < by ? -1 : ay > by ? 1 : 0;
            };

            while (!queue.Empty)
            {
                KDTreeBFSItem item = queue.Front;
                queue.Pop();

                int first = item.First;
                int last = item.Last;
                Debug.Assert(first < last);

                int len = last - first;
                if (len == 1)
                {
                    InsertVertex(ii[first], item.Parent);
                    continue;
                }

                int midIdx = first + len / 2;
                Float splitVal;
                Vector2 newBoxMin, newBoxMax;

                if (item.BoxMax.X - item.BoxMin.X >= item.BoxMax.Y - item.BoxMin.Y)
                {
                    NthElement.Perform(ii, first, midIdx, last, cmpX);
                    int mid = ii[midIdx];
                    splitVal = Vertices[mid].X;
                    newBoxMin = new Vector2(splitVal, item.BoxMin.Y);
                    newBoxMax = new Vector2(splitVal, item.BoxMax.Y);
                }
                else
                {
                    NthElement.Perform(ii, first, midIdx, last, cmpY);
                    int mid = ii[midIdx];
                    splitVal = Vertices[mid].Y;
                    newBoxMin = new Vector2(item.BoxMin.X, splitVal);
                    newBoxMax = new Vector2(item.BoxMax.X, splitVal);
                }

                int midVert = ii[midIdx];
                InsertVertex(midVert, item.Parent);

                if (first < midIdx)
                {
                    queue.Push(new KDTreeBFSItem(
                        first, midIdx, item.BoxMin, newBoxMax, midVert));
                }
                if (midIdx + 1 < last)
                {
                    queue.Push(new KDTreeBFSItem(
                        midIdx + 1, last, newBoxMin, item.BoxMax, midVert));
                }
            }
        }

        /// <summary>
        /// Calculate the maximum length of a BFS queue for a KD-tree bulk load.
        /// </summary>
        private static int MaxQueueLengthBFSKDTree(int vertexCount)
        {
            int filledLayerPow2 = (int)Math.Floor(Math.Log(vertexCount, 2) - 1);
            int nodesInFilledTree = (1 << (filledLayerPow2 + 1)) - 1;
            int nodesInLastFilledLayer = 1 << filledLayerPow2;
            int nodesInLastLayer = vertexCount - nodesInFilledTree;
            return nodesInLastLayer >= nodesInLastFilledLayer
                ? nodesInLastFilledLayer + nodesInLastLayer - nodesInLastFilledLayer
                : nodesInLastFilledLayer;
        }

        // ══════════════════════════════════════════════
        //  Private: methods defined in other partial class files
        //  (the compiler finds them across partial class files)
        // ══════════════════════════════════════════════

        // These methods are implemented in separate partial class files.
        // They are listed here as documentation of the interface boundary.
        //
        // From Triangulation algorithm partial:
        //   private void InsertVertex(int iVert)
        //   private void InsertVertex(int iVert, int walkStart)
        //   private void InsertEdge(Edge edge, Edge originalEdge,
        //       List<Edge> remaining, List<TriangulatePseudoPolygonTask> tppIterations)
        //   private void ConformToEdge(Edge edge, List<Edge> originals,
        //       ushort overlaps, List<ConformToEdgeTask> remaining)
        //   private (int, int) EdgeTriangles(int a, int b)
        //   ... and other algorithm internals

        // ══════════════════════════════════════════════
        //  Internal helper types
        // ══════════════════════════════════════════════

        /// <summary>
        /// Item for the BFS queue used in <see cref="InsertVertices_KDTreeBFS"/>.
        /// </summary>
        private struct KDTreeBFSItem
        {
            public int First;
            public int Last;
            public Vector2 BoxMin;
            public Vector2 BoxMax;
            public int Parent;

            public KDTreeBFSItem(int first, int last, Vector2 boxMin, Vector2 boxMax, int parent)
            {
                First = first;
                Last = last;
                BoxMin = boxMin;
                BoxMax = boxMax;
                Parent = parent;
            }
        }

        /// <summary>
        /// Fixed-capacity circular queue. Avoids re-allocation during KD-tree BFS.
        /// Ported from C++ <c>detail::FixedCapacityQueue</c>.
        /// </summary>
        private class FixedCapacityQueue<T>
        {
            private readonly T[] _buffer;
            private int _front;
            private int _back;
            private int _size;

            public FixedCapacityQueue(int capacity)
            {
                _buffer = new T[capacity];
                _front = 0;
                _back = 0;
                _size = 0;
            }

            public bool Empty => _size == 0;

            public T Front => _buffer[_front];

            public void Pop()
            {
                Debug.Assert(_size > 0);
                _front++;
                if (_front == _buffer.Length)
                    _front = 0;
                _size--;
            }

            public void Push(T item)
            {
                Debug.Assert(_size < _buffer.Length);
                _buffer[_back] = item;
                _back++;
                if (_back == _buffer.Length)
                    _back = 0;
                _size++;
            }
        }

    }
}
