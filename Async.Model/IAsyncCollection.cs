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

    public interface IAsyncCollection<T> : IReadOnlyCollection<T>
    {
        CollectionStatus Status { get; }
        bool IsComplete { get; }

        event EventHandler<IEnumerable<T>> CollectionChanged;
        event EventHandler<CollectionStatus> StatusChanged;

        AggregateException Exception { get; }
        Exception InnerException { get; }
        string ErrorMessage { get; }
    }
}
