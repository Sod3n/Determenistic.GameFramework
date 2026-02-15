using System;
using R3;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Client.Extensions
{
    /// <summary>
    /// Extensions to bridge ObservableAttribute to R3 reactive types.
    /// </summary>
    public static class ObservableAttributeExtensions
    {
        /// <summary>
        /// Converts an <see cref="ObservableAttribute{T}"/> to an R3 <see cref="Observable{T}"/>.
        /// Unsubscribing from the returned observable removes the callback from the source.
        /// </summary>
        public static Observable<T> ToObservable<T>(this ObservableAttribute<T> source)
        {
            return Observable.Create<T>(observer =>
            {
                return source.Observe(null, value => observer.OnNext(value), fireImmediately: true);
            });
        }

        /// <summary>
        /// Converts an <see cref="ObservableAttribute{T}"/> to an R3 <see cref="ReadOnlyReactiveProperty{T}"/>.
        /// The returned property reflects the current value and updates whenever the source changes.
        /// Dispose the returned property to unsubscribe from the source.
        /// </summary>
        public static ReadOnlyReactiveProperty<T> ToReadOnlyReactiveProperty<T>(
            this ObservableAttribute<T> source)
        {
            return source.ToObservable().ToReadOnlyReactiveProperty(source.Value);
        }
    }
}
