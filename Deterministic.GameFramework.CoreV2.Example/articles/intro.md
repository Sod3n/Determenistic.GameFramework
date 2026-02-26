# Introduction

**Deterministic.GameFramework.CoreV2** is a specialized framework for building multiplayer games that require **absolute determinism**. This capability allows you to implement advanced networking features like **Client-Side Prediction**, **Rollback Networking**, and **Replay Systems** with ease.

## Why Determinism Matters

In a multiplayer game, "determinism" means that if you give the game the same inputs in the same order, it will produce the **exact same state** on every machine, every time. 

Without determinism, slight differences in floating-point math between CPUs (e.g., Intel vs. AMD, or even different compiler optimizations) can cause the game state to diverge. Player A might see a hit, while Player B sees a miss. This "desync" breaks the game.

## Key Features

### 1. Safe, Deterministic Math
The framework replaces standard C# `float` and `double` types with a custom `Float` struct (Fixed-Point Math). This ensures that calculations like `10 / 3` or `Sqrt(2)` yield bit-identical results on all platforms.

### 2. High-Performance ECS Architecture
Game state is organized using an **Entity Component System (ECS)**. 
- **Entities** are just lightweight IDs.
- **Components** are data structs (e.g., `Position`, `Health`) attached to entities.
- **GlobalState** manages all data in flat arrays, ensuring CPU cache efficiency and zero garbage collection overhead during gameplay.

### 3. Automatic Networking
You define your game state and actions, and the framework handles the rest.
- **Snapshots**: The entire game state can be serialized to a byte array instantly.
- **Rollback**: The game can "rewind" to a previous tick, apply new inputs, and "fast-forward" back to the present. This is transparent to your game logic.

### 4. Safety Analyzers
The framework includes a Roslyn Analyzer that runs while you code. It will **flag an error** if you accidentally use a non-deterministic type (like `float` or a reference type) in your networked components, preventing desync bugs before they happen.

## Platform Support

### Unity Game Engine
The framework targets **.NET Standard 2.1**, making it fully compatible with **Unity 2021.3** and newer.
- **IL2CPP Friendly**: The core uses struct-based generics and avoids dynamic code generation that typically breaks IL2CPP.
- **Performance**: High-performance math types fall back to `BigInteger` on platforms lacking `Int128` (like older .NET Runtimes), ensuring compatibility.

## Next Steps

- **[Core Concepts](concepts/determinism.md)**: Learn about the basic building blocks like `Float` and `Entity`.
- **[Getting Started](getting-started/quickstart.md)**: Build your first deterministic game loop.
