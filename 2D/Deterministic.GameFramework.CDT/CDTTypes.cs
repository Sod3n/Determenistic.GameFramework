/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Float = Deterministic.GameFramework.Types.Float;
using Vector2 = Deterministic.GameFramework.Types.Vector2;

namespace Deterministic.GameFramework.CDT
{
    // ──────────────────────────────────────────────
    //  Constants
    // ──────────────────────────────────────────────

    /// <summary>Sentinel and constant values used throughout the CDT library.</summary>
    public static class CDTConstants
    {
        /// <summary>Constant representing no valid neighbor for a triangle.</summary>
        public const int NoNeighbor = -1;

        /// <summary>Constant representing no valid vertex for a triangle.</summary>
        public const int NoVertex = -1;

        /// <summary>Constant representing an invalid in-triangle index.</summary>
        public const int InvalidIndex = -1;

        /// <summary>Number of super-triangle vertices (always 3).</summary>
        public const int SuperTriangleVertexCount = 3;
    }

    // ──────────────────────────────────────────────
    //  Enums
    // ──────────────────────────────────────────────

    /// <summary>Location of a point relative to a triangle.</summary>
    public enum PtTriLocation
    {
        Inside,
        Outside,
        OnEdge1,
        OnEdge2,
        OnEdge3,
        OnVertex,
    }

    /// <summary>Relative location of a point to a directed line.</summary>
    public enum PtLineLocation
    {
        Left,
        Right,
        OnLine,
    }

    /// <summary>Strategy for vertex insertion order.</summary>
    public enum VertexInsertionOrder
    {
        /// <summary>Automatic insertion order optimized for performance (Kd-tree BFS bulk-load).</summary>
        Auto,
        /// <summary>Insert vertices in the same order they are provided.</summary>
        AsProvided,
    }

    /// <summary>Type of geometry used to embed the triangulation.</summary>
    public enum SuperGeometryType
    {
        /// <summary>Conventional super-triangle.</summary>
        SuperTriangle,
        /// <summary>User-specified custom geometry (e.g., grid).</summary>
        Custom,
    }

    /// <summary>Strategy for treating intersecting constraint edges.</summary>
    public enum IntersectingConstraintEdges
    {
        /// <summary>Constraint edge intersections are not allowed.</summary>
        NotAllowed,
        /// <summary>Attempt to resolve constraint edge intersections.</summary>
        TryResolve,
        /// <summary>No checks: slightly faster but user must provide valid non-intersecting input.</summary>
        DontCheck,
    }

    // ──────────────────────────────────────────────
    //  Edge
    // ──────────────────────────────────────────────

    /// <summary>
    /// Edge connecting two vertices. Canonical ordering ensures V1 &lt; V2.
    /// </summary>
    public readonly struct Edge : IEquatable<Edge>
    {
        /// <summary>First vertex index (always the smaller index).</summary>
        public readonly int V1;

        /// <summary>Second vertex index (always the larger index).</summary>
        public readonly int V2;

        public Edge(int v1, int v2)
        {
            if (v1 < v2)
            {
                V1 = v1;
                V2 = v2;
            }
            else
            {
                V1 = v2;
                V2 = v1;
            }
        }

        public bool Equals(Edge other) => V1 == other.V1 && V2 == other.V2;
        public override bool Equals(object? obj) => obj is Edge other && Equals(other);

        public override int GetHashCode()
        {
            // Same combine strategy as the C++ version (boost hash_combine)
            unchecked
            {
                int seed = 0;
                seed ^= V1.GetHashCode() + (int)0x9e3779b9 + (seed << 6) + (seed >> 2);
                seed ^= V2.GetHashCode() + (int)0x9e3779b9 + (seed << 6) + (seed >> 2);
                return seed;
            }
        }

        public static bool operator ==(Edge a, Edge b) => a.V1 == b.V1 && a.V2 == b.V2;
        public static bool operator !=(Edge a, Edge b) => !(a == b);

        public override string ToString() => $"Edge({V1}, {V2})";
    }

    // ──────────────────────────────────────────────
    //  Triangle
    // ──────────────────────────────────────────────

    /// <summary>
    /// Triangulation triangle with counter-clockwise winding.
    /// <code>
    ///       V2
    ///       /\
    ///   N2 /  \ N1
    ///     /____\
    ///   V0  N0  V1
    /// </code>
    /// </summary>
    public struct Triangle
    {
        // Vertices (indices into the vertex list)
        public int V0;
        public int V1;
        public int V2;

        // Neighbor triangles (indices into the triangle list)
        public int N0;
        public int N1;
        public int N2;

        /// <summary>Creates a triangle with no vertices and no neighbors.</summary>
        public static Triangle Empty => new Triangle
        {
            V0 = CDTConstants.NoVertex,
            V1 = CDTConstants.NoVertex,
            V2 = CDTConstants.NoVertex,
            N0 = CDTConstants.NoNeighbor,
            N1 = CDTConstants.NoNeighbor,
            N2 = CDTConstants.NoNeighbor,
        };

        public Triangle(int v0, int v1, int v2, int n0, int n1, int n2)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            N0 = n0;
            N1 = n1;
            N2 = n2;
        }

        /// <summary>Get vertex by in-triangle index (0, 1, or 2).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetVertex(int index)
        {
            switch (index)
            {
                case 0: return V0;
                case 1: return V1;
                case 2: return V2;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>Set vertex by in-triangle index (0, 1, or 2).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int index, int value)
        {
            switch (index)
            {
                case 0: V0 = value; break;
                case 1: V1 = value; break;
                case 2: V2 = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>Get neighbor by in-triangle index (0, 1, or 2).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNeighbor(int index)
        {
            switch (index)
            {
                case 0: return N0;
                case 1: return N1;
                case 2: return N2;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>Set neighbor by in-triangle index (0, 1, or 2).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNeighbor(int index, int value)
        {
            switch (index)
            {
                case 0: N0 = value; break;
                case 1: N1 = value; break;
                case 2: N2 = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Next triangle adjacent to vertex <paramref name="iV"/> (clockwise).
        /// Returns (neighborTriangle, otherVertexOnSharedEdge).
        /// </summary>
        public (int TriInd, int VertInd) Next(int iV)
        {
            if (V0 == iV) return (N0, V1);
            if (V1 == iV) return (N1, V2);
            if (V2 == iV) return (N2, V0);
            throw new ArgumentException($"Triangle does not contain vertex {iV}");
        }

        /// <summary>
        /// Previous triangle adjacent to vertex <paramref name="iV"/> (counter-clockwise).
        /// Returns (neighborTriangle, otherVertexOnSharedEdge).
        /// </summary>
        public (int TriInd, int VertInd) Prev(int iV)
        {
            if (V0 == iV) return (N2, V2);
            if (V1 == iV) return (N0, V0);
            if (V2 == iV) return (N1, V1);
            throw new ArgumentException($"Triangle does not contain vertex {iV}");
        }

        /// <summary>Check if triangle contains a given vertex index.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsVertex(int iV)
        {
            return V0 == iV || V1 == iV || V2 == iV;
        }

        /// <summary>Check if any of triangle's vertices belongs to the super-triangle (index 0, 1, or 2).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TouchesSuperTriangle()
        {
            return V0 < CDTConstants.SuperTriangleVertexCount
                || V1 < CDTConstants.SuperTriangleVertexCount
                || V2 < CDTConstants.SuperTriangleVertexCount;
        }

        public override string ToString() => $"Triangle(V:[{V0},{V1},{V2}] N:[{N0},{N1},{N2}])";
    }

    // ──────────────────────────────────────────────
    //  Box2d
    // ──────────────────────────────────────────────

    /// <summary>2D axis-aligned bounding box.</summary>
    public struct Box2d
    {
        public Vector2 Min;
        public Vector2 Max;

        /// <summary>Creates an empty box that does not contain any point.</summary>
        public static Box2d Empty => new Box2d
        {
            Min = new Vector2(Float.FromRaw(long.MaxValue), Float.FromRaw(long.MaxValue)),
            Max = new Vector2(Float.FromRaw(long.MinValue), Float.FromRaw(long.MinValue)),
        };

        /// <summary>Expand the box to contain the given point.</summary>
        public void EnvelopPoint(Vector2 p)
        {
            EnvelopPoint(p.X, p.Y);
        }

        /// <summary>Expand the box to contain the point at (x, y).</summary>
        public void EnvelopPoint(Float x, Float y)
        {
            Min = new Vector2(Float.Min(x, Min.X), Float.Min(y, Min.Y));
            Max = new Vector2(Float.Max(x, Max.X), Float.Max(y, Max.Y));
        }

        /// <summary>Expand the box to contain all points in the list.</summary>
        public void EnvelopPoints(IReadOnlyList<Vector2> vertices)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                EnvelopPoint(vertices[i]);
            }
        }
    }

    // ──────────────────────────────────────────────
    //  Exceptions
    // ──────────────────────────────────────────────

    /// <summary>Base exception for CDT errors.</summary>
    public class CDTException : Exception
    {
        public CDTException(string message) : base(message) { }
        public CDTException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when triangulation modification is attempted after it was finalized.
    /// </summary>
    public class FinalizedError : CDTException
    {
        public FinalizedError()
            : base("Triangulation was finalized with 'erase...' method. Further modification is not possible.")
        { }
    }

    /// <summary>
    /// Thrown when a duplicate vertex is detected during vertex insertion.
    /// </summary>
    public class DuplicateVertexError : CDTException
    {
        public int DuplicateV1 { get; }
        public int DuplicateV2 { get; }

        public DuplicateVertexError(int v1, int v2)
            : base($"Duplicate vertex detected: #{v1} is a duplicate of #{v2}")
        {
            DuplicateV1 = v1;
            DuplicateV2 = v2;
        }
    }

    /// <summary>
    /// Thrown when intersecting constraint edges are detected but the triangulation
    /// is not configured to attempt to resolve them.
    /// </summary>
    public class IntersectingConstraintsError : CDTException
    {
        public Edge E1 { get; }
        public Edge E2 { get; }

        public IntersectingConstraintsError(Edge e1, Edge e2)
            : base($"Intersecting constraint edges detected: ({e1.V1}, {e1.V2}) intersects ({e2.V1}, {e2.V2})")
        {
            E1 = e1;
            E2 = e2;
        }
    }

    // ──────────────────────────────────────────────
    //  Static utility / predicate functions
    // ──────────────────────────────────────────────

    /// <summary>
    /// Static helper methods ported from CDTUtils.h / CDTUtils.hpp.
    /// Contains geometric predicates and triangle index manipulation utilities.
    /// </summary>
    public static class CDTUtility
    {
        // ── Index rotation helpers ──

        /// <summary>Advance in-triangle index counter-clockwise: (i + 1) % 3.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Ccw(int i) => (i + 1) % 3;

        /// <summary>Advance in-triangle index clockwise: (i + 2) % 3.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Cw(int i) => (i + 2) % 3;

        // ── PtTriLocation helpers ──

        /// <summary>Check if location is classified as on any of three edges.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnEdge(PtTriLocation location)
        {
            return location == PtTriLocation.OnEdge1
                || location == PtTriLocation.OnEdge2
                || location == PtTriLocation.OnEdge3;
        }

        /// <summary>
        /// Get the neighbor index (0..2) from an on-edge location.
        /// Only valid when <see cref="IsOnEdge"/> returns true.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EdgeNeighborIndex(PtTriLocation location)
        {
            return (int)location - (int)PtTriLocation.OnEdge1;
        }

        // ── Opposed / vertex index helpers (raw index versions) ──

        /// <summary>
        /// Opposed neighbor index from vertex index.
        /// Vertex 0 -> neighbor 1, vertex 1 -> neighbor 2, vertex 2 -> neighbor 0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OpoNbr(int vertIndex)
        {
            switch (vertIndex)
            {
                case 0: return 1;
                case 1: return 2;
                case 2: return 0;
                default: throw new InvalidOperationException("Invalid vertex index");
            }
        }

        /// <summary>
        /// Opposed vertex index from neighbor index.
        /// Neighbor 0 -> vertex 2, neighbor 1 -> vertex 0, neighbor 2 -> vertex 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OpoVrt(int neighborIndex)
        {
            switch (neighborIndex)
            {
                case 0: return 2;
                case 1: return 0;
                case 2: return 1;
                default: throw new InvalidOperationException("Invalid neighbor index");
            }
        }

        /// <summary>
        /// Index of the triangle neighbor opposed to vertex <paramref name="iVert"/>.
        /// Looks up which of the three vertex slots matches, and returns the opposed neighbor slot.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OpposedTriangleInd(in Triangle tri, int iVert)
        {
            if (tri.V0 == iVert) return 1;
            if (tri.V1 == iVert) return 2;
            return 0;
        }

        /// <summary>
        /// Index of the neighbor that shares the edge (iVedge1, iVedge2).
        /// </summary>
        public static int EdgeNeighborInd(in Triangle tri, int iVedge1, int iVedge2)
        {
            /*
             *      V2
             *       /\
             *   N2 /  \ N1
             *     /____\
             *   V0  N0  V1
             */
            if (tri.V0 == iVedge1)
            {
                return tri.V1 == iVedge2 ? 0 : 2;
            }
            if (tri.V0 == iVedge2)
            {
                return tri.V1 == iVedge1 ? 0 : 2;
            }
            return 1;
        }

        /// <summary>
        /// Index of the vertex opposed to the neighbor triangle <paramref name="iTopo"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OpposedVertexInd(in Triangle tri, int iTopo)
        {
            if (tri.N0 == iTopo) return 2;
            if (tri.N1 == iTopo) return 0;
            return 1;
        }

        /// <summary>
        /// In-triangle index (0, 1, or 2) of vertex <paramref name="iV"/> in the triangle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int VertexInd(in Triangle tri, int iV)
        {
            if (tri.V0 == iV) return 0;
            if (tri.V1 == iV) return 1;
            return 2;
        }

        // ── Concrete triangle lookup helpers ──

        /// <summary>
        /// Given a triangle and a vertex, return the neighbor triangle opposed to that vertex.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OpposedTriangle(in Triangle tri, int iVert)
        {
            return tri.GetNeighbor(OpposedTriangleInd(in tri, iVert));
        }

        /// <summary>
        /// Given a triangle and a neighbor triangle, return the vertex opposed to that neighbor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OpposedVertex(in Triangle tri, int iTopo)
        {
            return tri.GetVertex(OpposedVertexInd(in tri, iTopo));
        }

        /// <summary>
        /// Given a triangle and an edge (by two vertex indices), find the neighbor sharing the edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int EdgeNeighbor(in Triangle tri, int iVedge1, int iVedge2)
        {
            return tri.GetNeighbor(EdgeNeighborInd(in tri, iVedge1, iVedge2));
        }

        /// <summary>Check if two vertices share at least one common triangle.</summary>
        public static bool VerticesShareEdge(List<int> aTris, List<int> bTris)
        {
            for (int i = 0; i < aTris.Count; i++)
            {
                int t = aTris[i];
                for (int j = 0; j < bTris.Count; j++)
                {
                    if (bTris[j] == t)
                        return true;
                }
            }
            return false;
        }

        // ── Geometric predicates ──

        /// <summary>
        /// Orient2D: returns the signed area of the parallelogram formed by (v1-p) and (v2-p).
        /// Positive when p is to the left of line v1->v2 (CCW), negative when to the right.
        /// Uses the simple determinant formula. Fixed64 is exact enough — no adaptive predicates needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Float Orient2D(Vector2 p, Vector2 v1, Vector2 v2)
        {
            return (v1.X - p.X) * (v2.Y - p.Y) - (v1.Y - p.Y) * (v2.X - p.X);
        }

        /// <summary>
        /// Classify the value of an orient2d predicate into Left, Right, or OnLine.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PtLineLocation ClassifyOrientation(Float orientation, Float orientationTolerance)
        {
            if (orientation < -orientationTolerance)
                return PtLineLocation.Right;
            if (orientation > orientationTolerance)
                return PtLineLocation.Left;
            return PtLineLocation.OnLine;
        }

        /// <summary>
        /// Classify the value of an orient2d predicate into Left, Right, or OnLine (zero tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PtLineLocation ClassifyOrientation(Float orientation)
        {
            return ClassifyOrientation(orientation, (Float)0);
        }

        /// <summary>
        /// Check if point lies to the left of, to the right of, or on line v1->v2.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PtLineLocation LocatePointLine(Vector2 p, Vector2 v1, Vector2 v2, Float orientationTolerance)
        {
            return ClassifyOrientation(Orient2D(p, v1, v2), orientationTolerance);
        }

        /// <summary>
        /// Check if point lies to the left of, to the right of, or on line v1->v2 (zero tolerance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PtLineLocation LocatePointLine(Vector2 p, Vector2 v1, Vector2 v2)
        {
            return LocatePointLine(p, v1, v2, (Float)0);
        }

        /// <summary>
        /// Locate point relative to triangle (v1, v2, v3).
        /// Returns Inside, Outside, OnEdge1/2/3, or OnVertex.
        /// Triangle winding must be counter-clockwise.
        /// </summary>
        public static PtTriLocation LocatePointTriangle(Vector2 p, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            PtTriLocation result = PtTriLocation.Inside;

            PtLineLocation edgeCheck = LocatePointLine(p, v1, v2);
            if (edgeCheck == PtLineLocation.Right)
                return PtTriLocation.Outside;
            if (edgeCheck == PtLineLocation.OnLine)
                result = PtTriLocation.OnEdge1;

            edgeCheck = LocatePointLine(p, v2, v3);
            if (edgeCheck == PtLineLocation.Right)
                return PtTriLocation.Outside;
            if (edgeCheck == PtLineLocation.OnLine)
                result = result == PtTriLocation.Inside ? PtTriLocation.OnEdge2 : PtTriLocation.OnVertex;

            edgeCheck = LocatePointLine(p, v3, v1);
            if (edgeCheck == PtLineLocation.Right)
                return PtTriLocation.Outside;
            if (edgeCheck == PtLineLocation.OnLine)
                result = result == PtTriLocation.Inside ? PtTriLocation.OnEdge3 : PtTriLocation.OnVertex;

            return result;
        }

        /// <summary>
        /// Test if point p lies inside the circumscribed circle of triangle (v1, v2, v3).
        /// Uses the standard 4x4 determinant formulation reduced to a 3x3 determinant.
        /// Fixed64 is exact enough that no adaptive predicates are needed.
        /// </summary>
        public static bool IsInCircumcircle(Vector2 p, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            // Standard incircle test:
            //  | v1.x - p.x   v1.y - p.y   (v1.x - p.x)^2 + (v1.y - p.y)^2 |
            //  | v2.x - p.x   v2.y - p.y   (v2.x - p.x)^2 + (v2.y - p.y)^2 | > 0
            //  | v3.x - p.x   v3.y - p.y   (v3.x - p.x)^2 + (v3.y - p.y)^2 |
            Float ax = v1.X - p.X;
            Float ay = v1.Y - p.Y;
            Float bx = v2.X - p.X;
            Float by = v2.Y - p.Y;
            Float cx = v3.X - p.X;
            Float cy = v3.Y - p.Y;

            Float det = ax * (by * (cx * cx + cy * cy) - cy * (bx * bx + by * by))
                      - bx * (ay * (cx * cx + cy * cy) - cy * (ax * ax + ay * ay))
                      + cx * (ay * (bx * bx + by * by) - by * (ax * ax + ay * ay));

            return det > (Float)0;
        }

        /// <summary>
        /// Compute the intersection point of segments ab and cd.
        /// Precondition: ab and cd intersect normally (not collinear).
        /// For better accuracy, x and y are interpolated independently on the segment
        /// with the shortest projection in that dimension.
        /// </summary>
        public static Vector2 IntersectionPosition(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Float a_cd = Orient2D(a, c, d);
            Float b_cd = Orient2D(b, c, d);
            Float t_ab = a_cd / (a_cd - b_cd);

            Float c_ab = Orient2D(c, a, b);
            Float d_ab = Orient2D(d, a, b);
            Float t_cd = c_ab / (c_ab - d_ab);

            Float one = (Float)1;

            Float absDiffABx = Float.Abs(a.X - b.X);
            Float absDiffCDx = Float.Abs(c.X - d.X);
            Float x = absDiffABx < absDiffCDx
                ? (one - t_ab) * a.X + t_ab * b.X
                : (one - t_cd) * c.X + t_cd * d.X;

            Float absDiffABy = Float.Abs(a.Y - b.Y);
            Float absDiffCDy = Float.Abs(c.Y - d.Y);
            Float y = absDiffABy < absDiffCDy
                ? (one - t_ab) * a.Y + t_ab * b.Y
                : (one - t_cd) * c.Y + t_cd * d.Y;

            return new Vector2(x, y);
        }

        /// <summary>Squared distance between two points.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Float DistanceSquared(Vector2 a, Vector2 b)
        {
            Float dx = b.X - a.X;
            Float dy = b.Y - a.Y;
            return dx * dx + dy * dy;
        }

        /// <summary>Distance between two points.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Float Distance(Vector2 a, Vector2 b)
        {
            return Float.Sqrt(DistanceSquared(a, b));
        }
    }
}
