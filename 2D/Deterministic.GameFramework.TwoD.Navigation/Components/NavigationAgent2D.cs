using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Components;

/// <summary>
/// Navigation agent that pathfinds and steers toward a target.
/// Attach to an entity with Transform2D (and optionally CharacterBody2D) to enable navigation.
/// Mirrors Godot's NavigationAgent2D.
///
/// Path data is stored externally in NavigationState (not in ECS) since it's
/// transient runtime data that doesn't need deterministic serialization.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
[Deterministic.GameFramework.ECS.StableId("c8f3b2d4-5e67-4a90-b123-4f5a6b7c8d9e")]
public struct NavigationAgent2D : IComponent
{
    // --- Configuration ---

    /// <summary>
    /// Target position to navigate to (world space).
    /// </summary>
    public Vector2 TargetPosition;

    /// <summary>
    /// Navigation layers the agent can traverse (bitmask).
    /// </summary>
    public uint NavigationLayers;

    /// <summary>
    /// Distance threshold to consider a waypoint reached.
    /// </summary>
    public Float PathDesiredDistance;

    /// <summary>
    /// Distance threshold to consider the final target reached.
    /// </summary>
    public Float TargetDesiredDistance;

    /// <summary>
    /// Maximum speed of the agent.
    /// </summary>
    public Float MaxSpeed;

    /// <summary>
    /// Radius for avoidance.
    /// </summary>
    public Float Radius;

    /// <summary>
    /// How far ahead to look for obstacles.
    /// </summary>
    public Float AvoidanceLookahead;

    /// <summary>
    /// Whether avoidance is enabled.
    /// </summary>
    public bool AvoidanceEnabled;

    /// <summary>
    /// Collision mask for avoidance raycasts.
    /// </summary>
    public uint AvoidanceMask;

    /// <summary>
    /// Maximum number of avoidance rays.
    /// </summary>
    public int AvoidanceRayCount;

    // --- State (Output) ---

    /// <summary>
    /// Whether navigation is finished (target reached or no path).
    /// </summary>
    public bool IsNavigationFinished;

    /// <summary>
    /// Whether the target is reachable.
    /// </summary>
    public bool IsTargetReachable;

    /// <summary>
    /// Whether the target position has changed.
    /// </summary>
    public bool IsTargetPositionChanged;

    /// <summary>
    /// Computed velocity the agent should move with this frame.
    /// </summary>
    public Vector2 Velocity;

    /// <summary>
    /// Next path position the agent is steering toward.
    /// </summary>
    public Vector2 NextPathPosition;

    /// <summary>
    /// Distance remaining along the path to the target.
    /// </summary>
    public Float DistanceToTarget;

    /// <summary>
    /// Previous target position for change detection (internal).
    /// </summary>
    internal Vector2 LastTargetPosition;

    public static NavigationAgent2D Default => new()
    {
        NavigationLayers = 1,
        PathDesiredDistance = (Float)2.0f,
        TargetDesiredDistance = (Float)4.0f,
        MaxSpeed = (Float)200.0f,
        Radius = (Float)10.0f,
        AvoidanceLookahead = (Float)50.0f,
        AvoidanceEnabled = false,
        AvoidanceMask = uint.MaxValue,
        AvoidanceRayCount = 5,
        IsNavigationFinished = true,
        IsTargetReachable = false,
        IsTargetPositionChanged = false,
    };
}
