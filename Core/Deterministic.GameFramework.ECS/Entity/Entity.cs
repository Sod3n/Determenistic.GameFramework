using System;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.ECS;

public struct Entity : IParam, IEquatable<Entity>, IComparable<Entity>
{
    public static readonly Entity Null = new Entity(-1);
    
    public int Id;
    
    public Entity(int id)
    {
        Id = id;
    }
    
    public static implicit operator int(Entity e) => e.Id;
    public static explicit operator Entity(int id) => new Entity(id);

    public bool Equals(Entity other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
    public int CompareTo(Entity other) => Id.CompareTo(other.Id);
    
    public override string ToString() => $"Entity({Id})";

    public void Deconstruct(out int id)
    {
        id = Id;
    }

    public static bool operator ==(Entity a, Entity b) => a.Id == b.Id;
    public static bool operator !=(Entity a, Entity b) => a.Id != b.Id;
}

