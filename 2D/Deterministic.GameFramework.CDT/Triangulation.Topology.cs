/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

// Topology query methods for the CDT Triangulation.
// Ported from the C++ CDT library (Triangulation.hpp / Triangulation.h).
//
// These methods complement the core methods defined in Triangulation.cs
// (ChangeNeighbor, SetAdjacentTriangle, PivotVertexTriangleCW, AddTriangle,
// HasEdge, TryAddVertexToLocator, TryInitNearestPointLocator, finalization, etc.)
// by providing the triangle walking, search, and fan-walk operations.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Float = Deterministic.GameFramework.Types.Float;
using Vector2 = Deterministic.GameFramework.Types.Vector2;

namespace Deterministic.GameFramework.CDT
{
    public partial class Triangulation
    {
        // ──────────────────────────────────────────────
        //  WalkTriangles (position-based walking search)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Walk the triangulation from <paramref name="startVertex"/>'s adjacent
        /// triangle towards position <paramref name="pos"/>, returning the triangle
        /// containing (or nearest to) <paramref name="pos"/>.
        /// <para>
        /// Uses a stochastic edge-check order (randomized starting edge per step)
        /// to avoid infinite loops on degenerate/collinear cases.
        /// </para>
        /// </summary>
        /// <param name="startVertex">Vertex whose adjacent triangle is used as the walk origin.</param>
        /// <param name="pos">Target position to walk towards.</param>
        /// <returns>Index of the triangle containing or nearest to <paramref name="pos"/>.</returns>
        private int WalkTriangles(int startVertex, Vector2 pos)
        {
            int currTri = _vertTris[startVertex];
            bool found = false;
            var prng = new SplitMix64(0);
            while (!found)
            {
                Triangle t = Triangles[currTri];
                found = true;
                // stochastic offset to randomize which edge we check first
                int offset = (int)(prng.Next() % 3);
                for (int i_ = 0; i_ < 3; i_++)
                {
                    int i = (i_ + offset) % 3;
                    Vector2 vStart = Vertices[t.GetVertex(i)];
                    Vector2 vEnd = Vertices[t.GetVertex(CDTUtility.Ccw(i))];
                    PtLineLocation edgeCheck = CDTUtility.LocatePointLine(pos, vStart, vEnd);
                    int iN = t.GetNeighbor(i);
                    if (edgeCheck == PtLineLocation.Right && iN != CDTConstants.NoNeighbor)
                    {
                        found = false;
                        currTri = iN;
                        break;
                    }
                }
            }
            return currTri;
        }

        // ──────────────────────────────────────────────
        //  WalkTriangles (visitor-based fan walk)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Visit all triangles around a vertex by walking the triangle fan,
        /// calling <paramref name="visitor"/> for each triangle.
        /// Walking starts from the vertex's stored adjacent triangle and rotates CW.
        /// <para>
        /// Ported from C++ template <c>walkTriangles</c> with a visitor callable.
        /// In C# the template visitor is replaced with <c>Func&lt;int, bool&gt;</c>.
        /// </para>
        /// </summary>
        /// <param name="startVertex">The vertex around which to walk.</param>
        /// <param name="visitor">
        /// Called with each triangle index. Return <c>true</c> to continue walking,
        /// <c>false</c> to stop early.
        /// </param>
        public void WalkTriangles(int startVertex, Func<int, bool> visitor)
        {
            int startTri = _vertTris[startVertex];
            int iT = startTri;
            do
            {
                if (!visitor(iT))
                    return;
                iT = Triangles[iT].Next(startVertex).TriInd;
                Debug.Assert(iT != CDTConstants.NoNeighbor);
            } while (iT != startTri);
        }

        // ──────────────────────────────────────────────
        //  WalkingSearchTrianglesAt
        // ──────────────────────────────────────────────

        /// <summary>
        /// Walk the triangulation to find the triangle(s) containing vertex
        /// <paramref name="iV"/>. Start from <paramref name="startVertex"/>'s triangle.
        /// <para>
        /// Returns a named tuple (T0, T1):
        /// <list type="bullet">
        /// <item><description>(triangle, NoNeighbor) if the vertex is strictly inside a triangle.</description></item>
        /// <item><description>(triangle1, triangle2) if the vertex lies on an edge shared by two triangles.</description></item>
        /// </list>
        /// Throws <see cref="CDTException"/> if the position is outside all triangles.
        /// Throws <see cref="DuplicateVertexError"/> if the position coincides with an existing vertex.
        /// </para>
        /// </summary>
        /// <param name="iV">Index of the vertex to locate.</param>
        /// <param name="startVertex">Vertex whose adjacent triangle is used as the walk origin.</param>
        private (int T0, int T1) WalkingSearchTrianglesAt(int iV, int startVertex)
        {
            Vector2 v = Vertices[iV];
            int iT = WalkTriangles(startVertex, v);

            // Finished walk, locate point in current triangle
            Triangle t = Triangles[iT];
            Vector2 v1 = Vertices[t.V0];
            Vector2 v2 = Vertices[t.V1];
            Vector2 v3 = Vertices[t.V2];
            PtTriLocation loc = CDTUtility.LocatePointTriangle(v, v1, v2, v3);

            if (loc == PtTriLocation.Outside)
            {
                throw new CDTException("No triangle was found at position");
            }
            if (loc == PtTriLocation.OnVertex)
            {
                int iDupe = v1.Equals(v) ? t.V0
                          : v2.Equals(v) ? t.V1
                          : t.V2;
                throw new DuplicateVertexError(
                    iV - _nTargetVerts,
                    iDupe - _nTargetVerts);
            }

            int t1 = CDTConstants.NoNeighbor;
            if (CDTUtility.IsOnEdge(loc))
                t1 = t.GetNeighbor(CDTUtility.EdgeNeighborIndex(loc));

            return (iT, t1);
        }

        // ──────────────────────────────────────────────
        //  TrianglesAt (brute-force point location)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Brute-force search for the triangle(s) containing position <paramref name="pos"/>.
        /// Iterates over all triangles and performs a point-in-triangle test.
        /// <para>
        /// Returns a named tuple (T0, T1):
        /// <list type="bullet">
        /// <item><description>(triangle, NoNeighbor) if the point is strictly inside a triangle.</description></item>
        /// <item><description>(triangle1, triangle2) if the point lies on an edge shared by two triangles.</description></item>
        /// </list>
        /// Throws <see cref="CDTException"/> if no triangle contains the position.
        /// </para>
        /// </summary>
        /// <param name="pos">Position to search for.</param>
        public (int T0, int T1) TrianglesAt(Vector2 pos)
        {
            for (int i = 0; i < Triangles.Count; i++)
            {
                Triangle t = Triangles[i];
                Vector2 v1 = Vertices[t.V0];
                Vector2 v2 = Vertices[t.V1];
                Vector2 v3 = Vertices[t.V2];
                PtTriLocation loc = CDTUtility.LocatePointTriangle(pos, v1, v2, v3);
                if (loc == PtTriLocation.Outside)
                    continue;
                int t1 = CDTConstants.NoNeighbor;
                if (CDTUtility.IsOnEdge(loc))
                    t1 = t.GetNeighbor(CDTUtility.EdgeNeighborIndex(loc));
                return (i, t1);
            }
            throw new CDTException("No triangle was found at position");
        }

        // ──────────────────────────────────────────────
        //  TrianglesAroundVertex (full fan walk)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Collect all triangle indices sharing vertex <paramref name="iVertex"/>
        /// by walking the full fan around the vertex (CW rotation).
        /// </summary>
        /// <param name="iVertex">Vertex index to walk around.</param>
        /// <returns>List of all triangle indices adjacent to the vertex.</returns>
        public List<int> TrianglesAroundVertex(int iVertex)
        {
            var result = new List<int>();
            int startTri = _vertTris[iVertex];
            int iT = startTri;
            do
            {
                result.Add(iT);
                iT = Triangles[iT].Next(iVertex).TriInd;
                Debug.Assert(iT != CDTConstants.NoNeighbor);
            } while (iT != startTri);
            return result;
        }

        // ──────────────────────────────────────────────
        //  EdgeTriangles
        // ──────────────────────────────────────────────

        /// <summary>
        /// Find the two triangles sharing edge (iA, iB).
        /// Walks CW around vertex <paramref name="iA"/> looking for vertex
        /// <paramref name="iB"/> in the Next() traversal.
        /// <para>
        /// Returns (T0, T1) where T0 is the triangle with edge iA->iB in CW order
        /// and T1 is the triangle on the other side. Returns (NoNeighbor, NoNeighbor)
        /// if no such edge exists.
        /// </para>
        /// </summary>
        /// <param name="iA">First vertex of the edge.</param>
        /// <param name="iB">Second vertex of the edge.</param>
        private (int, int) EdgeTriangles(int iA, int iB)
        {
            int triStart = _vertTris[iA];
            Debug.Assert(triStart != CDTConstants.NoNeighbor);
            int iT = triStart;
            do
            {
                Triangle t = Triangles[iT];
                var next = t.Next(iA);
                int iTNext = next.TriInd;
                int iV = next.VertInd;
                Debug.Assert(iTNext != CDTConstants.NoNeighbor);
                if (iV == iB)
                {
                    return (iT, iTNext);
                }
                iT = iTNext;
            } while (iT != triStart);
            return (CDTConstants.NoNeighbor, CDTConstants.NoNeighbor);
        }

        // ──────────────────────────────────────────────
        //  MakeDummy
        // ──────────────────────────────────────────────

        /// <summary>
        /// Make triangle <paramref name="iT"/> into a dummy/placeholder
        /// by resetting it to an empty triangle (all vertices and neighbors
        /// set to their sentinel "no" values).
        /// </summary>
        /// <param name="iT">Index of the triangle to reset.</param>
        public void MakeDummy(int iT)
        {
            Triangles[iT] = Triangle.Empty;
        }
    }
}
