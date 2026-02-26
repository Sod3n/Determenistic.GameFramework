namespace Deterministic.GameFramework.Network.NetworkState;

public struct Int : IParam
{
    public int Value { get; set; }
    
    public static implicit operator Int(int value) => new() { Value = value };
    public static implicit operator int(Int value) => value.Value;
    public static Int operator +(Int a, Int b) => new() { Value = a.Value + b.Value };
    public static Int operator -(Int a, Int b) => new() { Value = a.Value - b.Value };
    public static Int operator *(Int a, Int b) => new() { Value = a.Value * b.Value };
    public static Int operator /(Int a, Int b) => new() { Value = a.Value / b.Value };
}