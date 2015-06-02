using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public abstract class AsyncLoaderBase<TLoadResult> : IAsyncLoaded
    {
        protected readonly TaskScheduler eventScheduler;
        protected readonly CancellationTokenSource masterCancellationSource;

        /// <summary>
        /// A lock that can be taken both synchronously and asynchronously. This lock should always be used when accessing mutable fields.
        /// </summary>
        /// <remarks>An AsyncLock is NOT reentrant, so subclasses must be careful to only take it when it is known to not already be held.</remarks>
        protected readonly AsyncLock mutex = new AsyncLock();

        /// <summary>
        /// The current status of any asynchronous load or update operation currently running.
        /// </summary>
        /// <remarks>This field must ONLY be accessed whilst holding the mutex lock!</remarks>
        private AsyncStatus status = AsyncStatus.Ready;

        /// <summary>
        /// If the last asynchronous load or update operation failed, this holds the aggregate exception from the task.
        /// </summary>
        private AggregateException exception;

        protected AsyncLoaderBase(CancellationToken rootCancellationToken) : this(null, rootCancellationToken) { }

        protected AsyncLoaderBase(TaskScheduler eventScheduler, CancellationToken rootCancellationToken)
        {
            if (eventScheduler == null)
            {
                eventScheduler = (SynchronizationContext.Current == null) ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
            }

            this.eventScheduler = eventScheduler;
            this.masterCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(rootCancellationToken);
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
                    return exception;
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
            masterCancellationSource.Cancel();
        }

        protected void PerformAsyncOperation<TResult>(Func<CancellationToken, Task<TResult>> asyncOperation, Func<TResult, CancellationToken, TLoadResult> processResult)
        {
            AsyncStatus oldStatus;
            using (mutex.Lock())
            {
                // TODO: Should we fail instead of doing nothing? If we fail, it means client code MUST avoid race
                // conditions, where multiple operations could be attempted at once. If we do nothing, client code may
                // think an operation has been started, when it has not. 
                if (status == AsyncStatus.Loading)
                    return;

                oldStatus = status;
                status = AsyncStatus.Loading;
            }
            NotifyOperationStarted(oldStatus);

            var cancellationToken = masterCancellationSource.Token;
            var operationTask = asyncOperation(cancellationToken);

            var processDataTask = operationTask.ContinueWith(task => ProcessResultAndUpdateStatus(task, processResult), cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);


            // Handle errors for the load and insert tasks

            operationTask.ContinueWith(TaskFailedOrCancelled, CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            processDataTask.ContinueWith(TaskFailedOrCancelled, CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
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
            Debug.Assert(operationTask.Status == TaskStatus.RanToCompletion);

            var cancellationToken = masterCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();

            AsyncStatus oldStatus;
            TLoadResult notificationData;

            using (mutex.Lock())
            {
                notificationData = processResult(operationTask.Result, cancellationToken);

                oldStatus = status;
                Debug.Assert(oldStatus == AsyncStatus.Loading);

                status = AsyncStatus.Ready;
            }

            NotifyOperationCompleted(notificationData);
        }

        private void TaskFailedOrCancelled(Task previous)
        {
            AsyncStatus oldStatus;
            AsyncStatus newStatus;
            AggregateException locExc;

            using (mutex.Lock())
            {
                oldStatus = status;
                locExc = previous.Exception;
                newStatus = previous.IsCanceled ? AsyncStatus.Cancelled : AsyncStatus.Failed;

                exception = locExc;
                status = newStatus;
            }

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
            });
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
