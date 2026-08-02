using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Components;

/// <summary>
/// Constrains an entity's movement to the navigation mesh.
/// Add to any CharacterBody2D that moves via velocity integration
/// (e.g. the player) and doesn't use NavigationAgent2D pathfinding.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
[StableId("f7a23b1c-9d45-4e67-b8c2-3a1f5d7e9b04")]
public struct NavMeshConstraint : IComponent
{
    /// <summary>
    /// When true: rejects off-mesh moves entirely — hard stop.
    /// When false (default): predictive slide along walls (see <see cref="SlideFactor"/>).
    /// </summary>
    public bool DisableSlide;

    /// <summary>
    /// When true: leave entity stuck if obstacle spawns on top (game logic handles it).
    /// When false (default): push to nearest walkable point.
    /// </summary>
    public bool DisablePushOut;

    /// <summary>
    /// How much of the original tangent velocity is preserved when sliding along a wall.
    /// 1.0 = full slide (player skims the wall at full speed), 0.0 = stop dead on contact.
    /// Authored per-entity in the Godot editor.
    /// </summary>
    public Float SlideFactor;

    /// <summary>
    /// Maximum angle (in degrees, 0..90) between the desired-velocity and the wall-slide
    /// direction at which sliding is still allowed. Above this, the entity stops instead
    /// of rounding the corner. 90 = no restriction (current "rounds any corner" behaviour),
    /// 0 = never slide. Authored per-entity in the Godot editor.
    /// </summary>
    public Float MaxSlideAngleDegrees;
}
