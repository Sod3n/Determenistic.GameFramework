using System;

namespace Deterministic.GameFramework.ECS;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class StableIdAttribute : Attribute
{
    public Guid Id { get; }

    public StableIdAttribute(string id)
    {
        Id = Guid.Parse(id);
    }
}
