# Observable Attributes

`ObservableAttribute<T>` enables reactive UI patterns (MVVM/MVC) by automatically notifying observers when model state changes. This allows clean separation between your game logic (model) and presentation layer (view).

## The Pattern

The reactive pattern follows a simple flow:

1. **Model** (Domain) holds `ObservableAttribute` properties
2. **View** (UI) observes those properties via `.Observe()`
3. **Automatic Updates** - when model changes, all observers are notified

This decouples your game logic from presentation. The model doesn't know about UI, and UI doesn't modify the model directly - it only observes and displays.

```csharp
player.Health.Observe(this, health => UpdateDisplay(health));
```

When `player.Health.Value` changes, `UpdateDisplay()` is called automatically. See **Example 4** for a complete demonstration.

## Key Benefits

### Separation of Concerns

Your domain layer contains pure game logic with no UI dependencies. The model exposes `ObservableAttribute` properties that UI components can bind to. This means you can change your UI framework (Godot, Unity, Avalonia, console) without touching game logic.

### Multiple Observers

A single model change can update multiple UI components simultaneously. When player health decreases, your health bar, stats panel, and damage indicator all update automatically - no manual synchronization needed.

### Automatic Cleanup

Observations are tied to the observer's lifetime. When a UI component is disposed, its subscriptions are automatically removed. This prevents memory leaks and eliminates manual unsubscribe logic.

## How It Works

When you create an `ObservableAttribute`, it wraps a value and maintains an internal event. Calling `.Observe()` registers a callback that fires whenever the value changes. The framework handles equality checking - callbacks only fire when the value actually changes.

The observer (first parameter) is crucial - it ties the subscription's lifetime to that domain. When the observer is disposed, the subscription is automatically cleaned up.

## Integration with UI Frameworks

This pattern integrates naturally with any UI framework:

- **Godot**: Observe attributes and update node properties
- **Unity**: Observe attributes and update UI components
- **Avalonia/WPF**: Observe attributes and trigger property changed events
- **Console**: Observe attributes and redraw text (as shown in Example 4)

The key is that your domain layer remains framework-agnostic. Only the view layer knows about the specific UI technology.

## Observable Collections

`ObservableAttributeList<T>` extends the pattern to collections. Instead of observing value changes, you observe add/remove operations:

```csharp
inventory.Items.ObserveAdd(this, e => OnItemAdded(e.Item));
inventory.Items.ObserveRemove(this, e => OnItemRemoved(e.Item));
```

This is useful for inventory systems, buff lists, or any dynamic collection that needs UI synchronization.

## When to Use

Use `ObservableAttribute` when:

- You need UI to react to model changes
- Multiple systems need to respond to the same state change
- You want to decouple game logic from presentation
- You're integrating with a UI framework that needs data binding

## Summary

`ObservableAttribute` enables clean reactive UI patterns by automatically notifying observers of state changes. This decouples your game logic from presentation, allows multiple UI components to observe the same data, and handles cleanup automatically. See **Example 4** for a working demonstration of the pattern in action.
