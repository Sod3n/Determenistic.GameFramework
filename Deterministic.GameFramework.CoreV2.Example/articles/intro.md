# Introduction

Deterministic.GameFramework.CoreV2 is a framework for building multiplayer games with **deterministic simulation**. Same inputs always produce the same outputs, enabling rollback networking and replay systems.

## Purpose

The framework provides:
- **Deterministic game state** - Reproducible simulations across all clients
- **ECS architecture** - Struct-based entities and components
- **Action system** - Type-safe state mutations with validation
- **Reaction system** - Event-driven responses to actions
- **Rollback support** - Save, restore, and resimulate game state

## Why Determinism?

In multiplayer games, determinism means identical inputs produce identical results on every machine. Without it, floating-point differences between CPUs cause desyncs where players see different game states.

The framework uses fixed-point math (`Float` struct) instead of standard `float` types, ensuring calculations like `10 / 3` or `Sqrt(2)` produce bit-identical results on all platforms.

## Key Components

- **GlobalState** - Manages all entities and components in flat arrays
- **Entity** - Lightweight ID wrapper for game objects
- **IComponent** - Data structs attached to entities (must have `[NetworkId]`)
- **IAction** - Operations that modify state
- **ActionService** - Stateless handlers that execute actions
- **Reaction** - Hooks that respond to actions (Before, After)
- **GameLoop** - Manages tick rate, scheduling, and rollback

## Safety Features

The framework includes Roslyn Analyzers that enforce determinism at compile-time:
- **DGF200** - Flags non-deterministic types in components (like `float` or reference types)
- **DGF100-102** - Enforces `[NetworkId]` attributes on networked types

## Getting Started

New to the framework? Start with the [Hello World tutorial](getting-started/01-hello-world.md) to build your first deterministic game in minutes.
