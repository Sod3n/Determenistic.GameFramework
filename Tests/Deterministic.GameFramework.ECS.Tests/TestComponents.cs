using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.ECS.Tests;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[StableId("00000000-0000-0000-0000-000000000001")]
public struct TestComponent1 : IComponent
{
    public int Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[StableId("00000000-0000-0000-0000-000000000002")]
public struct TestComponent2 : IComponent
{
    public float Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[StableId("00000000-0000-0000-0000-000000000003")]
public struct TestComponent3 : IComponent
{
    public int Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[StableId("00000000-0000-0000-0000-000000000004")]
public struct TestComponentWithFields : IComponent
{
    public int X;
    public int Y;
}

[StableId("00000000-0000-0000-0000-000000000005")]
public struct TestComponentEmpty : IComponent
{
}

[StableId("00000000-0000-0000-0000-000000000006")]
public struct TestComponentHigh : IComponent
{
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UnregisteredComponent : IComponent
{
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ComponentNoAttribute : IComponent { }

[StableId("00000000-0000-0000-0000-000000000099")]
public class ComponentClass : IComponent { }

public class TestSystem : ISystem
{
    public bool Updated { get; private set; }
    public void Update(EntityWorld state)
    {
        Updated = true;
    }
    public void Reset() => Updated = false;
}

[UpdateOrder(10)]
public class OrderedSystem1 : ISystem
{
    public static List<string> ExecutionLog = new();
    public void Update(EntityWorld state) => ExecutionLog.Add("System1");
}

[UpdateOrder(20)]
public class OrderedSystem2 : ISystem
{
    public void Update(EntityWorld state) => OrderedSystem1.ExecutionLog.Add("System2");
}

[UpdateOrder(-5)]
public class OrderedSystemNegative : ISystem
{
    public void Update(EntityWorld state) => OrderedSystem1.ExecutionLog.Add("SystemNeg");
}

public class ExceptionSystem : ISystem
{
    public void Update(EntityWorld state) => throw new Exception("Test Exception");
}

public class ToStringNullComponent 
{
    public override string ToString() => null!;
}
