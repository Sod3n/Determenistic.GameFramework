using System.Collections.Generic;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Systems;

/// <summary>
/// Persistent state for the navigation system, stored as CustomData on EntityWorld.
/// Holds the navigation map and per-agent path data.
/// </summary>
public class NavigationState
{
    public NavigationMap Map { get; } = new();

    /// <summary>
    /// Tracks region entity IDs and their last known hash
    /// so we only rebuild when regions actually change.
    /// </summary>
    public Dictionary<int, int> RegionHashes { get; } = new();

    /// <summary>
    /// Set of region entity IDs from previous frame.
    /// </summary>
    public HashSet<int> PreviousRegionIds { get; } = new();

    /// <summary>
    /// Per-agent path data (EntityId -> AgentPathData).
    /// Stored here instead of in the ECS component to avoid
    /// putting large variable-size data in the deterministic state.
    /// </summary>
    public Dictionary<int, AgentPathData> AgentPaths { get; } = new();

    // --- Physics bake tracking ---

    /// <summary>
    /// Whether the physics-baked mesh has been built at least once.
    /// </summary>
    public bool PhysicsBaked { get; set; }

    /// <summary>
    /// Hash of the physics world state used for the last bake.
    /// Used to detect when a rebake is needed (non-chunked mode only).
    /// </summary>
    public int PhysicsBakeHash { get; set; }

    /// <summary>
    /// Whether the physics bake contributed to the current map.
    /// When true, the map contains physics-baked triangles that should
    /// be cleared before region-based rebuild.
    /// </summary>
    public bool HasPhysicsBakedMesh { get; set; }

    /// <summary>
    /// Number of triangles contributed by the physics baker.
    /// Used to skip these triangles during region adjacency rebuild,
    /// since the baker already builds grid-topology adjacency for them.
    /// </summary>
    public int PhysicsTriangleCount { get; set; }

    /// <summary>
    /// Cached static body count from last hash computation.
    /// Used as a cheap pre-check before computing the full hash.
    /// </summary>
    public int LastStaticBodyCount { get; set; }

    /// <summary>
    /// Tick at which the physics hash was last invalidated (body count changed).
    /// Used to debounce rebakes — wait several ticks after the last change.
    /// </summary>
    public long DirtyTick { get; set; } = -1;

    /// <summary>Number of ticks to wait after a physics change before rebaking.</summary>
    public const int RebakeDebounceFrames = 10;

    /// <summary>Max chunks to bake per tick during initial bake. Spreads load across frames.</summary>
    public const int MaxChunksPerTick = 15;

    /// <summary>Pending chunk keys that still need baking during progressive initial bake.</summary>
    public List<long> PendingBakeKeys { get; } = new();

    /// <summary>Whether a progressive initial bake is in progress.</summary>
    public bool IsProgressiveBaking { get; set; }

    // --- Chunked bake state ---

    /// <summary>
    /// Per-chunk bake results. Key = (chunkX &lt;&lt; 32) | (uint)chunkY.
    /// Each chunk caches its obstacle hash and baked rectangles so only
    /// changed chunks need re-rasterization.
    /// </summary>
    public Dictionary<long, ChunkBakeResult> Chunks { get; } = new();

    /// <summary>
    /// Cached world settings hash to detect when chunk layout must be fully invalidated
    /// (e.g. bounds, cellSize, agentRadius changed).
    /// </summary>
    public int ChunkWorldSettingsHash { get; set; }
}

/// <summary>
/// Runtime path data for a single navigation agent.
/// </summary>
public class AgentPathData
{
    public List<Vector2> PathPoints { get; } = new(64);
    public int CurrentPathIndex;
}

/// <summary>
/// Cached bake result for a single navigation chunk.
/// Stores the pre-computed rectangle vertices so the full NavigationMap
/// can be rebuilt cheaply from all chunk caches without re-rasterizing.
/// </summary>
public class ChunkBakeResult
{
    public int ObstacleHash;
    public readonly List<(Vector2 V0, Vector2 V1, Vector2 V2, Vector2 V3)> Rectangles = new();

    /// <summary>
    /// Start index of this chunk's triangles in the NavigationMap.
    /// Set when triangles are added to the map. -1 if not yet placed.
    /// </summary>
    public int TriangleStartIndex = -1;

    /// <summary>
    /// Number of triangles this chunk contributes to the NavigationMap.
    /// Equal to Rectangles.Count * 2.
    /// </summary>
    public int TriangleCount;
}
