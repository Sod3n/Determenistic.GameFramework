using System;

namespace Deterministic.GameFramework.CoreV2;

public static class ReactionExtensions
{
    /// <summary>
    /// Syntactic sugar for adding a Reaction Tag component to an entity.
    /// Usage: entity.AddReaction<MyReactionTag>(state);
    /// </summary>
    public static void AddReaction<T>(this Entity entity, GlobalState state) where T : struct, IComponent
    {
        state.AddComponent(entity, new T());
    }

    /// <summary>
    /// Syntactic sugar for adding a Reaction Tag component with initial data.
    /// Usage: entity.AddReaction(state, new MyReactionTag { ... });
    /// </summary>
    public static void AddReaction<T>(this Entity entity, GlobalState state, T component) where T : struct, IComponent
    {
        state.AddComponent(entity, component);
    }

    /// <summary>
    /// Syntactic sugar for removing a Reaction Tag component from an entity.
    /// Usage: entity.RemoveReaction<MyReactionTag>(state);
    /// </summary>
    public static void RemoveReaction<T>(this Entity entity, GlobalState state) where T : struct, IComponent
    {
        state.RemoveComponent<T>(entity);
    }
}
