using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public abstract class AsyncLoaderBase<TLoadResult> : IAsyncLoaded
    {
        /// <summary>The scheduler used for raising events. This ultimately decides which thread the event notification is run on.</summary>
        protected readonly TaskScheduler eventScheduler;

        /// <summary>The root cancellation token this loader was initialized with. Allows for grouped cancellation.</summary>
        protected readonly CancellationToken rootCancellationToken;

        /// <summary>A lock that can be taken both synchronously and asynchronously. This lock should always be used when accessing mutable fields.</summary>
        /// <remarks>An AsyncLock is NOT reentrant, so subclasses must be careful to only take it when it is known to not already be held.</remarks>
        protected readonly AsyncLock mutex = new AsyncLock();

        /// <summary>The current status of any asynchronous load or update operation currently running.</summary>
        /// <remarks>This field must ONLY be accessed whilst holding the mutex lock!</remarks>
        private AsyncStatus status = AsyncStatus.Ready;

        /// <summary>Cancellation token source for the currently executing operation. Null when no operation in progress.</summary>
        /// <remarks>This field must ONLY be accessed whilst holding the mutex lock!</remarks>
        private CancellationTokenSource currentOperationCancelSource;

        /// <summary>The last started operation. The operation could currently be running or it might have already completed.</summary>
        /// <remarks>This field must ONLY be accessed whilst holding the mutex lock!</remarks>
        private TaskCompletionSource lastStartedOperation;

        protected AsyncLoaderBase(CancellationToken rootCancellationToken) : this(null, rootCancellationToken) { }

        protected AsyncLoaderBase(TaskScheduler eventScheduler, CancellationToken rootCancellationToken)
        {
            if (eventScheduler == null)
            {
                eventScheduler = (SynchronizationContext.Current == null) ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
            }

            this.eventScheduler = eventScheduler;
            this.rootCancellationToken = rootCancellationToken;
        }

        public AsyncStatus Status
        {
            get
            {
                using (mutex.Lock())
                {
                    return status;
                }
            }
        }

        public AggregateException Exception
        {
            get
            {
                using (mutex.Lock())
                {
                    var operation = lastStartedOperation;
                    return (operation == null) ? null : operation.Task.Exception;
                }
            }
        }

        public Exception InnerException
        {
            get
            {
                var agg = Exception;
                return (agg == null) ? null : agg.InnerException;
            }
        }

        public string ErrorMessage
        {
            get
            {
                var e = InnerException;
                return (e == null) ? null : e.Message;
            }
        }

        public event EventHandler<AsyncStatusTransition> StatusChanged;

        public event EventHandler<Exception> AsyncOperationFailed;

        protected event EventHandler<TLoadResult> AsyncOperationCompleted;

        public void Cancel()
        {
            lock (mutex)
            {
                if (currentOperationCancelSource != null)
                    currentOperationCancelSource.Cancel();
            }
        }

        protected Task PerformAsyncOperation<TResult>(Func<CancellationToken, Task<TResult>> asyncOperation, Func<TResult, CancellationToken, TLoadResult> processResult)
        {
            AsyncStatus oldStatus;
            TaskCompletionSource overallOperation;
            CancellationToken cancellationToken;

            using (mutex.Lock())
            {
                // TODO: Should we fail instead of doing nothing? If we fail, it means client code MUST avoid race
                // conditions, where multiple operations could be attempted at once. If we do nothing, client code may
                // think an operation has been started, when it has not. Alternatively, we could actually "queue up"
                // the call by taking advantage of Task.ContinueWith and our new overallOperation task.
                if (status == AsyncStatus.Loading)
                    return TaskConstants.Completed;

                oldStatus = status;

                status = AsyncStatus.Loading;
                lastStartedOperation = overallOperation = new TaskCompletionSource();
                currentOperationCancelSource = CancellationTokenSource.CreateLinkedTokenSource(rootCancellationToken);

                cancellationToken = currentOperationCancelSource.Token;
            }

            NotifyOperationStarted(oldStatus);

            // Start the async operation
            // NOTE: It is up to the operation to ensure that it is truly asynchronous using async IO or Task.Run as necessary
            var operationTask = asyncOperation(cancellationToken);

            // NOTE: We cannot use OnlyOnRanToCompletion here, since this task will then be cancelled upon failure or
            // cancellation of operationTask, triggering TaskFailedOrCancelled _for this task_ in addition of triggering
            // TaskFailedOrCancelled for the original task.
            var processDataTask = operationTask.ContinueWith(task => ProcessResultAndUpdateStatus(task, processResult), cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);


            // Handle errors for the load and insert tasks

            operationTask.ContinueWith(TaskFailedOrCancelled, CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            processDataTask.ContinueWith(TaskFailedOrCancelled, CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return overallOperation.Task;
        }

        protected void NotifySpecialOperationCompleted(TLoadResult result)
        {
            var operationCompletedHandler = AsyncOperationCompleted;

            if (operationCompletedHandler == null)
                return;

            // Perform notification on event scheduler
            Task.Factory.StartNew(() =>
            {
                if (operationCompletedHandler != null)
                    operationCompletedHandler(this, result);
            }, CancellationToken.None, TaskCreationOptions.None, eventScheduler);
        }

        private void ProcessResultAndUpdateStatus<TResult>(Task<TResult> operationTask, Func<TResult, CancellationToken, TLoadResult> processResult)
        {
            Debug.Assert(operationTask.IsCompleted);

            // If the task was cancelled or failed, abort processing
            if (operationTask.Status != TaskStatus.RanToCompletion)
                return;

            TLoadResult notificationData;
            AsyncStatus oldStatus;

            using (mutex.Lock())
            {
                currentOperationCancelSource.Token.ThrowIfCancellationRequested();

                notificationData = processResult(operationTask.Result, currentOperationCancelSource.Token);

                oldStatus = status;
                Debug.Assert(oldStatus == AsyncStatus.Loading);

                status = AsyncStatus.Ready;
                lastStartedOperation.SetResult();

                // Perform cleanup
                currentOperationCancelSource.Dispose();
                currentOperationCancelSource = null;
            }

            // Report result
            NotifyOperationCompleted(notificationData);
        }

        private void TaskFailedOrCancelled(Task previous)
        {
            Debug.Assert(previous.IsFaulted || previous.IsCanceled);

            AsyncStatus oldStatus;
            AsyncStatus newStatus;
            AggregateException locExc;

            using (mutex.Lock())
            {
                oldStatus = status;
                locExc = previous.Exception;
                newStatus = previous.IsCanceled ? AsyncStatus.Cancelled : AsyncStatus.Failed;

                status = newStatus;

                // Need to transition lastStartedOperation inside lock to ensure consistency between Status and Exception/InnerException/Message properties
                if (newStatus == AsyncStatus.Cancelled)
                {
                    // FIXME: This will not propagate the correct token, will that be a problem?
                    // See: https://github.com/dotnet/roslyn/issues/447
                    // TODO: Switch to TaskCompletionSource<>.SetCanceled(CancellationToken) when we upgrade to .NET 4.6
                    lastStartedOperation.SetCanceled();
                }
                else
                {
                    lastStartedOperation.SetException(locExc.InnerExceptions);
                }

                // Perform cleanup
                currentOperationCancelSource.Dispose();
                currentOperationCancelSource = null;
            }

            // Report result
            NotifyOperationFailedOrCancelled(oldStatus, newStatus, locExc);
        }

        private void NotifyOperationStarted(AsyncStatus oldStatus)
        {
            // Contract
            Debug.Assert(oldStatus != AsyncStatus.Loading);

            var statusChangeHandler = StatusChanged;
            if (statusChangeHandler == null)
                return;

            // Perform notification on event scheduler
            // NOTE: The compiler transforms the lambda to an inner class with a field for "this", so using "this"
            // inside the lambda is fine and will refer to this class as expected
            // See: http://stackoverflow.com/questions/11103745/c-sharp-lambdas-and-this-variable-scope
            Task.Factory.StartNew(() =>
            {
                statusChangeHandler(this, new AsyncStatusTransition(oldStatus, AsyncStatus.Loading));
            }, CancellationToken.None, TaskCreationOptions.None, eventScheduler);
        }

        private void NotifyOperationCompleted(TLoadResult notifData)
        {
            var statusChangeHandler = StatusChanged;
            var operationCompletedHandler = AsyncOperationCompleted;

            if (statusChangeHandler == null && operationCompletedHandler == null)
                return;

            // Perform notification on event scheduler
            Task.Factory.StartNew(() =>
            {
                if (statusChangeHandler != null)
                    statusChangeHandler(this, new AsyncStatusTransition(AsyncStatus.Loading, AsyncStatus.Ready));

                if (operationCompletedHandler != null)
                    operationCompletedHandler(this, notifData);
            }, CancellationToken.None, TaskCreationOptions.None, eventScheduler);
        }

        private void NotifyOperationFailedOrCancelled(AsyncStatus oldStatus, AsyncStatus newStatus, AggregateException exception)
        {
            // Contract
            Debug.Assert(newStatus == AsyncStatus.Cancelled || newStatus == AsyncStatus.Failed);

            var statusChangeHandler = StatusChanged;
            var operationFailedHandler = AsyncOperationFailed;

            if (statusChangeHandler == null && (operationFailedHandler == null || exception == null))
                return;

            // Perform notification on event scheduler
            Task.Factory.StartNew(() =>
            {
                if (statusChangeHandler != null)
                    statusChangeHandler(this, new AsyncStatusTransition(oldStatus, newStatus));

                if (operationFailedHandler == null || exception == null)
                    return;

                // Normalize in case of nested aggregate exceptions
                exception = exception.Flatten();

                // Fire event for each exception
                foreach (var exc in exception.InnerExceptions)
                    operationFailedHandler(this, exc);
            }, CancellationToken.None, TaskCreationOptions.None, eventScheduler);
        }
    }
}
