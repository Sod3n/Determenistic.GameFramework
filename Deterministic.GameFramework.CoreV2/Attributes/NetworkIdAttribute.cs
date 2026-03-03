using System;

namespace Deterministic.GameFramework.CoreV2;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class NetworkIdAttribute : Attribute
{
    public Guid Id { get; }

    public NetworkIdAttribute(string id)
    {
        Id = Guid.Parse(id);
    }
}
