using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Detour
{
    public struct DtVec2f : IEquatable<DtVec2f>
    {
        public Float X;
        public Float Y;

        public static readonly DtVec2f Zero = new DtVec2f(0, 0);

        public DtVec2f(Float x, Float y)
        {
            X = x;
            Y = y;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DtVec2f))
                return false;

            return Equals((DtVec2f)obj);
        }

        public bool Equals(DtVec2f other)
        {
            return X == other.X && Y == other.Y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(DtVec2f left, DtVec2f right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DtVec2f left, DtVec2f right)
        {
            return !left.Equals(right);
        }

        public static DtVec2f operator -(DtVec2f left, DtVec2f right)
        {
            return new DtVec2f(
                left.X - right.X,
                left.Y - right.Y
            );
        }

        public override string ToString()
        {
            return $"{X}, {Y}";
        }
    }
}
