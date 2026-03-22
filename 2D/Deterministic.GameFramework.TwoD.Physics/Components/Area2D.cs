using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

[Deterministic.GameFramework.ECS.StableId("42ac8ac3-53d6-40ca-9f7e-5daee2548ba6")]
public struct Area2D : IComponent
{
    public ulong BodyId = ulong.MaxValue;
    
    public uint CollisionLayer;
    public uint CollisionMask;
    
    public bool Monitoring;
    public bool Monitorable;
    
    // Output State (Updated by Physics System)
    public bool HasOverlappingBodies;
    
    // Bitmasks for State Changes (Indices correspond to OverlappingEntities)
    // Bit 1 = Entered this frame
    // Bit 0 = Stayed (Implied if in list and neither Entered/Exited)
    public byte EnteredMask;
    
    // Bit 1 = Exited this frame (Entity is still in list for this frame, but will be removed next frame)
    public byte ExitedMask;
    
    public List8<int> OverlappingEntities;

    public Area2D()
    {
        CollisionLayer = 0;
        CollisionMask = 0;
        Monitoring = false;
        Monitorable = false;
        HasOverlappingBodies = false;
        EnteredMask = 0;
        ExitedMask = 0;
        OverlappingEntities = default;
    }
}