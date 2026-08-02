using System;

namespace Deterministic.GameFramework.ECS;

/// <summary>
/// Marks a system to be excluded from auto-discovery.
/// Systems with this attribute must be registered manually via SystemRunner.EnableSystem().
/// Use this when multiple implementations exist for the same role (e.g. RapierPhysicsSystem vs Box2DPhysicsSystem).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ManualSystemAttribute : Attribute { }
