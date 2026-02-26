# Entity Component System (ECS)

The framework uses an **ECS architecture** to manage game state. This separates **Data** (Components) from **Identity** (Entities) and **Logic** (Actions/Systems).

## 1. GlobalState

`GlobalState` is the container for **everything** in your game world. It holds all entities and their components. You pass this object around to read or write data.

```csharp
GlobalState state = new GlobalState();
```

## 2. Entities

An `Entity` is just a unique ID (an integer). It doesn't hold data itself; it's a key to look up data.

```csharp
Entity player = state.CreateEntity();
```

## 3. Components

Components are **structs** that hold data. They must implement `IComponent` and contain only [deterministic types](determinism.md).

> **Important:** Components MUST be `structs`. Classes are not allowed.

```csharp
[NetworkId(1)] // Unique ID for serialization
public struct Position : IComponent
{
    public Vector2 Value;
}

[NetworkId(2)]
public struct Health : IComponent
{
    public Int Current;
    public Int Max;
}
```

### Registering Components
Before using a component, you must register it with the state (usually at startup).

```csharp
state.RegisterComponent<Position>();
state.RegisterComponent<Health>();
```

### Adding & Getting Components

```csharp
// Add data
state.AddComponent(player, new Position { Value = new Vector2(0, 0) });
state.AddComponent(player, new Health { Current = 100, Max = 100 });

// Read/Write data
ref Position pos = ref state.GetState<Position>(player);
pos.Value += new Vector2(1, 0); // Modifies the state directly!
```

> **Performance Tip:** `GetState<T>` returns a `ref`. This means you are modifying the data directly in the `GlobalState` array. No copies are made.

## 4. Querying / Filtering

To find all entities with a specific set of components, use `Filter<T1, T2>()`.

```csharp
// Find all entities with Position AND Health
foreach (var entity in state.Filter<Position, Health>())
{
    ref var pos = ref state.GetState<Position>(entity);
    ref var hp = ref state.GetState<Health>(entity);
    
    // Do logic...
}
```

## 5. Actions

In this framework, **Actions** are the primary way logic is executed. An `IAction` is a struct that contains the *parameters* for a game operation.

```csharp
public struct MoveAction : IAction
{
    public Vector2 Direction;
}
```

You execute actions via a `Dispatcher`. This allows the framework to track, predict, and rollback these actions.

```csharp
// In your game loop
state.Execute(new MoveAction { Direction = Vector2.Up }, player, dispatcher);
```
