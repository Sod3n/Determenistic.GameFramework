using System;

namespace Deterministic.GameFramework.CoreV2;

public struct Ref : IParam, IEquatable<Ref>
{
    public int EntityId;
    
    public Ref(int entityId)
    {
        EntityId = entityId;
    }
    
    public Ref(Entity entity)
    {
        EntityId = entity.Id;
    }
    
    public static implicit operator int(Ref r) => r.EntityId;
    public static implicit operator Ref(int id) => new Ref(id);
    public static implicit operator Ref(Entity e) => new Ref(e);
    
    public static bool operator ==(Ref a, Ref b) => a.EntityId == b.EntityId;
    public static bool operator !=(Ref a, Ref b) => a.EntityId != b.EntityId;
    
    public override bool Equals(object? obj) => obj is Ref other && Equals(other);
    public bool Equals(Ref other) => EntityId == other.EntityId;
    public override int GetHashCode() => EntityId.GetHashCode();
    
    public override string ToString() => $"Ref({EntityId})";
}
