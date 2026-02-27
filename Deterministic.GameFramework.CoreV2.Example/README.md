# Deterministic Game Framework V2 - Examples

Runnable examples demonstrating the Deterministic Game Framework V2.

## Quick Start

```bash
cd Server/Framework/Deterministic.GameFramework.CoreV2.Example
dotnet run
```

## What's Included

The example project demonstrates:
- **Components & Actions** - Basic ECS setup with health and damage
- **Reactions** - Event-driven logic and validation
- **Rollback Networking** - State snapshots and resimulation
- **Deterministic Math** - Fixed-point arithmetic verification
- **Hierarchy System** - Parent-child entity relationships

## Documentation

Each example corresponds to a tutorial:

- [Hello World](articles/getting-started/01-hello-world.md) - Components, Actions, and basic ECS
- [Reactions](articles/getting-started/02-reactions.md) - Event-driven logic and validation
- [Rollback Networking](articles/getting-started/03-rollback.md) - State snapshots and resimulation

## Build Docs

```bash
./compile-docs.sh
```

Or manually:

```bash
docfx docfx.json --serve
```
