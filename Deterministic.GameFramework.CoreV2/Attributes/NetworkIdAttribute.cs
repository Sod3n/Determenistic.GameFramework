using System;

namespace Deterministic.GameFramework.CoreV2;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class NetworkIdAttribute : Attribute
{
    public int Id { get; }

    public NetworkIdAttribute(int id)
    {
        Id = id;
    }
}
