using Async.Model.AsyncLoaded;
using System;
using System.Collections.Generic;

namespace Async.Model
{
    // NOTE: In order to make interface covariant in T, we must use an event handler delegate that is
    // contravariant in T. EventHandler<T> is NOT variant in T, so we must use a custom delegate.
    public delegate void CollectionChangedHandler<in T>(object sender, IEnumerable<IItemChange<T>> changes);

    public interface IAsyncCollection<out T> : IEnumerable<T>, IAsyncLoaded
    {
        event CollectionChangedHandler<T> CollectionChanged;
    }
}
