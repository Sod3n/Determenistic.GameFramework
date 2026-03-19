using System;

namespace Deterministic.GameFramework.ECS;

[AttributeUsage(AttributeTargets.Class)]
public class UpdateOrderAttribute : Attribute
{
    public int Order { get; }
    public UpdateOrderAttribute(int order)
    {
        Order = order;
    }
}
