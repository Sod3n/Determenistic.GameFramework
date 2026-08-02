# Deterministic.GameFramework

> **License:** All Rights Reserved — see [LICENSE](LICENSE).

A deterministic ECS game framework for **realtime multiplayer at 60 Hz**: fixed-point simulation, a deterministic 2D physics and navigation stack, and two interchangeable network synchronization strategies.

Everything the simulation touches is bit-reproducible across machines — the physics solver, the navmesh, the RNG — so the server and every client can run the same tick and get the same bytes.

Looking for the older, much smaller turn-based framework (DAR domain tree, action replay over SignalR)? That line lives on in [DAR.GameFramework](https://github.com/Sod3n/DAR.GameFramework).

## Synchronization

Pick per match via `SyncMode`; client and server must agree.

### `SyncMode.GGPO` — rollback + resimulation

Classic rollback. Clients run ahead of the server by a confirmation window, inputs are resent redundantly until acknowledged, and a mispredicted input rewinds the world and resimulates the intervening ticks. State hashes are verified periodically; `DesyncRecorder` captures both sides when they disagree.

### `SyncMode.DeltaSync` — correction by delta composition, without resimulation

Server-authoritative, and the reason this framework exists in its current form. The server diffs its world against the last broadcast baseline and ships a per-tick op stream. The client predicts locally and records its own per-tick delta. When the server's delta for tick *T* arrives, the client computes

```
TargetDelta = ServerDelta − ClientDelta
```

algebraically, op by op (`DeltaSubtractor`):

- server op matches the client's byte-for-byte → prediction was right, **emit nothing**;
- server op differs or the client never predicted it → **emit the server op** (snap);
- client predicted something the server didn't do → **emit a revert** built from the baseline bytes, or the trivial inverse (`EntityDestroy` for `EntityCreate`, `ComponentRemove` for `ComponentAdd`, …).

For modified components the correction is byte arithmetic — `(server_new − baseline) − predicted` — applied on top of the client's *current*, further-advanced state. So a correct prediction costs nothing (`TargetDelta` is empty), a wrong one snaps only the diverged bytes, and **the ticks the client has already simulated past T are never thrown away and re-run**. No rollback buffer, no confirmation window, no resimulation cost that scales with latency.

## Layout

| Area | Projects |
| --- | --- |
| `Core/` | `ECS` (aligned component stores, archetypes, systems), `DAR` (actions, scheduler, dispatcher, prediction ids), `Reactive` (reactive queries, observers, view models), `DeltaSync`, `GGPO`, `Serialization` (state serializer, hasher, history), `Debugging` (desync recorder, state dumper), `Scenes`, `Types` (fixed-point math), `Common`, `Utils`, `Extensions` |
| `2D/` | `TwoD.Physics` (deterministic Box2D port + Rapier interop), `TwoD.Navigation` (navmesh baking, agents, obstacles, queries), `CDT` (constrained Delaunay triangulation), `TwoD` |
| `Networking/` | `Network` (transport-agnostic client/server, matchmaking, packets), `Network.LiteNetLib` (UDP), `Network.SignalR`, `Server` |
| `Deterministic.GameFramework.Box2D/` | line-by-line deterministic port of Box2D v3 |
| `Deterministic.GameFramework.Detour/` | Recast/Detour navmesh + crowd, deterministic port |
| `Tests/`, `Benchmarks/` | xUnit suites and BenchmarkDotNet projects per area |
| `Deterministic.GameFramework.SourceGenerators/` | component models, stable ids, struct layout, action generation, determinism analyzer |

Targets `net8.0` and `netstandard2.1` (the latter for Godot/Unity clients).

## Build

```bash
dotnet build Deterministic.GameFramework.sln
dotnet test  Deterministic.GameFramework.sln
```

## Use as a submodule

```bash
git submodule add https://github.com/Sod3n/Deterministic.GameFramework.git Framework
git submodule update --init --recursive
```

```xml
<ItemGroup>
  <ProjectReference Include="..\Framework\Core\Deterministic.GameFramework.ECS\Deterministic.GameFramework.ECS.csproj" />
  <ProjectReference Include="..\Framework\Networking\Deterministic.GameFramework.Server\Deterministic.GameFramework.Server.csproj" />
</ItemGroup>
```

## Which one do I want?

| | **This repo** | **[DAR.GameFramework](https://github.com/Sod3n/DAR.GameFramework)** |
| --- | --- | --- |
| Target | 60 Hz action games | Turn-based, card, tactics, slow real-time |
| State | ECS component stores | Domain tree of typed objects |
| Sync | Rollback (GGPO) or delta composition over UDP | Action ordering + replay over SignalR |
| Physics / navigation | deterministic Box2D port, Detour + CDT navmesh | none |
| Determinism | structurally enforced — own numeric types and containers, Roslyn analyzer, per-tick hashing | execution-order only, checked by an opt-in shadow simulation |
| Size | ~650 `.cs` files | ~110 `.cs` files |
