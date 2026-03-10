# TurnBasedPrototype.Server.Examples

Runnable examples demonstrating the TurnBasedPrototype.Server.Core framework.

## Quick Start

```bash
cd Server/TurnBasedPrototype.Server.Examples
dotnet run
```

## Available Examples

| # | Name | Concepts |
|---|------|----------|
| 1 | Hello World | GameState, Domain, Action basics |
| 2 | Reactions | Prepare, Abort, Before, After hooks |
| 3 | Domain Hierarchy | Parent-child relationships, tree navigation |

## Run Specific Example

```bash
# Interactive menu
dotnet run

# Or specify directly
dotnet run -- 1   # Hello World
dotnet run -- 2   # Reactions
dotnet run -- 3   # Domain Hierarchy
```

## Documentation

Each example corresponds to a tutorial:

- [Hello World](articles/getting-started/01-hello-world.md)
- [Reactions](articles/getting-started/02-reactions.md)
- [Domain Hierarchy](articles/getting-started/03-domain-hierarchy.md)
- [Network Actions](articles/getting-started/04-network-actions.md)

## Build Docs

```bash
docfx docfx.json --serve
```
