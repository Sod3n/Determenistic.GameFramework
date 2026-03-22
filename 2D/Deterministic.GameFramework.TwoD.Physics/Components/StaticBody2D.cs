using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

[Deterministic.GameFramework.ECS.StableId("56221147-3807-449e-8c88-e92544a47833")]
public struct StaticBody2D : IComponent
{
    public ulong BodyId = ulong.MaxValue;
    public Float PhysicsMaterialOverride;
    public bool ConstantLinearVelocity; // Godot has this for moving platforms (kinematic-like static)
    public Vector2 ConstantLinearVelocityValue;
    public Float ConstantAngularVelocityValue;

    public StaticBody2D()
    {
        PhysicsMaterialOverride = default;
        ConstantLinearVelocity = false;
        ConstantLinearVelocityValue = default;
        ConstantAngularVelocityValue = default;
    }
}