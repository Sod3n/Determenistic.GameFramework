# Part 3: Domain Hierarchy

Domains form a tree structure that mirrors your game's entity relationships. This enables powerful querying, automatic cleanup, and hierarchical event handling.

## The Domain Tree

Every domain has a parent (except the root GameState). When you create a domain, it automatically registers itself as a child of its parent. This creates a tree structure that represents your game world.

## Example Structure

A typical RPG might have this hierarchy:

```
GameState (root)
├── PlayerDomain
│   └── InventoryDomain
│       ├── ItemDomain (Sword)
│       └── ItemDomain (Shield)
└── EnemyDomain
```

Each domain is created by passing its parent:

```csharp
var player = new PlayerDomain(gameState, "Hero");
var inventory = new InventoryDomain(player);
var sword = new ItemDomain(inventory, "Sword");
```

The tree structure is built automatically as domains are constructed.

## Navigating the Tree

The framework provides methods to traverse the hierarchy:

**Find parents:**
```csharp
var owner = item.GetInParent<PlayerDomain>();  // Search up the tree
```

**Find children:**
```csharp
var inventory = player.GetFirst<InventoryDomain>();     // First child of type
var allItems = gameState.GetAll<ItemDomain>();          // All descendants (recursive)
var direct = player.GetAll<InventoryDomain>(false);     // Direct children only
```

These queries enable flexible access to related entities without storing explicit references.

## Lifecycle Management

Domains are automatically added when constructed and can be removed explicitly:

```csharp
item.RemoveFromParent();  // Detach from tree
item.Dispose();           // Dispose and detach
```

When a domain is disposed, all its children are also disposed recursively. This ensures clean teardown of entire subtrees.

## Observing Changes

`BranchDomain` exposes an `ObservableAttributeList` for its children:

```csharp
Subdomains.ObserveAdd(this, e => OnChildAdded(e.Item));
Subdomains.ObserveRemove(this, e => OnChildRemoved(e.Item));
```

This enables reactive patterns for dynamic collections like inventories or spawn systems.

## Domain IDs

Every domain has a unique integer `Id` assigned when added to the tree. The root GameState maintains a registry for ID-based lookups:

```csharp
var domain = gameState.GetDomain(playerId);
```

This is essential for network actions, which reference entities by ID rather than object references (see Network Actions article).

## Benefits

The hierarchical structure provides:

- **Automatic cleanup** - Disposing a parent disposes all children
- **Flexible queries** - Find entities by type without explicit references
- **Scoped reactions** - Observe actions on specific subtrees
- **Natural organization** - Tree mirrors game entity relationships
- **ID-based lookup** - Reference entities by ID for network serialization

## Next Steps

- [Part 4: Observable Attributes](04-observable-attributes.md) — Reactive UI bindings

See **Example 3** for a complete demonstration of domain hierarchy with inventory management.
