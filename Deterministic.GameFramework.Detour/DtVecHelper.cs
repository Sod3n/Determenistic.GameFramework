using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public static class DtVecHelper
    {
        public static readonly Float EPSILON = new Float(0.000001f);
        public static readonly Float EQUAL_THRESHOLD = (1.0f / 16384.0f) * (1.0f / 16384.0f);

        public static Vector3 ToVec3(this Float[] values)
        {
            return new Vector3(values[0], values[1], values[2]);
        }

        public static Vector3 ToVec3(this Float[] values, int n)
        {
            return new Vector3(values[n + 0], values[n + 1], values[n + 2]);
        }

        public static Vector3 ToVec3(this Span<Float> values)
        {
            return new Vector3(values[0], values[1], values[2]);
        }

        public static Vector3 ToVec3(this Span<Float> values, int n)
        {
            return new Vector3(values[n + 0], values[n + 1], values[n + 2]);
        }

        public static Vector3 ToVec3(this ReadOnlySpan<Float> values, int n)
        {
            return new Vector3(values[n + 0], values[n + 1], values[n + 2]);
        }

        public static void CopyTo(this Vector3 v, Float[] array, int n)
        {
            array[n + 0] = v.X;
            array[n + 1] = v.Y;
            array[n + 2] = v.Z;
        }

        public static Float Get(this DtVec2f v, int i)
        {
            switch (i)
            {
                case 0: return v.X;
                case 1: return v.Y;
                default: throw new IndexOutOfRangeException("vector2f index out of range");
            }
        }

        public static Float Get(this Vector3 v, int i)
        {
            switch (i)
            {
                case 0: return v.X;
                case 1: return v.Y;
                case 2: return v.Z;
                default: throw new IndexOutOfRangeException("vector3f index out of range");
            }
        }

        /// Performs a 'sloppy' colocation check of the specified points.
        public static bool Equal(Vector3 p0, Vector3 p1)
        {
            Float d = Vector3.DistanceSquared(p0, p1);
            return d < EQUAL_THRESHOLD;
        }

        public static Float Dot2(Vector3 a, Vector3 b)
        {
            return a.X * b.X + a.Z * b.Z;
        }

        public static Float DistSq2(Float[] verts, int p, int q)
        {
            Float dx = verts[q + 0] - verts[p + 0];
            Float dy = verts[q + 2] - verts[p + 2];
            return dx * dx + dy * dy;
        }

        public static Float Dist2(Float[] verts, int p, int q)
        {
            return Float.Sqrt(DistSq2(verts, p, q));
        }

        public static Float DistSq2(Vector3 p, Vector3 q)
        {
            Float dx = q.X - p.X;
            Float dy = q.Z - p.Z;
            return dx * dx + dy * dy;
        }

        public static Float Dist2(Vector3 p, Vector3 q)
        {
            return Float.Sqrt(DistSq2(p, q));
        }

        public static Float Cross2(Float[] verts, int p1, int p2, int p3)
        {
            Float u1 = verts[p2 + 0] - verts[p1 + 0];
            Float v1 = verts[p2 + 2] - verts[p1 + 2];
            Float u2 = verts[p3 + 0] - verts[p1 + 0];
            Float v2 = verts[p3 + 2] - verts[p1 + 2];
            return u1 * v2 - v1 * u2;
        }

        public static Float Cross2(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Float u1 = p2.X - p1.X;
            Float v1 = p2.Z - p1.Z;
            Float u2 = p3.X - p1.X;
            Float v2 = p3.Z - p1.Z;
            return u1 * v2 - v1 * u2;
        }

        /// Derives the dot product of two vectors on the xz-plane. (@p u . @p v)
        public static Float Dot2D(this Vector3 @this, Vector3 v)
        {
            return @this.X * v.X + @this.Z * v.Z;
        }

        public static void Cross(Float[] dest, Float[] v1, Float[] v2)
        {
            dest[0] = v1[1] * v2[2] - v1[2] * v2[1];
            dest[1] = v1[2] * v2[0] - v1[0] * v2[2];
            dest[2] = v1[0] * v2[1] - v1[1] * v2[0];
        }

        public static void Copy(Float[] @out, int n, Float[] @in, int m)
        {
            @out[n + 0] = @in[m + 0];
            @out[n + 1] = @in[m + 1];
            @out[n + 2] = @in[m + 2];
        }

        public static void Copy(Span<Float> @out, int n, ReadOnlySpan<Float> @in, int m)
        {
            @out[n + 0] = @in[m + 0];
            @out[n + 1] = @in[m + 1];
            @out[n + 2] = @in[m + 2];
        }

        /// Returns the squared distance between two points.
        public static Float DistanceSquared(Vector3 v1, Float[] v2, int i)
        {
            Float dx = v2[i] - v1.X;
            Float dy = v2[i + 1] - v1.Y;
            Float dz = v2[i + 2] - v1.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// Normalizes the vector if the length is greater than zero.
        /// If the magnitude is zero, the vector is unchanged.
        public static Vector3 SafeNormalize(Vector3 v)
        {
            Float sqMag = (v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z);
            if (sqMag > EPSILON)
            {
                Float inverseMag = 1.0f / Float.Sqrt(sqMag);
                return new Vector3(
                    v.X * inverseMag,
                    v.Y * inverseMag,
                    v.Z * inverseMag
                );
            }

            return v;
        }

        /// Derives the distance between the specified points on the xz-plane.
        public static Float Dist2D(Vector3 v1, Vector3 v2)
        {
            Float dx = v2.X - v1.X;
            Float dz = v2.Z - v1.Z;
            return Float.Sqrt(dx * dx + dz * dz);
        }

        /// Derives the square of the distance between the specified points on the xz-plane.
        public static Float Dist2DSqr(Vector3 v1, Vector3 v2)
        {
            Float dx = v2.X - v1.X;
            Float dz = v2.Z - v1.Z;
            return dx * dx + dz * dz;
        }

        public static Float Dist2DSqr(Vector3 p, ReadOnlySpan<Float> verts, int i)
        {
            Float dx = verts[i] - p.X;
            Float dz = verts[i + 2] - p.Z;
            return dx * dx + dz * dz;
        }

        /// Derives the xz-plane 2D perp product of the two vectors. (uz*vx - ux*vz)
        public static Float Perp2D(Vector3 u, Vector3 v)
        {
            return u.Z * v.X - u.X * v.Z;
        }

        /// Checks that the specified vector's components are all finite.
        /// Fixed-point has no NaN/Inf, so always returns true.
        public static bool IsFinite(this Vector3 v)
        {
            return true;
        }

        /// Checks that the specified vector's 2D components are finite.
        /// Fixed-point has no NaN/Inf, so always returns true.
        public static bool IsFinite2D(this Vector3 v)
        {
            return true;
        }

        public static Float PerpXZ(Vector3 a, Vector3 b)
        {
            return (a.X * b.Z) - (a.Z * b.X);
        }

        /// Performs a linear interpolation between two vectors. (@p v1 toward @p v2)
        public static Vector3 Lerp(ReadOnlySpan<Float> verts, int v1, int v2, Float t)
        {
            return new Vector3(
                verts[v1 + 0] + (verts[v2 + 0] - verts[v1 + 0]) * t,
                verts[v1 + 1] + (verts[v2 + 1] - verts[v1 + 1]) * t,
                verts[v1 + 2] + (verts[v2 + 2] - verts[v1 + 2]) * t
            );
        }

        /// Performs a scaled vector addition. (@p v1 + (@p v2 * @p s))
        public static Vector3 Mad(Vector3 v1, Vector3 v2, Float s)
        {
            return new Vector3(
                v1.X + (v2.X * s),
                v1.Y + (v2.Y * s),
                v1.Z + (v2.Z * s)
            );
        }
    }
}
