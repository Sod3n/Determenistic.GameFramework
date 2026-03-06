using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.Physics.Components;

[Deterministic.GameFramework.CoreV2.NetworkId("fb4fe7e4-2244-48ea-9ec5-0a972a3861d4")]
public struct PhysicsWorldState : IComponent
{
    // Holds the tick associated with the serialized state.
    // The actual data is stored in the RapierPhysicsSystem to ensure the component is blittable.
    public long Tick;
}