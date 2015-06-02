using System;

namespace Async.Model.AsyncLoaded
{
    public enum AsyncStatus
    {
        // Constants partially ordered, so everything from Ready represent completed states.
        Loading,
        Ready,
        Failed,
        Cancelled
    }

    public struct AsyncStatusTransition
    {
        public readonly AsyncStatus oldStatus;
        public readonly AsyncStatus newStatus;

        public AsyncStatusTransition(AsyncStatus oldStatus, AsyncStatus newStatus)
        {
            this.oldStatus = oldStatus;
            this.newStatus = newStatus;
        }
    }

    public interface IAsyncLoaded
    {
        AsyncStatus Status { get; }

        AggregateException Exception { get; }
        Exception InnerException { get; }
        string ErrorMessage { get; }

        event EventHandler<AsyncStatusTransition> StatusChanged;
        event EventHandler<Exception> AsyncOperationFailed;
    }
}
