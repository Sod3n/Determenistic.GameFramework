using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Static helper for querying the navigation mesh without coupling
/// game systems to CDTNavigationState internals.
/// </summary>
public static class NavigationQueries
{
    /// <summary>
    /// Returns true if the position is on the walkable nav mesh.
    /// Returns true (permissive) if no nav mesh is built yet.
    /// </summary>
    public static bool IsWalkable(EntityWorld state, Vector2 position)
    {
        var cdtState = state.GetCustomData<CDTNavigationState>();
        if (cdtState?.Map == null || !cdtState.Map.IsBuilt) return true;

        // Also check that physics bake has occurred
        foreach (var entity in state.Filter<NavigationWorld2D>())
        {
            if (!state.GetComponent<NavigationWorld2D>(entity).PhysicsBaked)
                return true;
            break;
        }

        return cdtState.Map.FindTriangle(position) >= 0;
    }

    /// <summary>
    /// Finds the closest walkable point on the nav mesh to the given position.
    /// Returns (triangleIndex, closestPoint). TriangleIndex is -1 if no mesh is built.
    /// </summary>
    public static (int TriangleIndex, Vector2 ClosestPoint) FindClosestWalkable(
        EntityWorld state, Vector2 position)
    {
        var cdtState = state.GetCustomData<CDTNavigationState>();
        if (cdtState?.Map == null || !cdtState.Map.IsBuilt)
            return (-1, position);
        return cdtState.Map.FindClosestTriangle(position);
    }
}
