using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    // Calculate the intersection between a polygon and a circle. A dodecagon is used as an approximation of the circle.
    public class DtStrictDtPolygonByCircleConstraint : IDtPolygonByCircleConstraint
    {
        private const int CIRCLE_SEGMENTS = 12;
        private static readonly Float[] UnitCircle = CreateCircle();

        public static readonly IDtPolygonByCircleConstraint Shared = new DtStrictDtPolygonByCircleConstraint();

        private DtStrictDtPolygonByCircleConstraint()
        {
        }

        public static Float[] CreateCircle()
        {
            var temp = new Float[CIRCLE_SEGMENTS * 3];
            for (int i = 0; i < CIRCLE_SEGMENTS; i++)
            {
                Float a = (Float)i * Float.Pi * 2 / (Float)CIRCLE_SEGMENTS;
                temp[3 * i] = Float.Cos(a);
                temp[3 * i + 1] = 0;
                temp[3 * i + 2] = -Float.Sin(a);
            }

            return temp;
        }

        public static void ScaleCircle(ReadOnlySpan<Float> src, Vector3 center, Float radius, Span<Float> dst)
        {
            for (int i = 0; i < CIRCLE_SEGMENTS; i++)
            {
                dst[3 * i] = src[3 * i] * radius + center.X;
                dst[3 * i + 1] = center.Y;
                dst[3 * i + 2] = src[3 * i + 2] * radius + center.Z;
            }
        }

        public bool Apply(Span<Float> verts, Vector3 center, Float radius, Span<Float> constrainedVerts, out int constrainedVertCount)
        {
            Float radiusSqr = radius * radius;
            int outsideVertex = -1;
            for (int pv = 0; pv < verts.Length; pv += 3)
            {
                if (DtVecHelper.Dist2DSqr(center, verts, pv) > radiusSqr)
                {
                    outsideVertex = pv;
                    break;
                }
            }

            if (outsideVertex == -1)
            {
                // polygon inside circle
                verts.CopyTo(constrainedVerts);
                constrainedVertCount = verts.Length;
                return true;
            }

            Span<Float> qCircle = stackalloc Float[UnitCircle.Length];
            ScaleCircle(UnitCircle, center, radius, qCircle);

            int maxIntersection = DtConvexConvexIntersections.CalculateIntersectionBufferSize(verts.Length / 3, qCircle.Length / 3);
            Span<Float> intersection = stackalloc Float[maxIntersection];
            bool result = DtConvexConvexIntersections.Intersect(verts, qCircle, intersection, out int nverts);
            if (!result && DtUtils.PointInPolygon(center, verts, verts.Length / 3))
            {
                // circle inside polygon
                qCircle.CopyTo(constrainedVerts);
                constrainedVertCount = qCircle.Length;
                return true;
            }

            intersection.Slice(0, nverts).CopyTo(constrainedVerts);
            constrainedVertCount = nverts;
            return true;
        }
    }
}
