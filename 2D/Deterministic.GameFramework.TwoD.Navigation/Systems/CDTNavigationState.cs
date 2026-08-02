using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Persistent state for the CDT-based navigation system, stored as CustomData on EntityWorld.
/// Holds the CDT navigation map (derived/cached state that can be rebuilt).
///
/// Per-agent path data is stored in the NavigationAgent2D ECS component
/// (FixedPathBuffer16) so it survives rollback deterministically.
///
/// Determinism-critical flags (PhysicsBaked, LastObstacleHash, etc.) live in the
/// NavigationWorld2D ECS component so they survive rollback and state sync.
/// </summary>
public class CDTNavigationState : IDerivedState
{
    /// <summary>
    /// The CDT-based navigation map used for pathfinding.
    /// Built from constrained Delaunay triangulation of obstacle polygons.
    /// </summary>
    public CDTNavigationMap Map { get; } = new();

    /// <summary>Number of ticks to wait after a physics change before rebaking.
    /// Kept short (2 frames) to minimize stale-path following when obstacles spawn on agents.</summary>
    public const int RebakeDebounceFrames = 2;

    /// <summary>
    /// Obstacle hash the Map was built from. Compared against the current
    /// obstacle hash after rollback to detect whether the Map is stale.
    /// Lives in transient state (not restored by rollback), so it always
    /// reflects what the Map was actually built with.
    /// </summary>
    public int MapObstacleHash { get; set; }

    /// <summary>
    /// Set after rollback (non-full invalidation) to trigger a staleness
    /// check on the next BakeIfNeeded call.
    /// </summary>
    public bool NeedsStaleCheck { get; set; }

    /// <summary>
    /// Invalidates derived state after rollback or state sync.
    ///
    /// On rollback (full=false): keeps the Map but marks it for a staleness
    /// check. BakeIfNeeded will compare the Map's obstacle hash against the
    /// restored obstacle state and only rebuild if they differ.
    ///
    /// On full state sync (full=true): clears Map entirely since obstacle
    /// geometry may be completely different.
    /// </summary>
    public void Invalidate(bool full = false)
    {
        if (full)
            Map.Clear();
        else
            NeedsStaleCheck = true;
    }
}
