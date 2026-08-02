namespace Deterministic.GameFramework.ECS;

/// <summary>
/// Implemented by SystemData objects that contain derived/cached state
/// which must be invalidated after a state restore (rollback or full sync).
///
/// Determinism-critical fields should live in ECS components (serialized).
/// Derived state (physics worlds, nav meshes, crowd sims, lookup caches)
/// is rebuilt from ECS data by the owning system on the next tick.
/// </summary>
public interface IDerivedState
{
    /// <summary>
    /// Resets derived state so the owning system rebuilds it from ECS data.
    /// Called by <see cref="EntityWorld.InvalidateDerivedState"/> after state restore.
    /// </summary>
    /// <param name="full">True for full state sync (external state, may have different
    /// obstacle geometry — clear everything including expensive-to-rebuild caches).
    /// False for rollback (same game instance, obstacles unchanged — preserve caches
    /// like navmesh Map that are still valid).</param>
    void Invalidate(bool full = false);
}
