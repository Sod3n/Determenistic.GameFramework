using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public static class DtUtils
    {
        public static int NextPow2(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }

        public static int Ilog2(int v)
        {
            int r;
            int shift;
            r = (v > 0xffff ? 1 : 0) << 4;
            v >>= r;
            shift = (v > 0xff ? 1 : 0) << 3;
            v >>= shift;
            r |= shift;
            shift = (v > 0xf ? 1 : 0) << 2;
            v >>= shift;
            r |= shift;
            shift = (v > 0x3 ? 1 : 0) << 1;
            v >>= shift;
            r |= shift;
            r |= (v >> 1);
            return r;
        }

        /// Determines if two axis-aligned bounding boxes overlap.
        /// @param[in] amin Minimum bounds of box A. [(x, y, z)]
        /// @param[in] amax Maximum bounds of box A. [(x, y, z)]
        /// @param[in] bmin Minimum bounds of box B. [(x, y, z)]
        /// @param[in] bmax Maximum bounds of box B. [(x, y, z)]
        /// @return True if the two AABB's overlap.
        /// @see dtOverlapBounds
        public static bool OverlapQuantBounds(DtVec3i amin, DtVec3i amax, DtVec3i bmin, DtVec3i bmax)
        {
            bool overlap = true;
            overlap = (amin.X > bmax.X || amax.X < bmin.X) ? false : overlap;
            overlap = (amin.Y > bmax.Y || amax.Y < bmin.Y) ? false : overlap;
            overlap = (amin.Z > bmax.Z || amax.Z < bmin.Z) ? false : overlap;
            return overlap;
        }

        /// Determines if two axis-aligned bounding boxes overlap.
        /// @param[in] amin Minimum bounds of box A. [(x, y, z)]
        /// @param[in] amax Maximum bounds of box A. [(x, y, z)]
        /// @param[in] bmin Minimum bounds of box B. [(x, y, z)]
        /// @param[in] bmax Maximum bounds of box B. [(x, y, z)]
        /// @return True if the two AABB's overlap.
        /// @see dtOverlapQuantBounds
        public static bool OverlapBounds(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax)
        {
            bool overlap = true;
            overlap = (amin.X > bmax.X || amax.X < bmin.X) ? false : overlap;
            overlap = (amin.Y > bmax.Y || amax.Y < bmin.Y) ? false : overlap;
            overlap = (amin.Z > bmax.Z || amax.Z < bmin.Z) ? false : overlap;
            return overlap;
        }

        public static bool OverlapRange(Float amin, Float amax, Float bmin, Float bmax, Float eps)
        {
            return ((amin + eps) > bmax || (amax - eps) < bmin) ? false : true;
        }

        /// @par
        ///
        /// All vertices are projected onto the xz-plane, so the y-values are ignored.
        public static bool OverlapPolyPoly2D(Span<Float> polya, int npolya, Span<Float> polyb, int npolyb)
        {
            Float eps = (Float)1e-4f;
            for (int i = 0, j = npolya - 1; i < npolya; j = i++)
            {
                int va = j * 3;
                int vb = i * 3;

                Vector3 n = new Vector3(polya[vb + 2] - polya[va + 2], (Float)0, -(polya[vb + 0] - polya[va + 0]));

                DtVec2f aminmax = ProjectPoly(n, polya, npolya);
                DtVec2f bminmax = ProjectPoly(n, polyb, npolyb);
                if (!OverlapRange(aminmax.X, aminmax.Y, bminmax.X, bminmax.Y, eps))
                {
                    // Found separating axis
                    return false;
                }
            }

            for (int i = 0, j = npolyb - 1; i < npolyb; j = i++)
            {
                int va = j * 3;
                int vb = i * 3;

                Vector3 n = new Vector3(polyb[vb + 2] - polyb[va + 2], (Float)0, -(polyb[vb + 0] - polyb[va + 0]));

                DtVec2f aminmax = ProjectPoly(n, polya, npolya);
                DtVec2f bminmax = ProjectPoly(n, polyb, npolyb);
                if (!OverlapRange(aminmax.X, aminmax.Y, bminmax.X, bminmax.Y, eps))
                {
                    // Found separating axis
                    return false;
                }
            }

            return true;
        }


        /// @}
        /// @name Computational geometry helper functions.
        /// @{
        /// Derives the signed xz-plane area of the triangle ABC, or the
        /// relationship of line AB to point C.
        /// @param[in] a Vertex A. [(x, y, z)]
        /// @param[in] b Vertex B. [(x, y, z)]
        /// @param[in] c Vertex C. [(x, y, z)]
        /// @return The signed xz-plane area of the triangle.
        public static Float TriArea2D(Span<Float> verts, int a, int b, int c)
        {
            Float abx = verts[b] - verts[a];
            Float abz = verts[b + 2] - verts[a + 2];
            Float acx = verts[c] - verts[a];
            Float acz = verts[c + 2] - verts[a + 2];
            return acx * abz - abx * acz;
        }

        public static Float TriArea2D(Vector3 a, Vector3 b, Vector3 c)
        {
            Float abx = b.X - a.X;
            Float abz = b.Z - a.Z;
            Float acx = c.X - a.X;
            Float acz = c.Z - a.Z;
            return acx * abz - abx * acz;
        }

        // Returns a random point in a convex polygon.
        // Adapted from Graphics Gems article.
        public static void RandomPointInConvexPoly(Span<Float> pts, int npts, Span<Float> areas, Float s, Float t, out Vector3 @out)
        {
            // Calc triangle araes
            Float areasum = (Float)0.0f;
            for (int i = 2; i < npts; i++)
            {
                areas[i] = TriArea2D(pts, 0, (i - 1) * 3, i * 3);
                areasum += Float.Max((Float)0.001f, areas[i]);
            }

            // Find sub triangle weighted by area.
            Float thr = s * areasum;
            Float acc = (Float)0.0f;
            Float u = (Float)1.0f;
            int tri = npts - 1;
            for (int i = 2; i < npts; i++)
            {
                Float dacc = areas[i];
                if (thr >= acc && thr < (acc + dacc))
                {
                    u = (thr - acc) / dacc;
                    tri = i;
                    break;
                }

                acc += dacc;
            }

            Float v = Float.Sqrt(t);

            Float a = (Float)1 - v;
            Float b = ((Float)1 - u) * v;
            Float c = u * v;
            int pa = 0;
            int pb = (tri - 1) * 3;
            int pc = tri * 3;

            @out = new Vector3()
            {
                X = a * pts[pa] + b * pts[pb] + c * pts[pc],
                Y = a * pts[pa + 1] + b * pts[pb + 1] + c * pts[pc + 1],
                Z = a * pts[pa + 2] + b * pts[pb + 2] + c * pts[pc + 2]
            };
        }

        public static bool ClosestHeightPointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Float h)
        {
            Float EPS = (Float)1e-6f;

            h = (Float)0;
            Vector3 v0 = c - a;
            Vector3 v1 = b - a;
            Vector3 v2 = p - a;

            // Compute scaled barycentric coordinates
            Float denom = v0.X * v1.Z - v0.Z * v1.X;
            if (Float.Abs(denom) < EPS)
            {
                return false;
            }

            Float u = v1.Z * v2.X - v1.X * v2.Z;
            Float v = v0.X * v2.Z - v0.Z * v2.X;

            if (denom < (Float)0)
            {
                denom = -denom;
                u = -u;
                v = -v;
            }

            // If point lies inside the triangle, return interpolated ycoord.
            if (u >= (Float)0.0f && v >= (Float)0.0f && (u + v) <= denom)
            {
                h = a.Y + (v0.Y * u + v1.Y * v) / denom;
                return true;
            }

            return false;
        }

        public static DtVec2f ProjectPoly(Vector3 axis, Span<Float> poly, int npoly)
        {
            Float rmin, rmax;
            rmin = rmax = axis.Dot2D(poly.ToVec3());
            for (int i = 1; i < npoly; ++i)
            {
                Float d = axis.Dot2D(poly.ToVec3(i * 3));
                rmin = Float.Min(rmin, d);
                rmax = Float.Max(rmax, d);
            }

            return new DtVec2f
            {
                X = rmin,
                Y = rmax,
            };
        }

        /// @par
        ///
        /// All points are projected onto the xz-plane, so the y-values are ignored.
        public static bool PointInPolygon(Vector3 pt, Span<Float> verts, int nverts)
        {
            // TODO: Replace pnpoly with triArea2D tests?
            int i, j;
            bool c = false;
            for (i = 0, j = nverts - 1; i < nverts; j = i++)
            {
                int vi = i * 3;
                int vj = j * 3;
                if (((verts[vi + 2] > pt.Z) != (verts[vj + 2] > pt.Z)) && (pt.X < (verts[vj + 0] - verts[vi + 0])
                        * (pt.Z - verts[vi + 2]) / (verts[vj + 2] - verts[vi + 2]) + verts[vi + 0]))
                {
                    c = !c;
                }
            }

            return c;
        }

        public static bool DistancePtPolyEdgesSqr(Vector3 pt, Span<Float> verts, int nverts, Span<Float> ed, Span<Float> et)
        {
            // TODO: Replace pnpoly with triArea2D tests?
            int i, j;
            bool c = false;
            for (i = 0, j = nverts - 1; i < nverts; j = i++)
            {
                int vi = i * 3;
                int vj = j * 3;
                if (((verts[vi + 2] > pt.Z) != (verts[vj + 2] > pt.Z)) &&
                    (pt.X < (verts[vj + 0] - verts[vi + 0]) * (pt.Z - verts[vi + 2]) / (verts[vj + 2] - verts[vi + 2]) + verts[vi + 0]))
                {
                    c = !c;
                }

                ed[j] = DistancePtSegSqr2D(pt, verts, vj, vi, out et[j]);
            }

            return c;
        }

        public static Float DistancePtSegSqr2D(Vector3 pt, Span<Float> verts, int p, int q, out Float t)
        {
            var vp = verts.ToVec3(p);
            var vq = verts.ToVec3(q);
            return DistancePtSegSqr2D(pt, vp, vq, out t);
        }

        public static Float DistancePtSegSqr2D(Vector3 pt, Vector3 p, Vector3 q, out Float t)
        {
            Float pqx = q.X - p.X;
            Float pqz = q.Z - p.Z;
            Float dx = pt.X - p.X;
            Float dz = pt.Z - p.Z;
            Float d = pqx * pqx + pqz * pqz;
            t = pqx * dx + pqz * dz;
            if (d > (Float)0)
            {
                t /= d;
            }

            if (t < (Float)0)
            {
                t = (Float)0;
            }
            else if (t > (Float)1)
            {
                t = (Float)1;
            }

            dx = p.X + t * pqx - pt.X;
            dz = p.Z + t * pqz - pt.Z;
            return dx * dx + dz * dz;
        }

        public static bool IntersectSegmentPoly2D(Vector3 p0, Vector3 p1,
            Span<Vector3> verts, int nverts,
            out Float tmin, out Float tmax,
            out int segMin, out int segMax)
        {
            Float EPS = (Float)0.000001f;

            tmin = (Float)0;
            tmax = (Float)1;
            segMin = -1;
            segMax = -1;

            var dir = p1 - p0;

            var p0v = p0;
            for (int i = 0, j = nverts - 1; i < nverts; j = i++)
            {
                Vector3 vpj = verts[j];
                Vector3 vpi = verts[i];
                var edge = vpi - vpj;
                var diff = p0v - vpj;
                Float n = DtVecHelper.Perp2D(edge, diff);
                Float d = DtVecHelper.Perp2D(dir, edge);
                if (Float.Abs(d) < EPS)
                {
                    // S is nearly parallel to this edge
                    if (n < (Float)0)
                    {
                        return false;
                    }
                    else
                    {
                        continue;
                    }
                }

                Float t = n / d;
                if (d < (Float)0)
                {
                    // segment S is entering across this edge
                    if (t > tmin)
                    {
                        tmin = t;
                        segMin = j;
                        // S enters after leaving polygon
                        if (tmin > tmax)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    // segment S is leaving across this edge
                    if (t < tmax)
                    {
                        tmax = t;
                        segMax = j;
                        // S leaves before entering polygon
                        if (tmax < tmin)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public static int OppositeTile(int side)
        {
            return (side + 4) & 0x7;
        }


        public static bool IntersectSegSeg2D(Vector3 ap, Vector3 aq, Vector3 bp, Vector3 bq, out Float s, out Float t)
        {
            s = (Float)0;
            t = (Float)0;

            Vector3 u = aq - ap;
            Vector3 v = bq - bp;
            Vector3 w = ap - bp;
            Float d = DtVecHelper.PerpXZ(u, v);
            if (Float.Abs(d) < (Float)1e-6f)
            {
                return false;
            }

            s = DtVecHelper.PerpXZ(v, w) / d;
            t = DtVecHelper.PerpXZ(u, w) / d;

            return true;
        }
    }
}
