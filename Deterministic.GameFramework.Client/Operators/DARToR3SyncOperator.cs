using System;
using System.Collections.Generic;
using R3;
using Deterministic.GameFramework.Core;
using ObservableCollections;

namespace Deterministic.GameFramework.Client.Operators
{
    /// <summary>
    /// Operator that synchronizes a DAR ObservableList to an R3 ObservableList with a transformation function.
    /// Automatically disposes created items when they're removed.
    /// </summary>
    public class DARToR3SyncOperator<TSource, TTarget> : IDisposable where TTarget : IDisposable
    {
        private readonly ObservableAttributeList<TSource> _darSource;
        private readonly ObservableList<TTarget> _r3Target;
        private readonly Func<TSource, TTarget> _selector;
        private readonly Dictionary<TSource, TTarget> _mapping = new();
        private readonly CompositeDisposable _disposables = new();

        public DARToR3SyncOperator(
            ObservableAttributeList<TSource> darSource, 
            ObservableList<TTarget> r3Target, 
            Func<TSource, TTarget> selector)
        {
            _darSource = darSource;
            _r3Target = r3Target;
            _selector = selector;

            // Subscribe to Add actions
            _darSource.ObserveAdd(
                null,
                (_) =>
                {
                    var sourceItem = _darSource[_darSource.Count - 1];
                    var targetItem = _selector(sourceItem);
                    _mapping[sourceItem] = targetItem;
                    _r3Target.Add(targetItem);
                }).AddTo(_disposables);

            // Subscribe to Remove actions - rebuild target list
            _darSource.ObserveRemove(
                null,
                (_) =>
                {
                    RebuildTargetList();
                }).AddTo(_disposables);

            // Subscribe to Insert actions - rebuild target list
            _darSource.ObserveInsert(
                null,
                (_) =>
                {
                    RebuildTargetList();
                }).AddTo(_disposables);

            // Subscribe to Clear actions
            _darSource.ObserveClear(
                null,
                () =>
                {
                    foreach (var item in _r3Target)
                    {
                        item.Dispose();
                    }
                    _r3Target.Clear();
                    _mapping.Clear();
                }).AddTo(_disposables);

            // Subscribe to Set (indexer) actions - rebuild target list
            _darSource.ObserveSet(
                null,
                (_) =>
                {
                    RebuildTargetList();
                }).AddTo(_disposables);

            // Subscribe to Sort actions - rebuild target list
            _darSource.ObserveSort(
                null,
                RebuildTargetList).AddTo(_disposables);

            // Sync existing values
            foreach (var item in _darSource)
            {
                var targetItem = _selector(item);
                _mapping[item] = targetItem;
                _r3Target.Add(targetItem);
            }
        }

        private void RebuildTargetList()
        {
            // Dispose items that are no longer in source
            var itemsToRemove = new List<TSource>();
            foreach (var kvp in _mapping)
            {
                if (!_darSource.Contains(kvp.Key))
                {
                    kvp.Value.Dispose();
                    itemsToRemove.Add(kvp.Key);
                }
            }
            foreach (var item in itemsToRemove)
            {
                _mapping.Remove(item);
            }

            // Rebuild target list to match source order
            _r3Target.Clear();
            foreach (var sourceItem in _darSource)
            {
                if (!_mapping.TryGetValue(sourceItem, out var targetItem))
                {
                    targetItem = _selector(sourceItem);
                    _mapping[sourceItem] = targetItem;
                }
                _r3Target.Add(targetItem);
            }
        }

        public void Dispose()
        {
            foreach (var item in _r3Target)
            {
                item.Dispose();
            }
            _r3Target.Clear();
            _mapping.Clear();
            _disposables?.Dispose();
        }
    }

    /// <summary>
    /// Operator that synchronizes a DAR ObservableList to an R3 ObservableList, filtering by type.
    /// Only items of type TSource (derived from TBase) are synced.
    /// </summary>
    public class DARToR3SyncOperator<TBase, TSource, TTarget> : IDisposable 
        where TSource : TBase 
        where TTarget : IDisposable
    {
        private readonly ObservableAttributeList<TBase> _darSource;
        private readonly ObservableList<TTarget> _r3Target;
        private readonly Func<TSource, TTarget> _selector;
        private readonly Dictionary<TSource, TTarget> _mapping = new();
        private readonly CompositeDisposable _disposables = new();

        public DARToR3SyncOperator(
            ObservableAttributeList<TBase> darSource, 
            ObservableList<TTarget> r3Target, 
            Func<TSource, TTarget> selector)
        {
            _darSource = darSource;
            _r3Target = r3Target;
            _selector = selector;

            _darSource.ObserveAdd(null, _ => RebuildTargetList()).AddTo(_disposables);
            _darSource.ObserveRemove(null, _ => RebuildTargetList()).AddTo(_disposables);
            _darSource.ObserveInsert(null, _ => RebuildTargetList()).AddTo(_disposables);
            _darSource.ObserveClear(null, () =>
            {
                foreach (var item in _r3Target)
                {
                    item.Dispose();
                }
                _r3Target.Clear();
                _mapping.Clear();
            }).AddTo(_disposables);
            _darSource.ObserveSet(null, _ => RebuildTargetList()).AddTo(_disposables);
            _darSource.ObserveSort(null, RebuildTargetList).AddTo(_disposables);

            // Sync existing values (filtered by type)
            foreach (var item in _darSource)
            {
                if (item is TSource typedItem)
                {
                    var targetItem = _selector(typedItem);
                    _mapping[typedItem] = targetItem;
                    _r3Target.Add(targetItem);
                }
            }
        }

        private void RebuildTargetList()
        {
            // Dispose items that are no longer in source
            var itemsToRemove = new List<TSource>();
            foreach (var kvp in _mapping)
            {
                if (!_darSource.Contains(kvp.Key))
                {
                    kvp.Value.Dispose();
                    itemsToRemove.Add(kvp.Key);
                }
            }
            foreach (var item in itemsToRemove)
            {
                _mapping.Remove(item);
            }

            // Rebuild target list to match source order (filtered by type)
            _r3Target.Clear();
            foreach (var baseItem in _darSource)
            {
                if (baseItem is TSource sourceItem)
                {
                    if (!_mapping.TryGetValue(sourceItem, out var targetItem))
                    {
                        targetItem = _selector(sourceItem);
                        _mapping[sourceItem] = targetItem;
                    }
                    _r3Target.Add(targetItem);
                }
            }
        }

        public void Dispose()
        {
            foreach (var item in _r3Target)
            {
                item.Dispose();
            }
            _r3Target.Clear();
            _mapping.Clear();
            _disposables?.Dispose();
        }
    }
}
