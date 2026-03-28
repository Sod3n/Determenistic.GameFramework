using System;
using Deterministic.GameFramework.ECS;
using R3;

namespace Deterministic.GameFramework.Reactive;

public static class ReactiveSystemObservableExtensions
{
    public static Observable<Entity> ObserveAdd<T>(this ReactiveSystem system) where T : struct, IComponent
    {
        return Observable.Create<Entity>(observer =>
        {
            if (system.BoundState == null) 
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }

            return system.ObserveArchetype<T>(system.BoundState, 
                onAdd: entity => observer.OnNext(entity), 
                onRemove: _ => { });
        });
    }

    public static Observable<Entity> ObserveRemove<T>(this ReactiveSystem system) where T : struct, IComponent
    {
        return Observable.Create<Entity>(observer =>
        {
             if (system.BoundState == null) 
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }

            return system.ObserveArchetype<T>(system.BoundState, 
                onAdd: _ => { }, 
                onRemove: entity => observer.OnNext(entity));
        });
    }
}
