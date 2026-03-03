using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using ObservableCollections;
using R3;

namespace Deterministic.GameFramework.Reactive;

public static class ReactiveQueryObservableExtensions
{
    public static ObservableList<TResult> ToObservableList<T1, TResult>(
        this ReactiveQuery<T1> query, 
        Func<Entity, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
    {
        var list = new ObservableList<TResult>();
        var tracking = new Dictionary<Entity, TResult>();

        var subscription = query.Subscribe(
            onAdd: entity => 
            {
                var item = selector(entity);
                tracking.Add(entity, item);
                list.Add(item);
            },
            onRemove: entity => 
            {
                if (tracking.TryGetValue(entity, out var item))
                {
                    list.Remove(item);
                    tracking.Remove(entity);
                    if (item is IDisposable d) d.Dispose();
                }
            }
        );
        
        disposables.Add(subscription);
        
        // Clean up items when the ViewModel/Scope is disposed
        disposables.Add(Disposable.Create(() => 
        {
             foreach(var item in list)
             {
                 if (item is IDisposable d) d.Dispose();
             }
             list.Clear();
             tracking.Clear();
        }));

        return list;
    }
    
    public static ObservableList<TResult> ToObservableList<T1, T2, TResult>(
        this ReactiveQuery<T1, T2> query, 
        Func<Entity, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var list = new ObservableList<TResult>();
        var tracking = new Dictionary<Entity, TResult>();

        var subscription = query.Subscribe(
            onAdd: entity => 
            {
                var item = selector(entity);
                tracking.Add(entity, item);
                list.Add(item);
            },
            onRemove: entity => 
            {
                if (tracking.TryGetValue(entity, out var item))
                {
                    list.Remove(item);
                    tracking.Remove(entity);
                    if (item is IDisposable d) d.Dispose();
                }
            }
        );
        
        disposables.Add(subscription);
        
        disposables.Add(Disposable.Create(() => 
        {
             foreach(var item in list)
             {
                 if (item is IDisposable d) d.Dispose();
             }
             list.Clear();
             tracking.Clear();
        }));

        return list;
    }

    public static ObservableList<TResult> ToObservableList<T1, T2, T3, TResult>(
        this ReactiveQuery<T1, T2, T3> query, 
        Func<Entity, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        var list = new ObservableList<TResult>();
        var tracking = new Dictionary<Entity, TResult>();

        var subscription = query.Subscribe(
            onAdd: entity => 
            {
                var item = selector(entity);
                tracking.Add(entity, item);
                list.Add(item);
            },
            onRemove: entity => 
            {
                if (tracking.TryGetValue(entity, out var item))
                {
                    list.Remove(item);
                    tracking.Remove(entity);
                    if (item is IDisposable d) d.Dispose();
                }
            }
        );
        
        disposables.Add(subscription);
        
        disposables.Add(Disposable.Create(() => 
        {
             foreach(var item in list)
             {
                 if (item is IDisposable d) d.Dispose();
             }
             list.Clear();
             tracking.Clear();
        }));

        return list;
    }

    // Shorthands using Context
    public static ObservableList<TResult> ToObservableList<T1, TResult>(
        this ReactiveQuery<T1> query, 
        Func<Context, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
    {
        return query.ToObservableList(entity => selector(new Context(query.State, entity)), disposables);
    }

    public static ObservableList<TResult> ToObservableList<T1, T2, TResult>(
        this ReactiveQuery<T1, T2> query, 
        Func<Context, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        return query.ToObservableList(entity => selector(new Context(query.State, entity)), disposables);
    }

    public static ObservableList<TResult> ToObservableList<T1, T2, T3, TResult>(
        this ReactiveQuery<T1, T2, T3> query, 
        Func<Context, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        return query.ToObservableList(entity => selector(new Context(query.State, entity)), disposables);
    }

    // Extensions on ReactiveSystem directly
    public static ObservableList<TResult> ObservableList<T1, TResult>(
        this ReactiveSystem system,
        Func<Context, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
    {
        return system.ObservableCollection<T1>().ToObservableList(selector, disposables);
    }

    public static ObservableList<TResult> ObservableList<T1, T2, TResult>(
        this ReactiveSystem system,
        Func<Context, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        return system.ObservableCollection<T1, T2>().ToObservableList(selector, disposables);
    }

    public static ObservableList<TResult> ObservableList<T1, T2, T3, TResult>(
        this ReactiveSystem system,
        Func<Context, TResult> selector,
        CompositeDisposable disposables)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        return system.ObservableCollection<T1, T2, T3>().ToObservableList(selector, disposables);
    }
}
