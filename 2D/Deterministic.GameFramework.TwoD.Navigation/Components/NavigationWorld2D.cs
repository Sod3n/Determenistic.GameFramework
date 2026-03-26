using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Components;

/// <summary>
/// Enables automatic navigation mesh generation from the physics world.
/// Place on a single entity to activate physics-based navigation.
///
/// The system will:
/// 1. Use the defined bounds as the walkable area
/// 2. Rasterize a grid and query Rapier for colliders in each cell
/// 3. Mark cells with StaticBody2D/RigidBody2D colliders as blocked
/// 4. Generate a nav mesh from the remaining walkable cells
/// 5. Agents automatically avoid all collision objects via raycasts
///
/// This replaces the need to manually define NavigationRegion2D polygons.
/// You can still use NavigationRegion2D alongside this for additional control.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
[Deterministic.GameFramework.ECS.StableId("ea5b4d6c-7f89-4012-d345-6a7b8c9d0e1f")]
public struct NavigationWorld2D : IComponent
{
    /// <summary>
    /// Minimum corner of the navigation bounds (world space).
    /// </summary>
    public Vector2 BoundsMin;

    /// <summary>
    /// Maximum corner of the navigation bounds (world space).
    /// </summary>
    public Vector2 BoundsMax;

    /// <summary>
    /// Size of each grid cell for rasterization.
    /// Smaller = more precise but more expensive to bake.
    /// Typical values: 4-16 for pixel-art games, 16-64 for larger worlds.
    /// </summary>
    public Float CellSize;

    /// <summary>
    /// Agent radius used to inflate obstacles.
    /// Walkable cells must be at least this far from any obstacle.
    /// Set to your largest agent radius.
    /// </summary>
    public Float AgentRadius;

    /// <summary>
    /// Collision mask for what counts as a navigation obstacle.
    /// Only colliders matching this mask will block the nav mesh.
    /// Default: all layers.
    /// </summary>
    public uint ObstacleMask;

    /// <summary>
    /// Whether to include dynamic bodies (RigidBody2D) as obstacles.
    /// If false, only StaticBody2D entities block the nav mesh.
    /// Dynamic obstacles are better handled by agent avoidance raycasts.
    /// </summary>
    public bool IncludeDynamicBodies;

    /// <summary>
    /// Whether the baked nav mesh needs to be rebuilt.
    /// Set to true to force a rebake next frame.
    /// The system also auto-detects when physics bodies change.
    /// </summary>
    public bool ForceBake;

    /// <summary>
    /// Size of each navigation chunk in world units.
    /// When > 0, the world is divided into chunks and only chunks
    /// affected by obstacle changes are rebaked each frame.
    /// Set to 0 to disable chunking (full rebake every time).
    /// Should be a multiple of CellSize. Typical values: 64-256.
    /// </summary>
    public Float ChunkSize;

    public static NavigationWorld2D Default => new()
    {
        BoundsMin = new Vector2(-500, -500),
        BoundsMax = new Vector2(500, 500),
        CellSize = (Float)8,
        AgentRadius = (Float)10,
        ObstacleMask = uint.MaxValue,
        IncludeDynamicBodies = false,
        ForceBake = true, // Bake on first frame
        ChunkSize = (Float)128,
    };
}
