using  System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CollisionShapeType : IEquatable<CollisionShapeType>
{
    public int Value;

    public static readonly CollisionShapeType Circle = new CollisionShapeType(0);
    public static readonly CollisionShapeType Rectangle = new CollisionShapeType(1);
    public static readonly CollisionShapeType Capsule = new CollisionShapeType(2);
    public static readonly CollisionShapeType Segment = new CollisionShapeType(3);

    public CollisionShapeType(int value)
    {
        Value = value;
    }

    public bool Equals(CollisionShapeType other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is CollisionShapeType other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public static bool operator ==(CollisionShapeType left, CollisionShapeType right)
    {
        return left.Value == right.Value;
    }

    public static bool operator !=(CollisionShapeType left, CollisionShapeType right)
    {
        return left.Value != right.Value;
    }
    
    public override string ToString() => Value switch 
    {
        0 => "Circle",
        1 => "Rectangle",
        2 => "Capsule",
        3 => "Segment",
        _ =>Value.ToString()
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[Deterministic.GameFramework.ECS.StableId("c9075775-7634-45e0-a006-037000784226")]
public struct CollisionShape2D : IComponent, IEquatable<CollisionShape2D>
{
    public CollisionShapeType Type;
    
    // Shape Data Union (Simulated)
    public CircleShape2D Circle;
    public RectangleShape2D Rectangle;
    public CapsuleShape2D Capsule;
    
    // Transform relative to parent body
    public Vector2 Position;
    public Float Rotation;
    
    public bool Disabled;
    public bool OneWayCollision;
    
    // Helpers
    public static CollisionShape2D CreateCircle(Float radius) => new CollisionShape2D 
    { 
        Type = CollisionShapeType.Circle, 
        Circle = new CircleShape2D { Radius = radius } 
    };
    
    public static CollisionShape2D CreateRectangle(Vector2 size) => new CollisionShape2D 
    { 
        Type = CollisionShapeType.Rectangle, 
        Rectangle = new RectangleShape2D { Size = size } 
    };
    
    public static CollisionShape2D CreateCapsule(Float radius, Float height) => new CollisionShape2D 
    { 
        Type = CollisionShapeType.Capsule, 
        Capsule = new CapsuleShape2D { Radius = radius, Height = height } 
    };

    public bool Equals(CollisionShape2D other)
    {
        return Type == other.Type &&
               Circle.Equals(other.Circle) &&
               Rectangle.Equals(other.Rectangle) &&
               Capsule.Equals(other.Capsule) &&
               Position.Equals(other.Position) &&
               Rotation.Equals(other.Rotation) &&
               Disabled == other.Disabled &&
               OneWayCollision == other.OneWayCollision;
    }

    public override bool Equals(object? obj)
    {
        return obj is CollisionShape2D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Circle, Rectangle, Capsule, Position, Rotation, Disabled, OneWayCollision);
    }

    public static bool operator ==(CollisionShape2D left, CollisionShape2D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CollisionShape2D left, CollisionShape2D right)
    {
        return !left.Equals(right);
    }
}