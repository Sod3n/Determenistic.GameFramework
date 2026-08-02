using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public static class DtIntersections
    {
        public static bool IntersectSegmentTriangle(Vector3 sp, Vector3 sq, Vector3 a, Vector3 b, Vector3 c, out Float t)
        {
            t = 0;
            Float v, w;
            Vector3 ab = b - a;
            Vector3 ac = c - a;
            Vector3 qp = sp - sq;

            Vector3 norm = Vector3.Cross(ab, ac);

            Float d = Vector3.Dot(qp, norm);
            if (d <= 0.0f)
            {
                return false;
            }

            Vector3 ap = sp - a;
            t = Vector3.Dot(ap, norm);
            if (t < 0.0f)
            {
                return false;
            }

            if (t > d)
            {
                return false;
            }

            Vector3 e = Vector3.Cross(qp, ap);
            v = Vector3.Dot(ac, e);
            if (v < 0.0f || v > d)
            {
                return false;
            }

            w = -Vector3.Dot(ab, e);
            if (w < 0.0f || v + w > d)
            {
                return false;
            }

            t /= d;

            return true;
        }

        public static bool IsectSegAABB(Vector3 sp, Vector3 sq, Vector3 amin, Vector3 amax, out Float tmin, out Float tmax)
        {
            Float EPS = new Float(1e-6f);

            Vector3 d = new Vector3(sq.X - sp.X, sq.Y - sp.Y, sq.Z - sp.Z);
            tmin = 0.0f;
            tmax = Float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                if (Float.Abs(DtVecHelper.Get(d, i)) < EPS)
                {
                    if (DtVecHelper.Get(sp, i) < DtVecHelper.Get(amin, i) || DtVecHelper.Get(sp, i) > DtVecHelper.Get(amax, i))
                    {
                        return false;
                    }
                }
                else
                {
                    Float ood = (Float)1.0f / DtVecHelper.Get(d, i);
                    Float t1 = (DtVecHelper.Get(amin, i) - DtVecHelper.Get(sp, i)) * ood;
                    Float t2 = (DtVecHelper.Get(amax, i) - DtVecHelper.Get(sp, i)) * ood;

                    if (t1 > t2)
                    {
                        (t1, t2) = (t2, t1);
                    }

                    if (t1 > tmin)
                    {
                        tmin = t1;
                    }

                    if (t2 < tmax)
                    {
                        tmax = t2;
                    }

                    if (tmin > tmax)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
