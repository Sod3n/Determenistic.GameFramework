# Deterministic Game Framework

> **License:** All Rights Reserved - See [LICENSE](LICENSE) for details.

A universal, deterministic game state management framework for building multiplayer games with lockstep synchronization.

## Projects

### Deterministic.GameFramework.Core
Core game state management framework with:
- Deterministic action processing
- Domain-driven state management
- Reaction system for game logic
- Observable attributes and cached trees
- Deterministic random number generation

### Deterministic.GameFramework.Server
Server-side networking layer built on SignalR:
- Game hub for client connections
- Match management
- Network synchronization
- Determinism validation
- Thread-safe game state execution

### Deterministic.GameFramework.Client
Client-side networking library:
- SignalR client wrapper
- Action processor for client-side predictions
- Network time synchronization
- REST API client

### Deterministic.GameFramework.SourceGenerators
Roslyn source generators for code generation:
- Enum generators for game data

### Deterministic.GameFramework.Examples
Example implementations and documentation.

## Building

```bash
dotnet build Determenistic.GameFramework.sln
```

## Using as a Git Submodule

To use this framework in your project as a git submodule:

```bash
# In your game project root
git submodule add https://github.com/Sod3n/Determenistic.GameFramework.git Framework
git submodule update --init --recursive
```

Then reference the projects in your game's solution:

```xml
<ItemGroup>
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Core\Deterministic.GameFramework.Core.csproj" />
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Server\Deterministic.GameFramework.Server.csproj" />
</ItemGroup>
```

## Configuration

### Client DLL Auto-Copy

The `Deterministic.GameFramework.Client` project can automatically copy built DLLs to a client plugins folder. To enable this:

```xml
<PropertyGroup>
  <CopyToClientPlugins>true</CopyToClientPlugins>
  <ClientPluginsPath>$(ProjectDir)..\..\YourClient\plugins\Network\</ClientPluginsPath>
</PropertyGroup>
```

## License

See LICENSE file for details.
