using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Reactive;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Reactive;

public static class ReactiveExtensions
{
    /// <summary>
    /// Subscribes to changes in a value derived from a context.
    /// This overload is allocation-free if the selector and callback are static or instance methods (not closures).
    /// </summary>
    public static IDisposable Subscribe<TValue>(this ReactiveSystem system, EntityWorld context, Func<EntityWorld, TValue> selector, Action<EntityWorld, TValue> callback)
        where TValue : struct, IEquatable<TValue>
    {
        var observer = ObserverPool<PollingObserver<TValue>>.Get();
        observer.Initialize(context, selector, callback, null);
        system.Register(observer);
        return observer;
    }

    public static IDisposable Subscribe<TValue>(this ReactiveSystem system, Func<TValue> selector, Action<TValue> callback) 
        where TValue : struct, IEquatable<TValue>
    {
        var observer = ObserverPool<SimplePollingObserver<TValue>>.Get();
        observer.Initialize(selector, callback, null);
        system.Register(observer);
        return observer;
    }

    public static IDisposable Subscribe<TValue>(this ReactiveSystem system, Func<TValue> selector, Action<TValue> callback, IEqualityComparer<TValue> comparer) 
    {
        var observer = ObserverPool<SimplePollingObserver<TValue>>.Get();
        observer.Initialize(selector, callback, comparer);
        system.Register(observer);
        return observer;
    }

    public static IDisposable ObserveArchetype<T1>(this ReactiveSystem system, EntityWorld state, Action<Entity> onAdd, Action<Entity> onRemove)
        where T1 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T1>.IntId);
        
        var observer = ObserverPool<ArchetypeObserver>.Get();
        observer.Initialize(state, mask, onAdd, onRemove);
        system.Register(observer);
        return observer;
    }

    public static IDisposable ObserveArchetype<T1, T2>(this ReactiveSystem system, EntityWorld state, Action<Entity> onAdd, Action<Entity> onRemove)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T1>.IntId);
        mask.Set(ComponentId<T2>.IntId);
        
        var observer = ObserverPool<ArchetypeObserver>.Get();
        observer.Initialize(state, mask, onAdd, onRemove);
        system.Register(observer);
        return observer;
    }

    public static IDisposable ObserveArchetype<T1, T2, T3>(this ReactiveSystem system, EntityWorld state, Action<Entity> onAdd, Action<Entity> onRemove)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T1>.IntId);
        mask.Set(ComponentId<T2>.IntId);
        mask.Set(ComponentId<T3>.IntId);
        
        var observer = ObserverPool<ArchetypeObserver>.Get();
        observer.Initialize(state, mask, onAdd, onRemove);
        system.Register(observer);
        return observer;
    }
}
