using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.ECS;

/// <summary>
/// Auto-attached to any entity created while an action with a non-zero
/// <c>PredictionId</c> is executing (see <see cref="EntityWorld.CurrentActionPredictionId"/>).
/// <para>
/// Client and server both tag — client uses it to locate its local predicted counterpart
/// when the server's authoritative delta for the same action arrives. The delta applier
/// reads <see cref="PredictionId"/> from this component on the newly-created server entity,
/// looks up the client's predicted entity by the same id, destroys the predicted ghost,
/// and lets the server entity take over.
/// </para>
/// <para>
/// Blittable, Pack=1 — safe to ride through <c>AlignedComponentStore</c> and the delta
/// op stream like any other component.
/// </para>
/// </summary>
[StableId("9c4a51b2-3f7e-4d18-8f2a-6b1c7e5d90fe")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PredictedByAction : IComponent
{
    public long PredictionId;
}
