using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Components;

/// <summary>
/// Navigation agent that pathfinds and steers toward a target.
/// Attach to an entity with Transform2D (and optionally CharacterBody2D) to enable navigation.
/// Mirrors Godot's NavigationAgent2D.
///
/// Path waypoints and progress are stored in the component (not transient system state)
/// so they survive rollback and state sync deterministically.
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

    /// <summary>
    /// Current waypoint index along the computed path.
    /// Stored in ECS so it survives rollback.
    /// </summary>
    public byte PathIndex;

    /// <summary>
    /// Number of valid waypoints in <see cref="PathPoints"/>.
    /// </summary>
    public byte PathPointCount;

    /// <summary>
    /// Fixed-size path buffer storing computed waypoints.
    /// Survives rollback, eliminating the need for transient dictionary storage.
    /// </summary>
    public FixedPathBuffer16 PathPoints;

    /// <summary>
    /// Index of this agent in the DtCrowd. -1 if not yet added.
    /// Stored in ECS so crowd agents can be reconnected after rollback
    /// without recreating the entire crowd.
    /// </summary>
    public int CrowdAgentIndex;

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
        CrowdAgentIndex = -1,
    };
}

/// <summary>
/// Fixed-size path buffer for up to 16 waypoints.
/// Uses inline fields for deterministic struct layout (same pattern as FixedPolygon16).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FixedPathBuffer16 : IEquatable<FixedPathBuffer16>
{
    public Vector2 P0, P1, P2, P3, P4, P5, P6, P7;
    public Vector2 P8, P9, P10, P11, P12, P13, P14, P15;

    public const int Capacity = 16;

    public Vector2 this[int index]
    {
        get => index switch
        {
            0 => P0, 1 => P1, 2 => P2, 3 => P3,
            4 => P4, 5 => P5, 6 => P6, 7 => P7,
            8 => P8, 9 => P9, 10 => P10, 11 => P11,
            12 => P12, 13 => P13, 14 => P14, 15 => P15,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (index)
            {
                case 0: P0 = value; break;
                case 1: P1 = value; break;
                case 2: P2 = value; break;
                case 3: P3 = value; break;
                case 4: P4 = value; break;
                case 5: P5 = value; break;
                case 6: P6 = value; break;
                case 7: P7 = value; break;
                case 8: P8 = value; break;
                case 9: P9 = value; break;
                case 10: P10 = value; break;
                case 11: P11 = value; break;
                case 12: P12 = value; break;
                case 13: P13 = value; break;
                case 14: P14 = value; break;
                case 15: P15 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    public bool Equals(FixedPathBuffer16 other)
    {
        return P0 == other.P0 && P1 == other.P1 && P2 == other.P2 && P3 == other.P3 &&
               P4 == other.P4 && P5 == other.P5 && P6 == other.P6 && P7 == other.P7 &&
               P8 == other.P8 && P9 == other.P9 && P10 == other.P10 && P11 == other.P11 &&
               P12 == other.P12 && P13 == other.P13 && P14 == other.P14 && P15 == other.P15;
    }

    public override bool Equals(object? obj) => obj is FixedPathBuffer16 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(P0, P1, P2, P3);
}
