using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.Physics.Components;

[Deterministic.GameFramework.CoreV2.NetworkId("56221147-3807-449e-8c88-e92544a47833")]
public struct StaticBody2D : IComponent
{
    public ulong BodyId;
    public Float PhysicsMaterialOverride;
    public bool ConstantLinearVelocity; // Godot has this for moving platforms (kinematic-like static)
    public Vector2 ConstantLinearVelocityValue;
    public Float ConstantAngularVelocityValue;
}