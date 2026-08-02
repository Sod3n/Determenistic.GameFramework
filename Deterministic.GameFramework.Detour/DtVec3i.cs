using System;

namespace Deterministic.GameFramework.Detour
{
    public struct DtVec3i : IEquatable<DtVec3i>
    {
        public int X;
        public int Y;
        public int Z;

        public static DtVec3i Zero => new DtVec3i(0, 0, 0);
        public static DtVec3i UnitX => new DtVec3i(1, 0, 0);
        public static DtVec3i UnitY => new DtVec3i(0, 1, 0);
        public static DtVec3i UnitZ => new DtVec3i(0, 1, 1);

        // Comparison Operators
        public static bool operator ==(DtVec3i left, DtVec3i right) => left.Equals(right);
        public static bool operator !=(DtVec3i left, DtVec3i right) => !left.Equals(right);

        // Arithmetic Operators
        public static DtVec3i operator +(DtVec3i a, DtVec3i b) => new DtVec3i(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static DtVec3i operator -(DtVec3i a, DtVec3i b) => new DtVec3i(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static DtVec3i operator *(DtVec3i a, int scalar) => new DtVec3i(a.X * scalar, a.Y * scalar, a.Z * scalar);

        public DtVec3i(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int this[int index]
        {
            get
            {
                return index switch
                {
                    0 => X,
                    1 => Y,
                    2 => Z,
                    _ => throw new IndexOutOfRangeException()
                };
            }
        }

        public bool Equals(DtVec3i other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object? obj)
        {
            return obj is DtVec3i other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }
    }
}
