using System;

namespace Async.Model.AsyncLoaded
{
    // NOTE: In order to make interface covariant in T, we must use an event handler delegate that is
    // contravariant in T. EventHandler<T> is NOT variant in T, so we must use a custom delegate.
    public delegate void ItemChangedHandler<in T>(object sender, T item);

    public interface IAsyncItem<out T> : IAsyncLoaded
    {
        T Item { get; }

        event ItemChangedHandler<T> ItemChanged;
    }
}
