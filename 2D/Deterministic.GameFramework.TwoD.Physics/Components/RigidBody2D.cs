using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

[Deterministic.GameFramework.ECS.StableId("8014592a-3375-4c07-ae29-598114227003")]
public struct RigidBody2D : IComponent
{
    public ulong BodyId; // Internal Rapier Handle
    
    public Float Mass;
    public Float GravityScale;
    public Float Inertia;
    
    public Vector2 LinearVelocity;
    public Float AngularVelocity;
    
    public Float LinearDamping;
    public Float AngularDamping;
    
    public bool Freeze; 
    public bool FreezeMode; // True for Kinematic/Static-like freeze, False for sleeping? Godot has enum.
    // Simpler: Freeze makes it static-like. 
    
    public bool LockRotation;
    public bool Sleeping;
    
    // Continuous Collision Detection
    public bool CcdEnabled;
    
    public uint CollisionLayer;
    public uint CollisionMask;

    public static RigidBody2D Default => new RigidBody2D 
    { 
        Mass = 1, 
        GravityScale = 1, 
        LinearDamping = 0, 
        AngularDamping = 0,
        CcdEnabled = false,
        CollisionLayer = 1,
        CollisionMask = uint.MaxValue
    };
}
