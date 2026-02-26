namespace Deterministic.GameFramework.CoreV2;

public struct Int : IParam, IEquatable<Int>, IComparable<Int>
{
    public int Value;
    
    public Int(int value)
    {
        Value = value;
    }
    
    public static implicit operator int(Int i) => i.Value;
    public static implicit operator Int(int i) => new Int(i);
    
    public static Int operator +(Int a, Int b) => new Int(a.Value + b.Value);
    public static Int operator -(Int a, Int b) => new Int(a.Value - b.Value);
    public static Int operator *(Int a, Int b) => new Int(a.Value * b.Value);
    public static Int operator /(Int a, Int b) => new Int(a.Value / b.Value);
    public static Int operator %(Int a, Int b) => new Int(a.Value % b.Value);
    
    public static Int operator -(Int a) => new Int(-a.Value);
    public static Int operator ++(Int a) => new Int(a.Value + 1);
    public static Int operator --(Int a) => new Int(a.Value - 1);

    public static bool operator ==(Int a, Int b) => a.Value == b.Value;
    public static bool operator !=(Int a, Int b) => a.Value != b.Value;
    
    public static bool operator >(Int a, Int b) => a.Value > b.Value;
    public static bool operator <(Int a, Int b) => a.Value < b.Value;
    public static bool operator >=(Int a, Int b) => a.Value >= b.Value;
    public static bool operator <=(Int a, Int b) => a.Value <= b.Value;
    
    public override bool Equals(object? obj) => obj is Int other && Value == other.Value;
    public bool Equals(Int other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(Int other) => Value.CompareTo(other.Value);
    
    public override string ToString() => Value.ToString();
}
