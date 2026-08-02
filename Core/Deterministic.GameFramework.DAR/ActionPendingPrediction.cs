using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.DAR;

/// <summary>
/// Transient component attached by <see cref="ActionScheduler.ExecuteActions"/> to the
/// target entity of a predicted action, read by the Dispatcher's per-service forEach
/// delegate to set <see cref="EntityWorld.CurrentActionPredictionId"/> before invoking
/// the action service, then removed immediately after service execution (alongside the
/// action component itself). Lifetime is sub-tick — it never survives to
/// <see cref="GameSimulation"/>'s <c>PostSimulate</c> where the delta is generated, so it
/// doesn't ride through the delta stream.
///
/// We need this indirection because <see cref="Dispatcher.ExecuteByteAction"/> only adds
/// the action component to the entity — the actual service call happens later via
/// <c>Dispatcher.Update</c>. Pushing the PredictionId directly in <c>ExecuteActions</c>
/// clears it before the service ever runs.
/// </summary>
[StableId("3e2b8d1a-4f5c-4a7b-9e3d-8c2f1a6b5d7e")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ActionPendingPrediction : IComponent
{
    public long PredictionId;
}
