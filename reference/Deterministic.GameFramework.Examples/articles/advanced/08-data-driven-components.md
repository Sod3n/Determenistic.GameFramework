# Data-Driven Component Architecture

A data-driven component architecture separates game content from game logic by representing entity behaviors as composable components defined in external data files (JSON, Excel, databases). This pattern enables designers to create and modify game content without touching code, while maintaining type safety and performance.

## The Core Concept

Instead of hardcoding every card, item, or ability in your game, you define them as combinations of reusable components stored in data files. The game engine reads these definitions at runtime and constructs the appropriate domain objects.

A "Light Attack" card defined in JSON with damage and cost components gets translated by the factory into corresponding `DamageDomain` and `CostDomain` objects attached to the card. The data describes *what* the card does, while the domain classes implement *how* it works.

## Architecture Components

The architecture consists of three key layers:

**1. Data Models** - Simple DTOs that deserialize from JSON/Excel. A `CardModel` contains a card ID and list of `ComponentModel` objects, each with a type enum and nullable parameters (Value, MinValue, MaxValue, Query, etc.).

**2. Component Type Enumeration** - Maps component names to implementations. Examples: `Damage`, `Heal`, `Shield` (actions), `Cost`, `Retain`, `Exhaust` (properties), `StackDamage`, `ChangeCost` (modifiers).

**3. Factory Pattern** - Translates data into domains using a switch expression that maps each `ComponentType` to its corresponding domain constructor. The factory collects components from base cards and upgrades, then instantiates each domain as a child of the card.

## Universal Applications

This pattern applies to virtually any data-driven game:

### Card Games
- **Abilities**: Damage, heal, draw, discard
- **Costs**: Mana, energy, action points
- **Effects**: Buffs, debuffs, status effects

### RPGs
- **Items**: Weapons, armor, consumables
- **Skills**: Active abilities, passive bonuses
- **Quests**: Objectives, rewards, prerequisites

### Strategy Games
- **Units**: Stats, abilities, upgrade paths
- **Buildings**: Production, research, defenses
- **Technologies**: Unlocks, bonuses, requirements

### Action Games
- **Weapons**: Damage, fire rate, ammo
- **Power-ups**: Duration, effects, stacking
- **Enemies**: Behaviors, attacks, loot

## Key Benefits

### Designer Empowerment
Designers can create and balance content without programmer intervention. Changes to card values, ability effects, or item stats happen in data files, not code.

### Rapid Iteration
Tweaking a card's damage from 5 to 7 is a one-line JSON change, not a code modification requiring recompilation and deployment.

### Composition Over Inheritance
Cards are defined by their components, not by class hierarchies. A "Fire Sword" doesn't need to inherit from both "Weapon" and "Fire Item"—it just has weapon and fire components.

### Upgrade Systems
Upgrades naturally extend the component model. An upgraded card simply adds more components by collecting components from the base card, then appending components from each upgrade card in sequence.

### Type Safety
Despite being data-driven, the system maintains compile-time type safety through enums and factory methods. Invalid component types are caught at load time, not during gameplay.

## When to Use Data vs. Code

Not everything should be data-driven. Here's a decision framework:

### Use Data-Driven Components For:

✅ **Atomic, reusable behaviors**
- Damage, heal, shield (simple numeric effects)
- Cost, retain, exhaust (card properties)
- Draw, discard, shuffle (deck manipulation)

✅ **Parameterizable effects**
- Effects that vary only in magnitude (damage: 5 vs. 10)
- Effects with simple configuration (AoE: true/false)

✅ **Content that designers balance**
- Card stats, item values, ability costs
- Anything requiring frequent iteration

### Use Code-Driven Logic For:

❌ **Complex conditional logic**
```
If enemy used card with "Analyzed" effect:
  → Deal 5 damage
Else:
  → Mark that card "Analyzed"
  → Deal 10 damage
```

❌ **Multi-step decision trees**
- State machines with multiple branches
- Context-dependent behavior chains

❌ **Unique mechanics**
- One-off boss abilities
- Special event interactions
- Game-specific rules

### The Hybrid Approach

For complex effects, create a component type that encapsulates the logic. Add `PatternAnalysis` to your `ComponentType` enum, then map it in the factory to `new PatternAnalysisDomain(card, damageIfAnalyzed: m.Value, damageIfNot: m.MaxValue)`.

The component is data-defined (appears in JSON), but the logic is code-implemented (in the domain class). This gives you designer control over parameters while maintaining programmer control over logic.

## Avoiding the "Scripting Language" Trap

A common mistake is trying to represent all logic in data, effectively creating a new scripting language. Warning signs:

- 🚫 Components that represent control flow (if/then/else structures)
- 🚫 Components that reference other components by index
- 🚫 Nested component hierarchies for logic (sequences, branches)

## Implementation Workflow

### Loading Data

The framework provides `GameDataLoader.LoadAsync()` which automatically finds data files relative to the caller using `CallerFilePath`:

```csharp
// Automatically finds Data/ folder next to the calling file
await GameDataLoader.LoadAsync(itemsData);

// Or specify explicit path when needed (e.g., production)
await GameDataLoader.LoadAsync(itemsData, "/path/to/data");
```

This single method works in all environments - development, examples, tests, and production. No need for different loading strategies.

### Complete Workflow

**1. Define Data Structure** - Create JSON files with entity definitions. Each entity has an ID and array of components with types and parameters.

**2. Create Data Models** - Define entry and model classes that inherit from `GameData<TEntry, TModel>` to handle deserialization and grouping.

**3. Load Data** - Call `GameDataLoader.LoadAsync()` at startup. It finds the data directory automatically or uses an explicit path.

**4. Create Factory** - Implement factory methods that read models and instantiate domain objects with component children.

**5. Use in Game** - Call factory methods with entity IDs to create game objects composed of their components.

See `Examples/Advanced/Example_DataDrivenComponents.cs` for a complete working implementation.

## Advanced: Auto-Generated Enums

For type safety without manual enum maintenance, use source generators to automatically create enums from data files. This keeps enums synchronized with your JSON data at compile time.

### The Problem

When designers add new component types to JSON, you face a choice:
- **Manual enums** - Programmers update enums by hand (error-prone, causes delays)
- **String-based** - Use strings everywhere (no type safety, runtime errors)

### The Solution

A source generator scans your JSON files and automatically generates enums from unique values:

**1. Create Configuration** (`enumgen.config.json`):
```json
{
  "ComponentType": {
    "source": "Definitions",
    "field": "component_type"
  },
  "CharacterId": {
    "source": "Characters",
    "field": "character_id"
  }
}
```

**2. Define Data** (`Definitions.json`):
```json
{
  "1": { "component_type": "damage", "value": 10 },
  "2": { "component_type": "heal", "value": 5 },
  "3": { "component_type": "shield", "value": 8 }
}
```

**3. Enum Generated Automatically**:
```csharp
// <auto-generated />
// Generated from Definitions.json, field: component_type

namespace Deterministic.GameFramework.Generated;

public enum ComponentType
{
    Damage,
    Heal,
    Shield
}
```

### Benefits

**Designer Autonomy** - Designers add new types in JSON, enums update automatically at next build. No programmer intervention needed.

**Type Safety** - Full compile-time checking. Impossible to use invalid component types.

**No Sync Issues** - Enums always match data. Can't get out of sync.

**Automatic Conversion** - Generator converts `snake_case` to `PascalCase` automatically.

### Implementation

The framework includes `GameDataEnumGenerator` source generator. To use it:

1. Add `enumgen.config.json` to your GameData folder
2. Mark JSON files as `AdditionalFiles` in your `.csproj`
3. Reference the source generator project
4. Enums generate automatically at compile time

See `Deterministic.GameFramework.SourceGenerators/GameDataEnumGenerator.cs` for the implementation.

## Best Practices

**Validate Early** - Validate all data at load time, not during gameplay. Fail fast with clear error messages. Consider a separate validation tool that designers can run before committing data changes.

**Cache Models** - Parse JSON once at startup and cache models in memory. Create domain objects lazily only when needed (e.g., when cards enter a player's hand).

**Test with Real Data** - Load actual game data in tests to catch issues before they reach players. Verify every entity can be created without exceptions.

## Summary

| Aspect | Approach |
|--------|----------|
| **Simple effects** | Data-driven components |
| **Complex logic** | Code-driven domains with data parameters |
| **Unique mechanics** | Pure code implementation |
| **Designer content** | Always data-driven |
| **Control flow** | Never in data |

Data-driven component architecture provides a powerful balance between designer flexibility and programmer control. By keeping components atomic and reusable while implementing complex logic in code, you create a maintainable system that scales with your game's complexity.

The key is knowing where to draw the line: use data for content, use code for logic, and use hybrid approaches for complex but parameterizable behaviors.
