using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.Physics.Components;

[Deterministic.GameFramework.CoreV2.NetworkId("aafea77a-2300-4eef-9ffc-071170bb9a26")]
public struct Transform2D : IComponent
{
    public Vector2 Position;
    public Float Rotation;
    public Vector2 Scale;
    
    // Default constructor for defaults
    public Transform2D(Vector2 position, Float rotation, Vector2 scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
    
    public static Transform2D Default => new Transform2D(Vector2.Zero, 0, Vector2.One);
}
