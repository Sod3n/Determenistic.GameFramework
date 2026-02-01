# Introduction

TurnBasedPrototype.Server.Core is a framework for building deterministic, multiplayer game servers using the **DAR (Domain-Action-Reaction)** pattern.

## Purpose

The Core library provides foundational utilities and abstractions for:
- **Deterministic game state** — Reproducible simulations for client-server sync
- **Domain hierarchy** — Tree-structured game entities
- **Action system** — Type-safe state mutations with validation
- **Reaction system** — Event-driven responses to actions
- **Network synchronization** — Client-side prediction with server authority

## Key Components

- **BaseGameState** — Root domain with ID registry and random provider
- **LeafDomain / BranchDomain** — Game entities in a tree structure
- **DARAction** — Actions that modify game state
- **Reaction** — Hooks that respond to actions (Prepare, Abort, Before, After)
- **NetworkAction** — Multiplayer-aware actions with serialization
- **MatchManager** — Manages active game sessions

## Getting Started

New to the framework? Start with the [Hello World tutorial](getting-started/01-hello-world.md) to build your first DAR project in minutes.
