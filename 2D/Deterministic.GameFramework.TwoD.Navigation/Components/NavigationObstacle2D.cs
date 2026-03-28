using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Components;

/// <summary>
/// Marks an entity as a navigation obstacle that agents should avoid.
/// Uses the entity's CollisionShape2D for the obstacle shape.
/// When AvoidanceEnabled is true on the agent, the agent will use Rapier
/// raycasts to dynamically steer around obstacles with this component.
///
/// Can also optionally carve into the navigation mesh (static obstacle).
/// Mirrors Godot's NavigationObstacle2D.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
[Deterministic.GameFramework.ECS.StableId("d9a4c3e5-6f78-4b01-c234-5a6b7c8d9e0f")]
public struct NavigationObstacle2D : IComponent
{
    /// <summary>
    /// Radius of the obstacle for avoidance purposes.
    /// If 0, uses the entity's CollisionShape2D radius.
    /// </summary>
    public Float Radius;

    /// <summary>
    /// Velocity of the obstacle (for predictive avoidance).
    /// Set automatically from RigidBody2D/CharacterBody2D if present.
    /// </summary>
    public Vector2 Velocity;

    /// <summary>
    /// Whether this obstacle should carve into the nav mesh.
    /// Carved obstacles create holes in the walkable area.
    /// More expensive but gives better paths. Use for large static obstacles.
    /// </summary>
    public bool AffectsNavigationMesh;

    /// <summary>
    /// Navigation layers this obstacle affects (bitmask).
    /// </summary>
    public uint NavigationLayers;

    public static NavigationObstacle2D Default => new()
    {
        Radius = (Float)10.0f,
        AffectsNavigationMesh = false,
        NavigationLayers = 1,
    };
}
