/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

// Constraint edge insertion methods, ported from CDT C++ library:
//   Triangulation.hpp — insertEdge, insertEdgeIteration, conformToEdge,
//   conformToEdgeIteration, intersectedTriangle, triangulatePseudoPolygon,
//   triangulatePseudoPolygonIteration, findDelaunayPoint,
//   splitFixedEdge, addSplitEdgeVertex, splitFixedEdgeAt.
//
// FixEdge (both overloads) and the task types (TriangulatePseudoPolygonTask,
// ConformToEdgeTask) are defined in Triangulation.cs.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Float = Deterministic.GameFramework.Types.Float;
using Vector2 = Deterministic.GameFramework.Types.Vector2;

namespace Deterministic.GameFramework.CDT
{
    public partial class Triangulation
    {
        // ────────────────────────────────────────────────────────────
        //  splitFixedEdge — split an existing constraint edge at a vertex
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Split an existing fixed (constraint) edge into two halves at
        /// <paramref name="iSplitVert"/>. Updates FixedEdges, OverlapCount,
        /// and PieceToOriginals accordingly.
        /// </summary>
        private void SplitFixedEdge(Edge edge, int iSplitVert)
        {
            Edge half1 = new Edge(edge.V1, iSplitVert);
            Edge half2 = new Edge(iSplitVert, edge.V2);

            // remove the original edge and add its halves
            FixedEdges.Remove(edge);
            FixEdge(half1);
            FixEdge(half2);

            // maintain overlap counts
            if (OverlapCount.TryGetValue(edge, out ushort count))
            {
                if (OverlapCount.TryGetValue(half1, out ushort c1))
                    OverlapCount[half1] = (ushort)(c1 + count);
                else
                    OverlapCount[half1] = count;

                if (OverlapCount.TryGetValue(half2, out ushort c2))
                    OverlapCount[half2] = (ushort)(c2 + count);
                else
                    OverlapCount[half2] = count;

                OverlapCount.Remove(edge);
            }

            // maintain piece-to-original mapping
            List<Edge> newOriginals;
            if (PieceToOriginals.TryGetValue(edge, out var existingOriginals))
            {
                // edge being split was split before: pass-through originals
                newOriginals = existingOriginals;
                PieceToOriginals.Remove(edge);
            }
            else
            {
                newOriginals = new List<Edge>(1) { edge };
            }

            InsertUnique(PieceToOriginals, half1, newOriginals);
            InsertUnique(PieceToOriginals, half2, newOriginals);
        }

        // ────────────────────────────────────────────────────────────
        //  addSplitEdgeVertex — add a new vertex that splits an edge
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Add a new vertex at <paramref name="splitVert"/> on the edge
        /// between triangles <paramref name="iT"/> and <paramref name="iTopo"/>.
        /// Inserts the vertex into the triangulation and restores the
        /// Delaunay property.
        /// </summary>
        /// <returns>Index of the newly created split vertex.</returns>
        private int AddSplitEdgeVertex(Vector2 splitVert, int iT, int iTopo)
        {
            int iSplitVert = Vertices.Count;
            AddNewVertex(splitVert, CDTConstants.NoNeighbor);

            Stack<int> triStack = InsertVertexOnEdge(iSplitVert, iT, iTopo);
            TryAddVertexToLocator(iSplitVert);
            EnsureDelaunayByEdgeFlips(iSplitVert, triStack);
            return iSplitVert;
        }

        // ────────────────────────────────────────────────────────────
        //  splitFixedEdgeAt — split fixed edge and add a split vertex
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Split the fixed edge at <paramref name="splitVert"/> and update
        /// the triangulation topology.
        /// </summary>
        /// <returns>Index of the newly created split vertex.</returns>
        private int SplitFixedEdgeAt(Edge edge, Vector2 splitVert, int iT, int iTopo)
        {
            int iSplitVert = AddSplitEdgeVertex(splitVert, iT, iTopo);
            SplitFixedEdge(edge, iSplitVert);
            return iSplitVert;
        }

        // ────────────────────────────────────────────────────────────
        //  intersectedTriangle
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Starting from vertex <paramref name="iA"/>, walk the triangle fan
        /// to find the triangle intersected by edge a-&gt;b.
        /// </summary>
        /// <returns>
        /// A tuple (triangleIndex, iVLeft, iVRight).
        /// If a vertex lies exactly on the line, triangleIndex is
        /// <see cref="CDTConstants.NoNeighbor"/> and both left/right
        /// return the on-line vertex index.
        /// </returns>
        private (int iT, int iVLeft, int iVRight) IntersectedTriangle(
            int iA,
            Vector2 a,
            Vector2 b,
            Float orientationTolerance)
        {
            int startTri = _vertTris[iA];
            int iT = startTri;
            do
            {
                Triangle t = Triangles[iT];
                int i = CDTUtility.VertexInd(in t, iA);
                int iP2 = t.GetVertex(CDTUtility.Ccw(i));
                Float orientP2 = CDTUtility.Orient2D(Vertices[iP2], a, b);
                PtLineLocation locP2 = CDTUtility.ClassifyOrientation(orientP2);
                if (locP2 == PtLineLocation.Right)
                {
                    int iP1 = t.GetVertex(CDTUtility.Cw(i));
                    Float orientP1 = CDTUtility.Orient2D(Vertices[iP1], a, b);
                    PtLineLocation locP1 = CDTUtility.ClassifyOrientation(orientP1);
                    if (locP1 == PtLineLocation.OnLine)
                    {
                        return (CDTConstants.NoNeighbor, iP1, iP1);
                    }
                    if (locP1 == PtLineLocation.Left)
                    {
                        if (orientationTolerance != (Float)0)
                        {
                            Float closestOrient;
                            int iClosestP;
                            if (Float.Abs(orientP1) <= Float.Abs(orientP2))
                            {
                                closestOrient = orientP1;
                                iClosestP = iP1;
                            }
                            else
                            {
                                closestOrient = orientP2;
                                iClosestP = iP2;
                            }
                            if (CDTUtility.ClassifyOrientation(closestOrient, orientationTolerance) ==
                                PtLineLocation.OnLine)
                            {
                                return (CDTConstants.NoNeighbor, iClosestP, iClosestP);
                            }
                        }
                        return (iT, iP1, iP2);
                    }
                }
                iT = t.Next(iA).TriInd;
            } while (iT != startTri);

            throw new CDTException("Could not find vertex triangle intersected by an edge.");
        }

        // ────────────────────────────────────────────────────────────
        //  insertEdge — main entry point for constraint edge insertion
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Insert a constraint edge (or part thereof) into the triangulation.
        /// Uses iteration over recursion to avoid stack overflows.
        /// </summary>
        private void InsertEdge(
            Edge edge,
            Edge originalEdge,
            List<Edge> remaining,
            List<TriangulatePseudoPolygonTask> tppIterations)
        {
            // use iteration over recursion to avoid stack overflows
            remaining.Clear();
            remaining.Add(edge);
            while (remaining.Count > 0)
            {
                edge = remaining[remaining.Count - 1];
                remaining.RemoveAt(remaining.Count - 1);
                InsertEdgeIteration(edge, originalEdge, remaining, tppIterations);
            }
        }

        // ────────────────────────────────────────────────────────────
        //  insertEdgeIteration — single step of edge insertion
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Single iteration of constraint edge insertion. Finds intersected
        /// triangles, builds pseudo-polygons on both sides, and re-triangulates.
        /// </summary>
        private void InsertEdgeIteration(
            Edge edge,
            Edge originalEdge,
            List<Edge> remaining,
            List<TriangulatePseudoPolygonTask> tppIterations)
        {
            int iA = edge.V1;
            int iB = edge.V2;
            if (iA == iB) // edge connects a vertex to itself
                return;

            if (HasEdge(iA, iB))
            {
                FixEdge(edge, originalEdge);
                return;
            }

            Vector2 a = Vertices[iA];
            Vector2 b = Vertices[iB];
            Float distanceTolerance =
                _minDistToConstraintEdge == (Float)0
                    ? (Float)0
                    : _minDistToConstraintEdge * CDTUtility.Distance(a, b);

            // Note: 'L' is left and 'R' is right of the inserted constraint edge
            var (iT, iVL, iVR) = IntersectedTriangle(iA, a, b, distanceTolerance);

            // if one of the triangle vertices is on the edge, move edge start
            if (iT == CDTConstants.NoNeighbor)
            {
                Edge edgePart = new Edge(iA, iVL);
                FixEdge(edgePart, originalEdge);
                remaining.Add(new Edge(iVL, iB));
                return;
            }

            Triangle t = Triangles[iT];
            List<int> intersected = new List<int> { iT };
            List<int> polyL = new List<int>(2) { iA, iVL };
            List<int> polyR = new List<int>(2) { iA, iVR };
            Dictionary<Edge, int> outerTris = new Dictionary<Edge, int>();
            outerTris[new Edge(iA, iVL)] = CDTUtility.EdgeNeighbor(in t, iA, iVL);
            outerTris[new Edge(iA, iVR)] = CDTUtility.EdgeNeighbor(in t, iA, iVR);
            int iV = iA;

            while (!t.ContainsVertex(iB))
            {
                int iTopo = CDTUtility.OpposedTriangle(in t, iV);
                Triangle tOpo = Triangles[iTopo];
                int iVopo = CDTUtility.OpposedVertex(in tOpo, iT);

                switch (_intersectingEdgesStrategy)
                {
                    case IntersectingConstraintEdges.NotAllowed:
                        if (FixedEdges.Contains(new Edge(iVL, iVR)))
                        {
                            // report original input edges in the exception
                            Edge e1 = originalEdge;
                            Edge e2 = new Edge(iVL, iVR);
                            if (PieceToOriginals.TryGetValue(e2, out var e2Originals) && e2Originals.Count > 0)
                                e2 = e2Originals[0];
                            // don't count super-triangle vertices
                            e1 = new Edge(e1.V1 - _nTargetVerts, e1.V2 - _nTargetVerts);
                            e2 = new Edge(e2.V1 - _nTargetVerts, e2.V2 - _nTargetVerts);
                            throw new IntersectingConstraintsError(e1, e2);
                        }
                        break;

                    case IntersectingConstraintEdges.TryResolve:
                    {
                        if (!FixedEdges.Contains(new Edge(iVL, iVR)))
                            break;
                        // split edge at the intersection of two constraint edges
                        Vector2 newV = CDTUtility.IntersectionPosition(
                            Vertices[iA], Vertices[iB], Vertices[iVL], Vertices[iVR]);
                        int iNewVert =
                            SplitFixedEdgeAt(new Edge(iVL, iVR), newV, iT, iTopo);
                        remaining.Add(new Edge(iA, iNewVert));
                        remaining.Add(new Edge(iNewVert, iB));
                        return;
                    }

                    case IntersectingConstraintEdges.DontCheck:
                        Debug.Assert(!FixedEdges.Contains(new Edge(iVL, iVR)));
                        break;
                }

                PtLineLocation loc =
                    CDTUtility.LocatePointLine(Vertices[iVopo], a, b, distanceTolerance);
                if (loc == PtLineLocation.Left)
                {
                    Edge e = new Edge(polyL[polyL.Count - 1], iVopo);
                    int outer = CDTUtility.EdgeNeighbor(in tOpo, e.V1, e.V2);
                    if (!outerTris.TryAdd(e, outer))
                        outerTris[e] = CDTConstants.NoNeighbor; // hanging edge detected
                    polyL.Add(iVopo);
                    iV = iVL;
                    iVL = iVopo;
                }
                else if (loc == PtLineLocation.Right)
                {
                    Edge e = new Edge(polyR[polyR.Count - 1], iVopo);
                    int outer = CDTUtility.EdgeNeighbor(in tOpo, e.V1, e.V2);
                    if (!outerTris.TryAdd(e, outer))
                        outerTris[e] = CDTConstants.NoNeighbor; // hanging edge detected
                    polyR.Add(iVopo);
                    iV = iVR;
                    iVR = iVopo;
                }
                else // encountered point on the edge
                {
                    iB = iVopo;
                }

                intersected.Add(iTopo);
                iT = iTopo;
                t = Triangles[iT];
            }

            outerTris[new Edge(polyL[polyL.Count - 1], iB)] =
                CDTUtility.EdgeNeighbor(in t, polyL[polyL.Count - 1], iB);
            outerTris[new Edge(polyR[polyR.Count - 1], iB)] =
                CDTUtility.EdgeNeighbor(in t, polyR[polyR.Count - 1], iB);
            polyL.Add(iB);
            polyR.Add(iB);

            Debug.Assert(intersected.Count > 0);

            // make sure start/end vertices have a valid adjacent triangle
            // that is not intersected by an edge
            if (_vertTris[iA] == intersected[0])
                PivotVertexTriangleCW(iA);
            if (_vertTris[iB] == intersected[intersected.Count - 1])
                PivotVertexTriangleCW(iB);

            {
                // Triangulate pseudo-polygons on both sides
                polyR.Reverse();

                // note: intersected triangles are re-used for new triangles
                // every triangulation of an n-gon has n - 2 triangles
                Debug.Assert(intersected.Count >= 2);
                int iTL = intersected[intersected.Count - 1];
                intersected.RemoveAt(intersected.Count - 1);
                int iTR = intersected[intersected.Count - 1];
                intersected.RemoveAt(intersected.Count - 1);

                TriangulatePseudoPolygon(
                    polyL, outerTris, iTL, iTR, intersected, tppIterations);
                TriangulatePseudoPolygon(
                    polyR, outerTris, iTR, iTL, intersected, tppIterations);
                Debug.Assert(intersected.Count == 0);
            }

            if (iB != edge.V2) // encountered point on the edge
            {
                // fix edge part
                Edge edgePart = new Edge(iA, iB);
                FixEdge(edgePart, originalEdge);
                remaining.Add(new Edge(iB, edge.V2));
            }
            else
            {
                FixEdge(edge, originalEdge);
            }
        }

        // ────────────────────────────────────────────────────────────
        //  conformToEdge — conforming edge insertion (may add midpoints)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Conform the triangulation to a fixed edge by recursively inserting
        /// midpoints of the edge until the original edge is represented by a
        /// sequence of its pieces. Uses iteration to avoid stack overflows.
        /// </summary>
        private void ConformToEdge(
            Edge edge,
            List<Edge> originals,
            ushort overlaps,
            List<ConformToEdgeTask> remaining)
        {
            // use iteration over recursion to avoid stack overflows
            remaining.Clear();
            remaining.Add(new ConformToEdgeTask(edge, originals, overlaps));
            while (remaining.Count > 0)
            {
                ConformToEdgeTask task = remaining[remaining.Count - 1];
                remaining.RemoveAt(remaining.Count - 1);
                ConformToEdgeIteration(task.Edge, task.Originals, task.OverlapCount, remaining);
            }
        }

        // ────────────────────────────────────────────────────────────
        //  conformToEdgeIteration — single step of conforming insertion
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Single iteration of conforming-to-edge. Walks the triangulation
        /// along the edge, adds a midpoint vertex when the edge is not present,
        /// and queues the two halves for further processing.
        /// </summary>
        private void ConformToEdgeIteration(
            Edge edge,
            List<Edge> originals,
            ushort overlaps,
            List<ConformToEdgeTask> remaining)
        {
            int iA = edge.V1;
            int iB = edge.V2;
            if (iA == iB) // edge connects a vertex to itself
                return;

            if (HasEdge(iA, iB))
            {
                FixEdge(edge);
                if (overlaps > 0)
                    OverlapCount[edge] = overlaps;
                // avoid marking edge as a part of itself
                if (originals.Count > 0 && edge != originals[0])
                {
                    InsertUnique(PieceToOriginals, edge, originals);
                }
                return;
            }

            Vector2 a = Vertices[iA];
            Vector2 b = Vertices[iB];
            Float distanceTolerance =
                _minDistToConstraintEdge == (Float)0
                    ? (Float)0
                    : _minDistToConstraintEdge * CDTUtility.Distance(a, b);

            var (iT, iVleft, iVright) = IntersectedTriangle(iA, a, b, distanceTolerance);

            // if one of the triangle vertices is on the edge, move edge start
            if (iT == CDTConstants.NoNeighbor)
            {
                Edge edgePart = new Edge(iA, iVleft);
                FixEdge(edgePart);
                if (overlaps > 0)
                    OverlapCount[edgePart] = overlaps;
                InsertUnique(PieceToOriginals, edgePart, originals);
                remaining.Add(new ConformToEdgeTask(
                    new Edge(iVleft, iB), originals, overlaps));
                return;
            }

            int iV = iA;
            Triangle t = Triangles[iT];
            while (!t.ContainsVertex(iB))
            {
                int iTopo = CDTUtility.OpposedTriangle(in t, iV);
                Triangle tOpo = Triangles[iTopo];
                int iVopo = CDTUtility.OpposedVertex(in tOpo, iT);
                Vector2 vOpo = Vertices[iVopo];

                switch (_intersectingEdgesStrategy)
                {
                    case IntersectingConstraintEdges.NotAllowed:
                        if (FixedEdges.Contains(new Edge(iVleft, iVright)))
                        {
                            // report original input edges in the exception
                            Edge e1 = PieceToOriginals.TryGetValue(edge, out var e1Orig) && e1Orig.Count > 0
                                ? e1Orig[0]
                                : edge;
                            Edge e2 = new Edge(iVleft, iVright);
                            if (PieceToOriginals.TryGetValue(e2, out var e2Orig) && e2Orig.Count > 0)
                                e2 = e2Orig[0];
                            // don't count super-triangle vertices
                            e1 = new Edge(e1.V1 - _nTargetVerts, e1.V2 - _nTargetVerts);
                            e2 = new Edge(e2.V1 - _nTargetVerts, e2.V2 - _nTargetVerts);
                            throw new IntersectingConstraintsError(e1, e2);
                        }
                        break;

                    case IntersectingConstraintEdges.TryResolve:
                    {
                        if (!FixedEdges.Contains(new Edge(iVleft, iVright)))
                            break;
                        // split edge at the intersection of two constraint edges
                        Vector2 newV = CDTUtility.IntersectionPosition(
                            Vertices[iA], Vertices[iB], Vertices[iVleft], Vertices[iVright]);
                        int iNewVert =
                            SplitFixedEdgeAt(new Edge(iVleft, iVright), newV, iT, iTopo);
                        remaining.Add(new ConformToEdgeTask(
                            new Edge(iNewVert, iB), originals, overlaps));
                        remaining.Add(new ConformToEdgeTask(
                            new Edge(iA, iNewVert), originals, overlaps));
                        return;
                    }

                    case IntersectingConstraintEdges.DontCheck:
                        Debug.Assert(!FixedEdges.Contains(new Edge(iVleft, iVright)));
                        break;
                }

                iT = iTopo;
                t = Triangles[iT];

                PtLineLocation loc =
                    CDTUtility.LocatePointLine(vOpo, a, b, distanceTolerance);
                if (loc == PtLineLocation.Left)
                {
                    iV = iVleft;
                    iVleft = iVopo;
                }
                else if (loc == PtLineLocation.Right)
                {
                    iV = iVright;
                    iVright = iVopo;
                }
                else // encountered point on the edge
                {
                    iB = iVopo;
                }
            }

            // encountered one or more points on the edge: add remaining edge part
            if (iB != edge.V2)
            {
                remaining.Add(new ConformToEdgeTask(
                    new Edge(iB, edge.V2), originals, overlaps));
            }

            // add mid-point to triangulation
            int iMid = Vertices.Count;
            Vector2 start = Vertices[iA];
            Vector2 end = Vertices[iB];
            AddNewVertex(
                new Vector2((start.X + end.X) / (Float)2, (start.Y + end.Y) / (Float)2),
                CDTConstants.NoNeighbor);

            List<Edge> flippedFixedEdges = InsertVertex_FlipFixedEdges(iMid);

            remaining.Add(new ConformToEdgeTask(
                new Edge(iMid, iB), originals, overlaps));
            remaining.Add(new ConformToEdgeTask(
                new Edge(iA, iMid), originals, overlaps));

            // re-introduce fixed edges that were flipped
            // and make sure overlap count is preserved
            for (int idx = 0; idx < flippedFixedEdges.Count; idx++)
            {
                Edge flippedFixedEdge = flippedFixedEdges[idx];
                FixedEdges.Remove(flippedFixedEdge);

                ushort prevOverlaps = 0;
                if (OverlapCount.TryGetValue(flippedFixedEdge, out ushort oc))
                {
                    prevOverlaps = oc;
                    OverlapCount.Remove(flippedFixedEdge);
                }
                // override overlapping boundaries count when re-inserting an edge
                List<Edge> prevOriginals;
                if (PieceToOriginals.TryGetValue(flippedFixedEdge, out var po))
                {
                    prevOriginals = po;
                }
                else
                {
                    prevOriginals = new List<Edge>(1) { flippedFixedEdge };
                }
                remaining.Add(new ConformToEdgeTask(
                    flippedFixedEdge, prevOriginals, prevOverlaps));
            }
        }

        // ────────────────────────────────────────────────────────────
        //  triangulatePseudoPolygon
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Iteratively triangulate the pseudo-polygon formed on one side of the
        /// inserted constraint edge. Re-uses intersected triangle slots for the
        /// new triangles.
        /// </summary>
        private void TriangulatePseudoPolygon(
            List<int> poly,
            Dictionary<Edge, int> outerTris,
            int iT,
            int iN,
            List<int> trianglesToReuse,
            List<TriangulatePseudoPolygonTask> iterations)
        {
            Debug.Assert(poly.Count > 2);
            // note: uses iteration instead of recursion to avoid stack overflows
            iterations.Clear();
            iterations.Add(new TriangulatePseudoPolygonTask(0, poly.Count - 1, iT, iN, 0));
            while (iterations.Count > 0)
            {
                TriangulatePseudoPolygonIteration(poly, outerTris, trianglesToReuse, iterations);
            }
        }

        // ────────────────────────────────────────────────────────────
        //  triangulatePseudoPolygonIteration
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Single iteration of pseudo-polygon triangulation. Pops a task from
        /// the iterations list, finds the Delaunay point, creates the triangle,
        /// and pushes sub-tasks for the two resulting sub-polygons.
        /// </summary>
        private void TriangulatePseudoPolygonIteration(
            List<int> poly,
            Dictionary<Edge, int> outerTris,
            List<int> trianglesToReuse,
            List<TriangulatePseudoPolygonTask> iterations)
        {
            Debug.Assert(iterations.Count > 0);
            TriangulatePseudoPolygonTask task = iterations[iterations.Count - 1];
            iterations.RemoveAt(iterations.Count - 1);
            int iA = task.IA;
            int iB = task.IB;
            int iT = task.IT;
            int iParent = task.IParent;
            int iInParent = task.IInParent;
            Debug.Assert(iB - iA > 1 && iT != CDTConstants.NoNeighbor && iParent != CDTConstants.NoNeighbor);

            // find Delaunay point
            int iC = FindDelaunayPoint(poly, iA, iB);

            int va = poly[iA];
            int vb = poly[iB];
            int vc = poly[iC];

            // split pseudo-polygon in two parts and triangulate them
            // note: second part needs to be pushed on stack first to be processed first

            // second part: points after the Delaunay point
            if (iB - iC > 1)
            {
                Debug.Assert(trianglesToReuse.Count > 0);
                int iNext = trianglesToReuse[trianglesToReuse.Count - 1];
                trianglesToReuse.RemoveAt(trianglesToReuse.Count - 1);
                iterations.Add(new TriangulatePseudoPolygonTask(iC, iB, iNext, iT, 1));
            }
            else // pseudo-poly is reduced to a single outer edge
            {
                Edge outerEdge = new Edge(vb, vc);
                int outerTri = outerTris[outerEdge];
                if (outerTri != CDTConstants.NoNeighbor)
                {
                    Debug.Assert(outerTri != iT);
                    Triangle tRef = Triangles[iT];
                    tRef.N1 = outerTri;
                    Triangles[iT] = tRef;
                    ChangeNeighbor(outerTri, vc, vb, iT);
                }
                else
                {
                    outerTris[outerEdge] = iT;
                }
            }

            // first part: points before the Delaunay point
            if (iC - iA > 1)
            {
                // add next triangle and add another iteration
                Debug.Assert(trianglesToReuse.Count > 0);
                int iNext = trianglesToReuse[trianglesToReuse.Count - 1];
                trianglesToReuse.RemoveAt(trianglesToReuse.Count - 1);
                iterations.Add(new TriangulatePseudoPolygonTask(iA, iC, iNext, iT, 2));
            }
            else
            {
                // pseudo-poly is reduced to a single outer edge
                Edge outerEdge = new Edge(vc, va);
                int outerTri = outerTris[outerEdge];
                if (outerTri != CDTConstants.NoNeighbor)
                {
                    Debug.Assert(outerTri != iT);
                    Triangle tRef = Triangles[iT];
                    tRef.N2 = outerTri;
                    Triangles[iT] = tRef;
                    ChangeNeighbor(outerTri, vc, va, iT);
                }
                else
                {
                    outerTris[outerEdge] = iT;
                }
            }

            // Finalize triangle
            // note: only when triangle is finalized do we add it as a neighbor to
            // parent to maintain triangulation topology consistency
            {
                Triangle parentTri = Triangles[iParent];
                parentTri.SetNeighbor(iInParent, iT);
                Triangles[iParent] = parentTri;
            }
            {
                // Read back the triangle — N1 and N2 may have been set by the
                // "else" branches above (outer edge linking). Only overwrite
                // N0 and vertex fields here.
                Triangle tFinal = Triangles[iT];
                tFinal.N0 = iParent;
                tFinal.V0 = va;
                tFinal.V1 = vb;
                tFinal.V2 = vc;
                Triangles[iT] = tFinal;
            }
            SetAdjacentTriangle(vc, iT);
        }

        // ────────────────────────────────────────────────────────────
        //  findDelaunayPoint
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Find the point in the pseudo-polygon between indices
        /// <paramref name="iA"/> and <paramref name="iB"/> (exclusive) that
        /// satisfies the Delaunay criterion (incircle test).
        /// </summary>
        private int FindDelaunayPoint(List<int> poly, int iA, int iB)
        {
            Debug.Assert(iB - iA > 1);
            Vector2 a = Vertices[poly[iA]];
            Vector2 b = Vertices[poly[iB]];
            int outIdx = iA + 1;
            Vector2 c = Vertices[poly[outIdx]]; // cached for better performance
            for (int i = iA + 1; i < iB; i++)
            {
                Vector2 v = Vertices[poly[i]];
                if (CDTUtility.IsInCircumcircle(v, a, b, c))
                {
                    outIdx = i;
                    c = v;
                }
            }
            Debug.Assert(outIdx > iA && outIdx < iB); // point is between ends
            return outIdx;
        }

        // ────────────────────────────────────────────────────────────
        //  insert_unique helpers (ported from detail namespace)
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Add <paramref name="elem"/> to the list stored at
        /// <paramref name="dict"/>[<paramref name="key"/>] if not already present.
        /// Creates the list if the key is new.
        /// </summary>
        private static void InsertUnique(
            Dictionary<Edge, List<Edge>> dict, Edge key, Edge elem)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<Edge>(1);
                dict[key] = list;
            }
            if (!list.Contains(elem))
                list.Add(elem);
        }

        /// <summary>
        /// Add elements of <paramref name="from"/> that are not already in the
        /// list stored at <paramref name="dict"/>[<paramref name="key"/>].
        /// Creates the list if the key is new.
        /// </summary>
        private static void InsertUnique(
            Dictionary<Edge, List<Edge>> dict, Edge key, List<Edge> from)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<Edge>(from.Count);
                dict[key] = list;
            }
            for (int i = 0; i < from.Count; i++)
            {
                if (!list.Contains(from[i]))
                    list.Add(from[i]);
            }
        }
    }
}
