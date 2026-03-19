using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Physics2D.Components;

[Deterministic.GameFramework.ECS.StableId("fb4fe7e4-2244-48ea-9ec5-0a972a3861d4")]
public struct PhysicsWorldState : IComponent
{
    // Holds the tick associated with the serialized state.
    // The actual data is stored in the RapierPhysicsSystem to ensure the component is blittable.
    public long Tick;
}