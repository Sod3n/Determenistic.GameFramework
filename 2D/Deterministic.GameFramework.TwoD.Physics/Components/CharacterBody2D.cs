using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlatformOnLeave : IEquatable<PlatformOnLeave>
{
    public int Value;

    public static readonly PlatformOnLeave AddVelocity = new PlatformOnLeave(0);
    public static readonly PlatformOnLeave DoNothing = new PlatformOnLeave(1);

    public PlatformOnLeave(int value)
    {
        Value = value;
    }

    public bool Equals(PlatformOnLeave other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is PlatformOnLeave other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public static bool operator ==(PlatformOnLeave left, PlatformOnLeave right)
    {
        return left.Value == right.Value;
    }

    public static bool operator !=(PlatformOnLeave left, PlatformOnLeave right)
    {
        return left.Value != right.Value;
    }
    
    public override string ToString() => Value switch 
    {
        0 => "AddVelocity",
        1 => "DoNothing",
        _ => Value.ToString()
    };
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[Deterministic.GameFramework.ECS.StableId("1f3b7788-2947-4d66-9d33-1275d2757278")]
public struct CharacterBody2D : IComponent
{
    public Vector2 Velocity;
    public Vector2 UpDirection;
    public Float FloorSnapLength;
    public Float WallMinSlideAngle;
    public Float FloorMaxAngle;
    public PlatformOnLeave PlatformOnLeave;
    public bool SlideOnCeiling;
    public int MaxSlides;
    public bool FloorConstantSpeed;
    
    public uint CollisionLayer;
    public uint CollisionMask;

    // State (Output from simulation)
    // public bool IsOnFloor;
    // public bool IsOnCeiling;
    // public bool IsOnWall;
    // public Vector2 FloorNormal;
    // public Vector2 WallNormal;
    public Vector2 RealVelocity;

    public static CharacterBody2D Default => new CharacterBody2D
    {
        UpDirection = new Vector2(0, 1),
        FloorSnapLength = 0.1f,
        WallMinSlideAngle = 0.261799f, 
        FloorMaxAngle = 0.785398f, 
        MaxSlides = 4,
        PlatformOnLeave = PlatformOnLeave.AddVelocity,
        SlideOnCeiling = true,
        FloorConstantSpeed = false,
        CollisionLayer = 1,
        CollisionMask = uint.MaxValue,
        Velocity = Vector2.Zero,
        RealVelocity = Vector2.Zero
    };
}