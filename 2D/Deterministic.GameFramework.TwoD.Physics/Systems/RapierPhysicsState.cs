using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics2D.Systems;

internal class RapierPhysicsState
{
    public RapierWorld? World { get; set; }
    public long LastSimulatedTick { get; set; } = -1;

    // Old world awaiting disposal — deferred until after Step() completes in SyncTo
    public RapierWorld? PendingDispose { get; set; }

    // EntityId -> BodyHandle
    public Dictionary<int, ulong> EntityToBody { get; } = new();
    // BodyHandle -> EntityId
    public Dictionary<ulong, int> BodyToEntity { get; } = new();
    // EntityId -> ColliderHandle (first collider per entity)
    public Dictionary<int, ulong> EntityToCollider { get; } = new();

    // Processors
    public RapierCharacterProcessor CharacterProcessor { get; } = new();
    public RapierAreaProcessor AreaProcessor { get; } = new();

    public void Dispose()
    {
        PendingDispose?.Dispose();
        PendingDispose = null;
        World?.Dispose();
        World = null;
        CharacterProcessor.Clear();
        EntityToBody.Clear();
        BodyToEntity.Clear();
        EntityToCollider.Clear();
    }
}
