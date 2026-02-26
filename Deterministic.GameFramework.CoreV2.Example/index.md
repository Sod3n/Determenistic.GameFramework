# Deterministic Game Framework V2

Welcome to the documentation for **Deterministic Game Framework V2**.

This framework is designed to build **high-performance, deterministic multiplayer games** using a struct-based ECS (Entity Component System) architecture. It ensures that game logic runs exactly the same way on any machine, enabling features like **prediction, rollback networking, and replay systems**.

## Key Features

- **100% Deterministic Math**: Custom `Float`, `Vector2`, and `Vector3` types ensuring identical results across platforms.
- **Struct-Based ECS**: Zero-allocation entity and component storage for maximum performance.
- **Rollback Networking**: Built-in support for saving state, rolling back, and resimulating ticks.
- **Source Generators**: Automatic code generation for networking boilerplate and type safety analysis.

## Quick Links

- [Introduction](articles/intro.md)
- [Core Concepts](articles/concepts/determinism.md)
- [API Reference](api/index.md)
