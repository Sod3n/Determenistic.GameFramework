using System;

namespace Deterministic.GameFramework.CoreV2;

public struct Guid : IParam, IEquatable<Guid>, IComparable<Guid>
{
    private System.Guid _value;

    public Guid(System.Guid value)
    {
        _value = value;
    }

    public Guid(string g)
    {
        _value = new System.Guid(g);
    }

    public Guid(byte[] b)
    {
        _value = new System.Guid(b);
    }
    
    public static readonly Guid Empty = new Guid(System.Guid.Empty);

    public static Guid NewGuid() => new Guid(System.Guid.NewGuid());
    
    public static Guid Parse(string input) => new Guid(System.Guid.Parse(input));

    public static implicit operator System.Guid(Guid g) => g._value;
    public static implicit operator Guid(System.Guid g) => new Guid(g);

    public override bool Equals(object? obj) => obj is Guid other && Equals(other);
    public bool Equals(Guid other) => _value.Equals(other._value);
    public override int GetHashCode() => _value.GetHashCode();
    public override string ToString() => _value.ToString();
    public string ToString(string? format) => _value.ToString(format);
    public int CompareTo(Guid other) => _value.CompareTo(other._value);

    public static bool operator ==(Guid a, Guid b) => a._value == b._value;
    public static bool operator !=(Guid a, Guid b) => a._value != b._value;
    
    public byte[] ToByteArray() => _value.ToByteArray();
}
