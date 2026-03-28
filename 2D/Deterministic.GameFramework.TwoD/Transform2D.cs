using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.TwoD;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[Deterministic.GameFramework.ECS.StableId("aafea77a-2300-4eef-9ffc-071170bb9a26")]
public struct Transform2D : IComponent
{
    // Hierarchy
    public Entity Parent;
    public bool DestroyOnUnparent;
    
    // Local Space (Relative to Parent)
    private Vector2 _position;
    private Float _rotation;
    private Vector2 _scale;
    
    public Vector2 Position
    {
        get => _position;
        set
        {
            _position = value;
            if (Parent == Entity.Null) _globalPosition = value;
        }
    }

    public Float Rotation
    {
        get => _rotation;
        set
        {
            _rotation = value;
            if (Parent == Entity.Null) _globalRotation = value;
        }
    }

    public Vector2 Scale
    {
        get => _scale;
        set
        {
            _scale = value;
            if (Parent == Entity.Null) _globalScale = value;
        }
    }
    
    // World Space
    private Vector2 _globalPosition;
    private Float _globalRotation;
    private Vector2 _globalScale;

    public Vector2 GlobalPosition
    {
        get => _globalPosition;
        set
        {
            _globalPosition = value;
            if (Parent == Entity.Null) _position = value;
        }
    }

    public Float GlobalRotation
    {
        get => _globalRotation;
        set
        {
            _globalRotation = value;
            if (Parent == Entity.Null) _rotation = value;
        }
    }

    public Vector2 GlobalScale
    {
        get => _globalScale;
        set
        {
            _globalScale = value;
            if (Parent == Entity.Null) _scale = value;
        }
    }
    
    // Change Tracking
    internal Vector2 LastGlobalPosition;
    internal Float LastGlobalRotation;
    internal Vector2 LastGlobalScale;
    
    // Default constructor for defaults
    public Transform2D(Vector2 globalPosition, Float globalRotation, Vector2 globalScale)
    {
        Parent = Entity.Null;
        DestroyOnUnparent = false;
        
        _position = globalPosition;
        _rotation = globalRotation;
        _scale = globalScale;
        
        _globalPosition = globalPosition;
        _globalRotation = globalRotation;
        _globalScale = globalScale;
        
        LastGlobalPosition = globalPosition;
        LastGlobalRotation = globalRotation;
        LastGlobalScale = globalScale;
    }
    
    public static Transform2D Default => new Transform2D(Vector2.Zero, 0, Vector2.One);
}
