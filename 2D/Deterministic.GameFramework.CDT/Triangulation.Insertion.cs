/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

// Ported from CDT C++ library: vertex insertion & Delaunay edge-flip methods.
// Source: Triangulation.hpp / Triangulation.h
//
// This partial class file provides the core insertion algorithm methods that are
// referenced by the public API in Triangulation.cs but implemented here:
//   InsertVertex (two overloads), InsertVertexInsideTriangle,
//   InsertVertexOnEdge, EnsureDelaunayByEdgeFlips, InsertVertex_FlipFixedEdges,
//   EdgeFlipInfo, IsFlipNeeded

using System.Collections.Generic;

using Vector2 = Deterministic.GameFramework.Types.Vector2;
using Float = Deterministic.GameFramework.Types.Float;

namespace Deterministic.GameFramework.CDT
{
    public partial class Triangulation
    {
        // ────────────────────────────────────────────────────────────────────
        //  insertVertex (two overloads)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Insert vertex at index <paramref name="iVert"/> using a walking search
        /// starting from <paramref name="walkStart"/>.
        /// Locates the triangle containing the vertex, splits it, and restores
        /// the Delaunay property via edge flips.
        /// </summary>
        private void InsertVertex(int iVert, int walkStart)
        {
            var trisAt = WalkingSearchTrianglesAt(iVert, walkStart);

            Stack<int> triStack;
            if (trisAt.Item2 == CDTConstants.NoNeighbor)
                triStack = InsertVertexInsideTriangle(iVert, trisAt.Item1);
            else
                triStack = InsertVertexOnEdge(iVert, trisAt.Item1, trisAt.Item2, true);

            EnsureDelaunayByEdgeFlips(iVert, triStack);
        }

        /// <summary>
        /// Insert vertex at index <paramref name="iVert"/> using the nearest-point
        /// locator to find a good walk-start vertex. After insertion the vertex is
        /// added to the locator.
        /// </summary>
        private void InsertVertex(int iVert)
        {
            Vector2 v = Vertices[iVert];
            int walkStart = _nearPtLocator.NearPoint(v, Vertices);
            InsertVertex(iVert, walkStart);
            TryAddVertexToLocator(iVert);
        }

        // ────────────────────────────────────────────────────────────────────
        //  insertVertexInsideTriangle
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Insert a vertex inside a triangle by splitting it into 3 new triangles.
        /// Two new triangles are created; the original triangle is re-used as the third.
        /// <code>
        ///                    v3
        ///                  / | \
        ///                 /  |  \  &lt;-- original triangle (t)
        ///                /   |   \
        ///            n3 /    |    \ n2
        ///              /newT2|newT1\
        ///             /      v      \
        ///            /    __/ \__    \
        ///           /  __/       \__  \
        ///          / _/      t'     \_ \
        ///        v1 ___________________ v2
        ///                   n1
        /// </code>
        /// </summary>
        /// <returns>Stack of new triangle indices for Delaunay edge-flip checking.</returns>
        private Stack<int> InsertVertexInsideTriangle(int v, int iT)
        {
            int iNewT1 = AddTriangle();
            int iNewT2 = AddTriangle();

            Triangle t = Triangles[iT];
            int v1 = t.V0, v2 = t.V1, v3 = t.V2;
            int n1 = t.N0, n2 = t.N1, n3 = t.N2;

            // make two new triangles and convert current triangle to 3rd
            Triangles[iNewT1] = new Triangle(v2, v3, v, n2, iNewT2, iT);
            Triangles[iNewT2] = new Triangle(v3, v1, v, n3, iT, iNewT1);
            Triangles[iT] = new Triangle(v1, v2, v, n1, iNewT1, iNewT2);

            // adjust adjacent triangles
            SetAdjacentTriangle(v, iT);
            SetAdjacentTriangle(v3, iNewT1);

            // change triangle neighbor's neighbors to new triangles
            ChangeNeighbor(n2, iT, iNewT1);
            ChangeNeighbor(n3, iT, iNewT2);

            // return newly added triangles
            var newTriangles = new Stack<int>(4);
            newTriangles.Push(iT);
            newTriangles.Push(iNewT1);
            newTriangles.Push(iNewT2);
            return newTriangles;
        }

        // ────────────────────────────────────────────────────────────────────
        //  insertVertexOnEdge
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Insert a vertex on an edge shared by two triangles, splitting them
        /// into 4 triangles total.
        /// <code>
        ///   T1 (top)        v1
        ///                  /|\
        ///             n1 /  |  \ n4
        ///              /    |    \
        ///            /  T1' | Tnew1\
        ///          v2-------v-------v4
        ///            \  T2' | Tnew2/
        ///              \    |    /
        ///             n2 \  |  / n3
        ///                  \|/
        ///   T2 (bottom)    v3
        /// </code>
        /// </summary>
        /// <returns>Stack of new triangle indices for Delaunay edge-flip checking.</returns>
        private Stack<int> InsertVertexOnEdge(
            int v, int iT1, int iT2, bool doHandleFixedSplitEdge = false)
        {
            int iTnew1 = AddTriangle();
            int iTnew2 = AddTriangle();

            Triangle t1 = Triangles[iT1];
            Triangle t2 = Triangles[iT2];

            // t1: find the vertex opposed to t2
            int i1 = CDTUtility.OpposedVertexInd(in t1, iT2);
            int v1 = t1.GetVertex(i1);
            int v2 = t1.GetVertex(CDTUtility.Ccw(i1));
            int n1 = t1.GetNeighbor(i1);
            int n4 = t1.GetNeighbor(CDTUtility.Cw(i1));

            // t2: find the vertex opposed to t1
            int i2 = CDTUtility.OpposedVertexInd(in t2, iT1);
            int v3 = t2.GetVertex(i2);
            int v4 = t2.GetVertex(CDTUtility.Ccw(i2));
            int n3 = t2.GetNeighbor(i2);
            int n2 = t2.GetNeighbor(CDTUtility.Cw(i2));

            // add new triangles and change existing ones
            Triangles[iT1] = new Triangle(v, v1, v2, iTnew1, n1, iT2);
            Triangles[iT2] = new Triangle(v, v2, v3, iT1, n2, iTnew2);
            Triangles[iTnew1] = new Triangle(v, v4, v1, iTnew2, n4, iT1);
            Triangles[iTnew2] = new Triangle(v, v3, v4, iT2, n3, iTnew1);

            // adjust adjacent triangles
            SetAdjacentTriangle(v, iT1);
            SetAdjacentTriangle(v4, iTnew1);

            // adjust neighboring triangles and vertices
            ChangeNeighbor(n4, iT1, iTnew1);
            ChangeNeighbor(n3, iT2, iTnew2);

            // properly handle the case when the split edge is a fixed edge
            if (doHandleFixedSplitEdge)
            {
                Edge sharedEdge = new Edge(v2, v4);
                if (FixedEdges.Contains(sharedEdge))
                    SplitFixedEdge(sharedEdge, v);
            }

            // return newly added triangles
            var newTriangles = new Stack<int>(4);
            newTriangles.Push(iT1);
            newTriangles.Push(iTnew2);
            newTriangles.Push(iT2);
            newTriangles.Push(iTnew1);
            return newTriangles;
        }

        // ────────────────────────────────────────────────────────────────────
        //  ensureDelaunayByEdgeFlips
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Process a stack of triangles and flip edges as needed to restore
        /// the Delaunay property after a vertex insertion.
        /// </summary>
        private void EnsureDelaunayByEdgeFlips(int iV1, Stack<int> triStack)
        {
            int iTopo, n1, n2, n3, n4;
            int iV2, iV3, iV4;
            while (triStack.Count > 0)
            {
                int iT = triStack.Pop();

                EdgeFlipInfo(iT, iV1, out iTopo, out iV2, out iV3, out iV4,
                    out n1, out n2, out n3, out n4);

                if (iTopo != CDTConstants.NoNeighbor && IsFlipNeeded(iV1, iV2, iV3, iV4))
                {
                    FlipEdge(iT, iTopo, iV1, iV2, iV3, iV4, n1, n2, n3, n4);
                    triStack.Push(iT);
                    triStack.Push(iTopo);
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  insertVertex_FlipFixedEdges
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Insert a vertex using the nearest-point locator and walking search,
        /// performing edge flips that may flip fixed edges. Returns a list of any
        /// fixed edges that were flipped (so the caller can re-insert them).
        /// </summary>
        private List<Edge> InsertVertex_FlipFixedEdges(int iV1)
        {
            var flippedFixedEdges = new List<Edge>();

            Vector2 v1 = Vertices[iV1];
            int startVertex = _nearPtLocator.NearPoint(v1, Vertices);

            var trisAt = WalkingSearchTrianglesAt(iV1, startVertex);

            Stack<int> triStack;
            if (trisAt.Item2 == CDTConstants.NoNeighbor)
                triStack = InsertVertexInsideTriangle(iV1, trisAt.Item1);
            else
                triStack = InsertVertexOnEdge(iV1, trisAt.Item1, trisAt.Item2);

            int iTopo, n1, n2, n3, n4;
            int iV2, iV3, iV4;
            while (triStack.Count > 0)
            {
                int iT = triStack.Pop();

                EdgeFlipInfo(iT, iV1, out iTopo, out iV2, out iV3, out iV4,
                    out n1, out n2, out n3, out n4);

                if (iTopo != CDTConstants.NoNeighbor && IsFlipNeeded(iV1, iV2, iV3, iV4))
                {
                    // if flipped edge is fixed, remember it
                    Edge flippedEdge = new Edge(iV2, iV4);
                    if (FixedEdges.Count > 0 && FixedEdges.Contains(flippedEdge))
                    {
                        flippedFixedEdges.Add(flippedEdge);
                    }

                    FlipEdge(iT, iTopo, iV1, iV2, iV3, iV4, n1, n2, n3, n4);
                    triStack.Push(iT);
                    triStack.Push(iTopo);
                }
            }

            TryAddVertexToLocator(iV1);
            return flippedFixedEdges;
        }

        // ────────────────────────────────────────────────────────────────────
        //  edgeFlipInfo
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Given a triangle <paramref name="iT"/> and one of its vertices
        /// <paramref name="iV1"/>, find the opposite triangle and surrounding
        /// vertex/neighbor topology for a potential edge flip.
        /// <code>
        ///                    v4         original edge: (v1, v3)
        ///                   /|\   flip-candidate edge: (v,  v2)
        ///                 /  |  \
        ///           n3  /    |    \  n4
        ///             /      |      \
        ///  new vertex v1    T | Topo  v3
        ///             \      |      /
        ///           n1  \    |    /  n2
        ///                 \  |  /
        ///                   \|/
        ///                    v2
        /// </code>
        /// </summary>
        private void EdgeFlipInfo(
            int iT, int iV1,
            out int iTopo, out int iV2, out int iV3, out int iV4,
            out int n1, out int n2, out int n3, out int n4)
        {
            Triangle t = Triangles[iT];
            if (t.V0 == iV1)
            {
                iV2 = t.V1;
                iV4 = t.V2;
                n1 = t.N0;
                n3 = t.N2;
                iTopo = t.N1;
            }
            else if (t.V1 == iV1)
            {
                iV2 = t.V2;
                iV4 = t.V0;
                n1 = t.N1;
                n3 = t.N0;
                iTopo = t.N2;
            }
            else
            {
                iV2 = t.V0;
                iV4 = t.V1;
                n1 = t.N2;
                n3 = t.N1;
                iTopo = t.N0;
            }

            // defaults for when there is no opposite triangle
            iV3 = 0;
            n2 = 0;
            n4 = 0;

            if (iTopo == CDTConstants.NoNeighbor)
                return;

            Triangle tOpo = Triangles[iTopo];
            if (tOpo.N0 == iT)
            {
                iV3 = tOpo.V2;
                n2 = tOpo.N1;
                n4 = tOpo.N2;
            }
            else if (tOpo.N1 == iT)
            {
                iV3 = tOpo.V0;
                n2 = tOpo.N2;
                n4 = tOpo.N0;
            }
            else
            {
                iV3 = tOpo.V1;
                n2 = tOpo.N0;
                n4 = tOpo.N1;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  isFlipNeeded
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Determine whether an edge flip is needed to maintain the Delaunay
        /// property. Handles super-triangle vertices specially.
        /// <para>
        /// Three cases for super-triangle handling:
        /// 1. If one of the opposed vertices (v1 or v3) is super-tri: no flip needed
        /// 2. One of the shared vertices (v2 or v4) is super-tri: orient2d test
        /// 3. None are super-tri: standard incircle test
        /// </para>
        /// <code>
        ///                    v4         original edge: (v2, v4)
        ///                   /|\   flip-candidate edge: (v1, v3)
        ///                 /  |  \
        ///               /    |    \
        ///             /      |      \
        ///  new vertex v1     |       v3
        ///             \      |      /
        ///               \    |    /
        ///                 \  |  /
        ///                   \|/
        ///                    v2
        /// </code>
        /// </summary>
        private bool IsFlipNeeded(int iV1, int iV2, int iV3, int iV4)
        {
            if (FixedEdges.Contains(new Edge(iV2, iV4)))
                return false; // flip not needed if the original edge is fixed

            Vector2 v1 = Vertices[iV1];
            Vector2 v2 = Vertices[iV2];
            Vector2 v3 = Vertices[iV3];
            Vector2 v4 = Vertices[iV4];

            if (_superGeomType == SuperGeometryType.SuperTriangle)
            {
                // If flip-candidate edge touches super-triangle, the in-circumcircle
                // test must be replaced with orient2d test against the line formed by
                // the two non-artificial vertices.
                if (iV1 < 3) // flip-candidate edge touches super-triangle
                {
                    // does original edge also touch super-triangle?
                    if (iV2 < 3)
                        return CDTUtility.LocatePointLine(v2, v3, v4) ==
                               CDTUtility.LocatePointLine(v1, v3, v4);
                    if (iV4 < 3)
                        return CDTUtility.LocatePointLine(v4, v2, v3) ==
                               CDTUtility.LocatePointLine(v1, v2, v3);
                    return false; // original edge does not touch super-triangle
                }
                if (iV3 < 3) // flip-candidate edge touches super-triangle
                {
                    if (iV2 < 3)
                        return CDTUtility.LocatePointLine(v2, v1, v4) ==
                               CDTUtility.LocatePointLine(v3, v1, v4);
                    if (iV4 < 3)
                        return CDTUtility.LocatePointLine(v4, v2, v1) ==
                               CDTUtility.LocatePointLine(v3, v2, v1);
                    return false; // original edge does not touch super-triangle
                }
                // flip-candidate edge does not touch super-triangle
                if (iV2 < 3)
                    return CDTUtility.LocatePointLine(v2, v3, v4) ==
                           CDTUtility.LocatePointLine(v1, v3, v4);
                if (iV4 < 3)
                    return CDTUtility.LocatePointLine(v4, v2, v3) ==
                           CDTUtility.LocatePointLine(v1, v2, v3);
            }

            return CDTUtility.IsInCircumcircle(v1, v2, v3, v4);
        }
    }
}
