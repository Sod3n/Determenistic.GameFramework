using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Components;

[Deterministic.GameFramework.CoreV2.NetworkId("aafea77a-2300-4eef-9ffc-071170bb9a26")]
public struct Transform2D : IComponent
{
    // Hierarchy
    public Entity Parent;
    public bool DestroyOnUnparent;
    
    // Local Space (Relative to Parent)
    public Vector2 Position;
    public Float Rotation;
    public Vector2 Scale;
    
    // World Space
    public Vector2 GlobalPosition;
    public Float GlobalRotation;
    public Vector2 GlobalScale;
    
    // Change Tracking
    internal Vector2 LastGlobalPosition;
    internal Float LastGlobalRotation;
    internal Vector2 LastGlobalScale;
    
    // Default constructor for defaults
    public Transform2D(Vector2 globalPosition, Float globalRotation, Vector2 globalScale)
    {
        Parent = Entity.Null;
        
        Position = globalPosition;
        Rotation = globalRotation;
        Scale = globalScale;
        
        GlobalPosition = globalPosition;
        GlobalRotation = globalRotation;
        GlobalScale = globalScale;
        
        LastGlobalPosition = globalPosition;
        LastGlobalRotation = globalRotation;
        LastGlobalScale = globalScale;
    }
    
    public static Transform2D Default => new Transform2D(Vector2.Zero, 0, Vector2.One);
}
