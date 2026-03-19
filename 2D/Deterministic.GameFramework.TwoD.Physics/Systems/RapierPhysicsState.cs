using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics2D.Systems;

internal class RapierPhysicsState
{
    public RapierWorld? World { get; set; }
    public long LastSimulatedTick { get; set; } = -1;
    
    // EntityId -> BodyHandle
    public Dictionary<int, ulong> EntityToBody { get; } = new();
    // BodyHandle -> EntityId
    public Dictionary<ulong, int> BodyToEntity { get; } = new();
    
    // Processors
    public RapierCharacterProcessor CharacterProcessor { get; } = new();
    public RapierAreaProcessor AreaProcessor { get; } = new();
    
    // History
    public Dictionary<long, byte[]> WorldStateHistory { get; } = new();
    
    public void Dispose()
    {
        World?.Dispose();
        World = null;
        CharacterProcessor.Clear();
        EntityToBody.Clear();
        BodyToEntity.Clear();
        WorldStateHistory.Clear();
    }
}
