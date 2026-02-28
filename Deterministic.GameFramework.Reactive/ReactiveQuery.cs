using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.Reactive;

public struct ReactiveQuery<T1>
    where T1 : struct, IComponent
{
    public readonly ReactiveSystem System;
    public readonly GlobalState State;
    public readonly BitMask128 Mask;
    public readonly List<Func<Entity, bool>> Filters;

    public ReactiveQuery(ReactiveSystem system, GlobalState state)
    {
        System = system;
        State = state;
        Mask = new BitMask128();
        Mask.Set(InternalTypeId<T1>.Value);
        Filters = new List<Func<Entity, bool>>();
    }
    
    public ReactiveQuery<T1> Where<TComponent>(Func<TComponent, bool> predicate) where TComponent : struct, IComponent
    {
        // Capture state and type locally to avoid closure allocs if possible, 
        // but here we need closure for predicate.
        var state = State;
        Filters.Add(entity => 
        {
            if (!state.HasComponent<TComponent>(entity)) return false;
            ref var comp = ref state.GetComponent<TComponent>(entity);
            return predicate(comp);
        });
        return this;
    }
    
    public ReactiveQuery<T1> Where(Func<Entity, bool> predicate)
    {
        Filters.Add(predicate);
        return this;
    }

    public IDisposable Subscribe(Action<Entity> onAdd, Action<Entity> onRemove)
    {
        Func<Entity, bool>? combinedFilter = null;
        if (Filters.Count > 0)
        {
            var filters = Filters.ToArray(); // Copy to avoid modification issues
            combinedFilter = entity =>
            {
                foreach (var filter in filters)
                {
                    if (!filter(entity)) return false;
                }
                return true;
            };
        }

        var observer = ObserverPool<ArchetypeObserver>.Get();
        observer.Initialize(State, Mask, onAdd, onRemove, combinedFilter);
        System.Register(observer);
        return observer;
    }
}

public struct ReactiveQuery<T1, T2>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    public readonly ReactiveSystem System;
    public readonly GlobalState State;
    public readonly BitMask128 Mask;
    public readonly List<Func<Entity, bool>> Filters;

    public ReactiveQuery(ReactiveSystem system, GlobalState state)
    {
        System = system;
        State = state;
        Mask = new BitMask128();
        Mask.Set(InternalTypeId<T1>.Value);
        Mask.Set(InternalTypeId<T2>.Value);
        Filters = new List<Func<Entity, bool>>();
    }
    
    public ReactiveQuery<T1, T2> Where<TComponent>(Func<TComponent, bool> predicate) where TComponent : struct, IComponent
    {
        var state = State;
        Filters.Add(entity => 
        {
            if (!state.HasComponent<TComponent>(entity)) return false;
            ref var comp = ref state.GetComponent<TComponent>(entity);
            return predicate(comp);
        });
        return this;
    }

    public IDisposable Subscribe(Action<Entity> onAdd, Action<Entity> onRemove)
    {
        Func<Entity, bool>? combinedFilter = null;
        if (Filters.Count > 0)
        {
            var filters = Filters.ToArray();
            combinedFilter = entity =>
            {
                foreach (var filter in filters)
                {
                    if (!filter(entity)) return false;
                }
                return true;
            };
        }

        var observer = ObserverPool<ArchetypeObserver>.Get();
        observer.Initialize(State, Mask, onAdd, onRemove, combinedFilter);
        System.Register(observer);
        return observer;
    }
}

public struct ReactiveQuery<T1, T2, T3>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent
{
    public readonly ReactiveSystem System;
    public readonly GlobalState State;
    public readonly BitMask128 Mask;
    public readonly List<Func<Entity, bool>> Filters;

    public ReactiveQuery(ReactiveSystem system, GlobalState state)
    {
        System = system;
        State = state;
        Mask = new BitMask128();
        Mask.Set(InternalTypeId<T1>.Value);
        Mask.Set(InternalTypeId<T2>.Value);
        Mask.Set(InternalTypeId<T3>.Value);
        Filters = new List<Func<Entity, bool>>();
    }
    
    public ReactiveQuery<T1, T2, T3> Where<TComponent>(Func<TComponent, bool> predicate) where TComponent : struct, IComponent
    {
        var state = State;
        Filters.Add(entity => 
        {
            if (!state.HasComponent<TComponent>(entity)) return false;
            ref var comp = ref state.GetComponent<TComponent>(entity);
            return predicate(comp);
        });
        return this;
    }

    public IDisposable Subscribe(Action<Entity> onAdd, Action<Entity> onRemove)
    {
        Func<Entity, bool>? combinedFilter = null;
        if (Filters.Count > 0)
        {
            var filters = Filters.ToArray();
            combinedFilter = entity =>
            {
                foreach (var filter in filters)
                {
                    if (!filter(entity)) return false;
                }
                return true;
            };
        }

        var observer = ObserverPool<ArchetypeObserver>.Get();
        observer.Initialize(State, Mask, onAdd, onRemove, combinedFilter);
        System.Register(observer);
        return observer;
    }
}

public static class ReactiveSystemQueryExtensions
{
    public static ReactiveQuery<T1> ObservableCollection<T1>(this ReactiveSystem system)
        where T1 : struct, IComponent
    {
        if (system.BoundState == null) throw new InvalidOperationException("ReactiveSystem must be bound to a GlobalState to use ObservableCollection without explicit state.");
        return new ReactiveQuery<T1>(system, system.BoundState);
    }
    
    public static ReactiveQuery<T1, T2> ObservableCollection<T1, T2>(this ReactiveSystem system)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        if (system.BoundState == null) throw new InvalidOperationException("ReactiveSystem must be bound to a GlobalState to use ObservableCollection without explicit state.");
        return new ReactiveQuery<T1, T2>(system, system.BoundState);
    }

    public static ReactiveQuery<T1, T2, T3> ObservableCollection<T1, T2, T3>(this ReactiveSystem system)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        if (system.BoundState == null) throw new InvalidOperationException("ReactiveSystem must be bound to a GlobalState to use ObservableCollection without explicit state.");
        return new ReactiveQuery<T1, T2, T3>(system, system.BoundState);
    }
    
    // Extensions allowing passing explicit state if needed
    public static ReactiveQuery<T1> ObservableCollection<T1>(this ReactiveSystem system, GlobalState state)
        where T1 : struct, IComponent
    {
        return new ReactiveQuery<T1>(system, state);
    }

    public static ReactiveQuery<T1, T2> ObservableCollection<T1, T2>(this ReactiveSystem system, GlobalState state)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        return new ReactiveQuery<T1, T2>(system, state);
    }

    public static ReactiveQuery<T1, T2, T3> ObservableCollection<T1, T2, T3>(this ReactiveSystem system, GlobalState state)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        return new ReactiveQuery<T1, T2, T3>(system, state);
    }
}
