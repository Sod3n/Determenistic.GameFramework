using System.Collections;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Deterministic.GameFramework.Core.Extensions;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Event args for list add operations.
/// </summary>
public readonly struct ListAddEventArgs<TValue>(TValue item, int index)
{
    public TValue Item { get; } = item;
    public int Index { get; } = index;
}

/// <summary>
/// Event args for list remove operations.
/// </summary>
public readonly struct ListRemoveEventArgs<TValue>(TValue item, int index)
{
    public TValue Item { get; } = item;
    public int Index { get; } = index;
}

/// <summary>
/// Event args for list set operations.
/// </summary>
public readonly struct ListSetEventArgs<TValue>(TValue oldValue, TValue newValue, int index)
{
    public TValue OldValue { get; } = oldValue;
    public TValue NewValue { get; } = newValue;
    public int Index { get; } = index;
}

public class ObservableAttributeList<TValue> : IList<TValue>
{
    private List<TValue> _list = new();
    
    // Events
    private event Action<ListAddEventArgs<TValue>>? OnAdd;
    private event Action<ListRemoveEventArgs<TValue>>? OnBeforeRemove;
    private event Action<ListRemoveEventArgs<TValue>>? OnRemove;
    private event Action<ListAddEventArgs<TValue>>? OnInsert;
    private event Action<ListSetEventArgs<TValue>>? OnSet;
    private event Action? OnBeforeClear;
 
    private event Action? OnClear;
    private event Action? OnSort;
    
    public TValue this[int index]
    {
        get => _list[index];
        set
        {
            var oldValue = _list[index];
            if (EqualityComparer<TValue>.Default.Equals(oldValue, value)) return;
            _list[index] = value;
            OnSet?.Invoke(new ListSetEventArgs<TValue>(oldValue, value, index));
        }
    }

    public int Count => _list.Count;
    public bool IsReadOnly => false;
    
    public ObservableAttributeList() { }
    
    public ObservableAttributeList(IEnumerable<TValue> collection)
    {
        _list.AddRange(collection);
    }

    public void Add(TValue item)
    {
        _list.Add(item);
        OnAdd?.Invoke(new ListAddEventArgs<TValue>(item, _list.Count - 1));
    }

    public void AddRange(IEnumerable<TValue> items)
    {
        foreach (var item in items)
            Add(item);
    }

    public bool Remove(TValue item)
    {
        var index = _list.IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index)
    {
        var item = _list[index];
        OnBeforeRemove?.Invoke(new ListRemoveEventArgs<TValue>(item, index));
        _list.RemoveAt(index);
        OnRemove?.Invoke(new ListRemoveEventArgs<TValue>(item, index));
    }

    public void Insert(int index, TValue item)
    {
        _list.Insert(index, item);
        OnInsert?.Invoke(new ListAddEventArgs<TValue>(item, index));
    }

    public void Clear()
    {
        OnBeforeClear?.Invoke();
        _list.Clear();
        OnClear?.Invoke();
    }

    public void Sort()
    {
        _list.Sort();
        OnSort?.Invoke();
    }

    public void Sort(IComparer<TValue> comparer)
    {
        _list.Sort(comparer);
        OnSort?.Invoke();
    }

    public void Sort(Comparison<TValue> comparison)
    {
        _list.Sort(comparison);
        OnSort?.Invoke();
    }

    /// <summary>
    /// Shuffles the list using Fisher-Yates algorithm with a deterministic seed.
    /// </summary>
    public void Shuffle(Random random)
    {
        for (int i = _list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (_list[i], _list[j]) = (_list[j], _list[i]);
        }
        OnSort?.Invoke();
    }

    public bool Contains(TValue item) => _list.Contains(item);
    public int IndexOf(TValue item) => _list.IndexOf(item);
    public void CopyTo(TValue[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
    public IEnumerator<TValue> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Observe methods - auto-add to observer's Disposables AND return IDisposable
    public IDisposable ObserveAdd(LeafDomain observer, Action<ListAddEventArgs<TValue>> callback)
    {
        OnAdd += callback;
        var disposable = new ActionDisposable(() => OnAdd -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    public IDisposable ObserveRemove(LeafDomain observer, Action<ListRemoveEventArgs<TValue>> callback)
    {
        OnRemove += callback;
        var disposable = new ActionDisposable(() => OnRemove -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    public IDisposable ObserveBeforeRemove(LeafDomain observer, Action<ListRemoveEventArgs<TValue>> callback)
    {
        OnBeforeRemove += callback;
        var disposable = new ActionDisposable(() => OnBeforeRemove -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    public IDisposable ObserveInsert(LeafDomain observer, Action<ListAddEventArgs<TValue>> callback)
    {
        OnInsert += callback;
        var disposable = new ActionDisposable(() => OnInsert -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    public IDisposable ObserveSet(LeafDomain observer, Action<ListSetEventArgs<TValue>> callback)
    {
        OnSet += callback;
        var disposable = new ActionDisposable(() => OnSet -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    public IDisposable ObserveClear(LeafDomain observer, Action callback)
    {
        OnClear += callback;
        var disposable = new ActionDisposable(() => OnClear -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    public IDisposable ObserveBeforeClear(LeafDomain observer, Action callback)
    {
        OnBeforeClear += callback;
        var disposable = new ActionDisposable(() => OnBeforeClear -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    public IDisposable ObserveSort(LeafDomain observer, Action callback)
    {
        OnSort += callback;
        var disposable = new ActionDisposable(() => OnSort -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Observes when a specific item is added to or removed from this list.
    /// </summary>
    public IDisposable ObserveItemPresence(LeafDomain observer, TValue item, Action? onAdded = null, Action? onRemoved = null)
    {
        var disposables = new CompositeDisposable();

        if (onAdded != null)
        {
            Action<ListAddEventArgs<TValue>> addCallback = e =>
            {
                if (EqualityComparer<TValue>.Default.Equals(e.Item, item))
                    onAdded();
            };
            OnAdd += addCallback;
            disposables.Add(new ActionDisposable(() => OnAdd -= addCallback));
        }

        if (onRemoved != null)
        {
            Action<ListRemoveEventArgs<TValue>> removeCallback = e =>
            {
                if (EqualityComparer<TValue>.Default.Equals(e.Item, item))
                    onRemoved();
            };
            OnRemove += removeCallback;
            disposables.Add(new ActionDisposable(() => OnRemove -= removeCallback));
        }

        observer.Disposables.Add(disposables);
        return disposables;
    }
}
