using System;
using System.Collections.Generic;

namespace Async.Model
{
    public enum CollectionStatus
    {
        // Constants partially ordered, so everything from Ready represent completed states.
        Loading,
        Updating,
        Ready,
        LoadFailed,
        UpdateFailed,
        Cancelled
    }

    public delegate void CollectionResetHandler(object sender);

    // NOTE: In order to make IAsyncCollection covariant in T, we must use an event handler delegate that is
    // contravariant in T. EventHandler<T> is NOT variant in T, so we must use a custom delegate.
    public delegate void CollectionChangedHandler<in T>(object sender, IEnumerable<IItemChange<T>> changes);

    public interface IAsyncCollection<out T> : IEnumerable<T>
    {
        CollectionStatus Status { get; }
        bool IsComplete { get; }

        event CollectionResetHandler CollectionReset;
        event CollectionChangedHandler<T> CollectionChanged;
        event EventHandler<CollectionStatus> StatusChanged;

        AggregateException Exception { get; }
        Exception InnerException { get; }
        string ErrorMessage { get; }
    }
}
