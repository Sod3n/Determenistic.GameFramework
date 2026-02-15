using System;
using R3;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Client.Operators;
using ObservableCollections;

namespace Deterministic.GameFramework.Client.Extensions
{
    /// <summary>
    /// Extensions to bridge ObservableAttributeList events to R3 Observables.
    /// </summary>
    public static class ObservableAttributeListExtensions
    {
        /// <summary>
        /// Returns an R3 Observable that fires whenever an item is added to the list.
        /// </summary>
        public static Observable<ListAddEventArgs<T>> ObserveAddAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<ListAddEventArgs<T>>(o =>
            {
                return source.ObserveAdd(null, e => o.OnNext(e));
            });
        }

        /// <summary>
        /// Returns an R3 Observable that fires whenever an item is removed from the list.
        /// </summary>
        public static Observable<ListRemoveEventArgs<T>> ObserveRemoveAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<ListRemoveEventArgs<T>>(o =>
            {
                return source.ObserveRemove(null, e => o.OnNext(e));
            });
        }

        /// <summary>
        /// Returns an R3 Observable that fires before an item is removed from the list.
        /// </summary>
        public static Observable<ListRemoveEventArgs<T>> ObserveBeforeRemoveAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<ListRemoveEventArgs<T>>(o =>
            {
                return source.ObserveBeforeRemove(null, e => o.OnNext(e));
            });
        }

        /// <summary>
        /// Returns an R3 Observable that fires whenever an item is inserted into the list.
        /// </summary>
        public static Observable<ListAddEventArgs<T>> ObserveInsertAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<ListAddEventArgs<T>>(o =>
            {
                return source.ObserveInsert(null, e => o.OnNext(e));
            });
        }

        /// <summary>
        /// Returns an R3 Observable that fires whenever an item is replaced via indexer.
        /// </summary>
        public static Observable<ListSetEventArgs<T>> ObserveSetAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<ListSetEventArgs<T>>(o =>
            {
                return source.ObserveSet(null, e => o.OnNext(e));
            });
        }

        /// <summary>
        /// Returns an R3 Observable that fires whenever the list is cleared.
        /// </summary>
        public static Observable<Unit> ObserveClearAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<Unit>(o =>
            {
                return source.ObserveClear(null, () => o.OnNext(Unit.Default));
            });
        }

        /// <summary>
        /// Returns an R3 Observable that fires before the list is cleared.
        /// </summary>
        public static Observable<Unit> ObserveBeforeClearAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<Unit>(o =>
            {
                return source.ObserveBeforeClear(null, () => o.OnNext(Unit.Default));
            });
        }

        /// <summary>
        /// Returns an R3 Observable that fires whenever the list is sorted.
        /// </summary>
        public static Observable<Unit> ObserveSortAsObservable<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<Unit>(o =>
            {
                return source.ObserveSort(null, () => o.OnNext(Unit.Default));
            });
        }

        /// <summary>
        /// Returns a <see cref="ReadOnlyReactiveProperty{T}"/> that tracks the count of the list.
        /// Updates on add, remove, insert, and clear.
        /// </summary>
        public static ReadOnlyReactiveProperty<int> ObserveCountAsReactiveProperty<T>(
            this ObservableAttributeList<T> source)
        {
            return Observable.Create<int>(o =>
            {
                o.OnNext(source.Count);
                var d1 = source.ObserveAdd(null, _ => o.OnNext(source.Count));
                var d2 = source.ObserveRemove(null, _ => o.OnNext(source.Count));
                var d3 = source.ObserveInsert(null, _ => o.OnNext(source.Count));
                var d4 = source.ObserveClear(null, () => o.OnNext(source.Count));
                return Disposable.Combine(d1, d2, d3, d4);
            }).ToReadOnlyReactiveProperty(source.Count);
        }

        /// <summary>
        /// Synchronizes a DAR ObservableAttributeList to an R3 ObservableList, applying a transformation function.
        /// Useful for creating ViewModels from DAR Models.
        /// Dispose the returned operator to stop synchronization and clean up all created items.
        /// </summary>
        public static DARToR3SyncOperator<T1, T2> SyncToR3<T1, T2>(
            this ObservableAttributeList<T1> source,
            ObservableList<T2> target,
            Func<T1, T2> selector) where T2 : IDisposable
        {
            return new DARToR3SyncOperator<T1, T2>(source, target, selector);
        }

        /// <summary>
        /// Synchronizes a DAR ObservableAttributeList to an R3 ObservableList without transformation.
        /// Dispose the returned operator to stop synchronization and clean up all items.
        /// </summary>
        public static DARToR3SyncOperator<T, T> SyncToR3<T>(
            this ObservableAttributeList<T> source,
            ObservableList<T> target) where T : IDisposable
        {
            return new DARToR3SyncOperator<T, T>(source, target, x => x);
        }

        /// <summary>
        /// Creates a builder for type-filtered list synchronization.
        /// Call <c>.SyncToR3(target, selector)</c> on the returned builder to complete the sync setup.
        /// </summary>
        public static FilteredObservableListBuilder<TBase, TFiltered> OfTypeObs<TBase, TFiltered>(
            this ObservableAttributeList<TBase> source)
            where TFiltered : TBase
        {
            return new FilteredObservableListBuilder<TBase, TFiltered>(source);
        }
    }

    /// <summary>
    /// Builder for filtered observable list synchronization.
    /// </summary>
    public class FilteredObservableListBuilder<TBase, TFiltered> where TFiltered : TBase
    {
        private readonly ObservableAttributeList<TBase> _source;

        public FilteredObservableListBuilder(ObservableAttributeList<TBase> source)
        {
            _source = source;
        }

        /// <summary>
        /// Synchronizes to an R3 ObservableList, applying a transformation function.
        /// Only items of type <typeparamref name="TFiltered"/> are included.
        /// </summary>
        public DARToR3SyncOperator<TBase, TFiltered, TTarget> SyncToR3<TTarget>(
            ObservableList<TTarget> target,
            Func<TFiltered, TTarget> selector)
            where TTarget : IDisposable
        {
            return new DARToR3SyncOperator<TBase, TFiltered, TTarget>(_source, target, selector);
        }
    }
}
