using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation2D.Components;

/// <summary>
/// Defines a walkable navigation polygon region.
/// Vertices define the outline of the walkable area in local space.
/// Multiple regions are merged by the NavigationServer to form the full nav mesh.
/// Mirrors Godot's NavigationRegion2D.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
[Deterministic.GameFramework.ECS.StableId("b7e2a1c3-4d56-4f89-a012-3e4f5a6b7c8d")]
public struct NavigationRegion2D : IComponent
{
    /// <summary>
    /// Navigation layer this region belongs to (bitmask).
    /// </summary>
    public uint NavigationLayers;

    /// <summary>
    /// Whether this region is enabled for navigation.
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// Cost multiplier for traversing this region.
    /// </summary>
    public Float TravelCost;

    /// <summary>
    /// Additional cost for entering this region from another.
    /// </summary>
    public Float EnterCost;

    /// <summary>
    /// Number of valid vertices in the polygon (max 16).
    /// </summary>
    public int VertexCount;

    /// <summary>
    /// Polygon vertices in local space (max 16).
    /// </summary>
    public FixedPolygon16 Vertices;

    public static NavigationRegion2D Default => new()
    {
        NavigationLayers = 1,
        Enabled = true,
        TravelCost = (Float)1,
        EnterCost = (Float)0,
        VertexCount = 0,
    };
}

/// <summary>
/// Fixed-size polygon storage for up to 16 vertices.
/// Uses inline fields to satisfy deterministic safety requirements.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FixedPolygon16 : IParam, IEquatable<FixedPolygon16>
{
    public Vector2 V0, V1, V2, V3, V4, V5, V6, V7;
    public Vector2 V8, V9, V10, V11, V12, V13, V14, V15;

    public const int Capacity = 16;

    public Vector2 this[int index]
    {
        get => index switch
        {
            0 => V0, 1 => V1, 2 => V2, 3 => V3,
            4 => V4, 5 => V5, 6 => V6, 7 => V7,
            8 => V8, 9 => V9, 10 => V10, 11 => V11,
            12 => V12, 13 => V13, 14 => V14, 15 => V15,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (index)
            {
                case 0: V0 = value; break;
                case 1: V1 = value; break;
                case 2: V2 = value; break;
                case 3: V3 = value; break;
                case 4: V4 = value; break;
                case 5: V5 = value; break;
                case 6: V6 = value; break;
                case 7: V7 = value; break;
                case 8: V8 = value; break;
                case 9: V9 = value; break;
                case 10: V10 = value; break;
                case 11: V11 = value; break;
                case 12: V12 = value; break;
                case 13: V13 = value; break;
                case 14: V14 = value; break;
                case 15: V15 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    public bool Equals(FixedPolygon16 other)
    {
        return V0 == other.V0 && V1 == other.V1 && V2 == other.V2 && V3 == other.V3 &&
               V4 == other.V4 && V5 == other.V5 && V6 == other.V6 && V7 == other.V7 &&
               V8 == other.V8 && V9 == other.V9 && V10 == other.V10 && V11 == other.V11 &&
               V12 == other.V12 && V13 == other.V13 && V14 == other.V14 && V15 == other.V15;
    }

    public override bool Equals(object? obj) => obj is FixedPolygon16 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(V0, V1, V2, V3);
}
