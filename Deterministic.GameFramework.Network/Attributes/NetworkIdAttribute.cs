namespace Deterministic.GameFramework.Network;
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class NetworkIdAttribute : Attribute
{
    public int Id { get; }

    public NetworkIdAttribute(int id)
    {
        Id = id;
    }
}
