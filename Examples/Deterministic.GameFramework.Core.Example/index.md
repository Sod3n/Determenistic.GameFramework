# Deterministic Game Framework V2

Framework for building deterministic multiplayer games with rollback networking support.

## Quick Start

```bash
cd Server/Framework/Deterministic.GameFramework.CoreV2.Example
dotnet run
```

## Tutorials

1. [Hello World](articles/getting-started/01-hello-world.md) - Components, Actions, and basic ECS
2. [Reactions](articles/getting-started/02-reactions.md) - Event-driven logic and validation
3. [Rollback Networking](articles/getting-started/03-rollback.md) - State snapshots and resimulation

## Core Features

- **Deterministic Math** - Fixed-point `Float`, `Vector2`, `Vector3` for identical results across platforms
- **Struct-Based ECS** - Zero-allocation component storage
- **Rollback Support** - Save, restore, and resimulate game state
- **Safety Analyzers** - Compile-time checks for non-deterministic types

## API Reference

Browse the [API Reference](api/) for detailed documentation of all types.
