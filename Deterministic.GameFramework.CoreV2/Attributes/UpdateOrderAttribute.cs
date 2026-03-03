using System;

namespace Deterministic.GameFramework.CoreV2;

[AttributeUsage(AttributeTargets.Class)]
public class UpdateOrderAttribute : Attribute
{
    public int Order { get; }
    public UpdateOrderAttribute(int order)
    {
        Order = order;
    }
}
