﻿using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public abstract class AsyncLoaderBase<TLoadResult> : IAsyncLoaded
    {
        /// <summary>The <see cref="SynchronizationContext"/> used to post event notifications on.</summary>
        protected readonly SynchronizationContext eventContext;

        /// <summary>The root cancellation token this loader was initialized with. Allows for grouped cancellation.</summary>
        protected readonly CancellationToken rootCancellationToken;

        /// <summary>A lock that must be held while accessing mutable fields.</summary>
        protected readonly object mutex = new object();

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

        protected AsyncLoaderBase(SynchronizationContext eventContext, CancellationToken rootCancellationToken)
        {
            this.eventContext = eventContext ?? SynchronizationContext.Current ?? new SynchronizationContext();
            this.rootCancellationToken = rootCancellationToken;
        }

        public AsyncStatus Status
        {
            get
            {
                lock (mutex)
                {
                    return status;
                }
            }
        }

        public AggregateException Exception
        {
            get
            {
                lock (mutex)
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

        /// <summary>
        /// Starts an asynchronous operation if one is not already running and returns a task representing its
        /// progress. This is the main method subclasses are expected to expose in their own specific ways to clients.
        /// This method takes care of all the mechanics in regards to updating the status and notifying listeners.
        /// </summary>
        /// <typeparam name="TResult">The result type of the operation.</typeparam>
        /// <param name="prepareOperation">
        /// An action performed under lock after it has been decided that the operation should proceed (no async
        /// operation currently in progress).
        /// </param>
        /// <param name="asyncOperation">
        /// The asynchronous operation to perform. It is up to the operation to ensure asynchronous behaviour: this
        /// method will not attempt to force execution on another thread. This operation is NOT performed under lock.
        /// </param>
        /// <param name="processResult">
        /// A function to perform on the result of the asynchronous operation, unless the operation is cancelled or
        /// fails. Performed under lock.
        /// </param>
        /// <returns>
        /// A task that will complete when the overall operation including post-processing has completed. Any exception
        /// encountered by the operation will be propagated to the task and available via the Exception property. If
        /// the operation is cancelled, the task will reflect this in its Status property.
        /// </returns>
        protected Task PerformAsyncOperation<TResult>(
            Action prepareOperation,
            Func<CancellationToken, Task<TResult>> asyncOperation,
            Func<TResult, CancellationToken, TLoadResult> processResult)
        {

            // These all need to be read under lock but used outside lock
            AsyncStatus oldStatus;
            TaskCompletionSource overallOperation;
            CancellationToken cancellationToken;

            Debug.WriteLine("AsyncLoaderBase.PerformAsyncOperation: Taking mutex");
            lock (mutex)
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

                // Prepare for operation inside lock _after_ it has been determined that operation should proceed
                prepareOperation();
            }
            Debug.WriteLine("AsyncLoaderBase.PerformAsyncOperation: Released mutex");

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
            // Delegate to private method, so we can group implementation code together but also group API methods together
            NotifySpecialOperationCompletedCore(result);
        }

        #region Continuations
        private void ProcessResultAndUpdateStatus<TResult>(Task<TResult> operationTask, Func<TResult, CancellationToken, TLoadResult> processResult)
        {
            Debug.Assert(operationTask.IsCompleted);

            // If the task was cancelled or failed, abort processing
            if (operationTask.Status != TaskStatus.RanToCompletion)
                return;

            TLoadResult notificationData;

            Debug.WriteLine("AsyncLoaderBase.ProcessResultAndUpdateStatus: Taking mutex");
            lock (mutex)
            {
                currentOperationCancelSource.Token.ThrowIfCancellationRequested();

                notificationData = processResult(operationTask.Result, currentOperationCancelSource.Token);

                // Perform cleanup
                currentOperationCancelSource.Dispose();
                currentOperationCancelSource = null;
            }
            Debug.WriteLine("AsyncLoaderBase.ProcessResultAndUpdateStatus: Released mutex");

            // Report result
            NotifyOperationCompleted(notificationData);

            // Now update status and complete task
            // NOTE: We wait until after notifications in order to simplify tests - if an event handler throws an
            // exception, it will be reflected in the task status
            Debug.WriteLine("AsyncLoaderBase.ProcessResultAndUpdateStatus: Taking mutex");
            lock (mutex)
            {
                Debug.Assert(status == AsyncStatus.Loading);

                status = AsyncStatus.Ready;
                lastStartedOperation.SetResult();
            }
            Debug.WriteLine("AsyncLoaderBase.ProcessResultAndUpdateStatus: Released mutex");
        }

        private void TaskFailedOrCancelled(Task previous)
        {
            Debug.Assert(previous.IsFaulted || previous.IsCanceled);

            AsyncStatus oldStatus;
            AsyncStatus newStatus;
            AggregateException locExc;

            Debug.WriteLine("AsyncLoaderBase.TaskFailedOrCancelled: Taking mutex");
            lock (mutex)
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
            Debug.WriteLine("AsyncLoaderBase.TaskFailedOrCancelled: Released mutex");

            // Report result
            NotifyOperationFailedOrCancelled(oldStatus, newStatus, locExc);
        }
        #endregion Continuations

        #region Notifications
        private void NotifyOperationStarted(AsyncStatus oldStatus)
        {
            // Contract
            Debug.Assert(oldStatus != AsyncStatus.Loading);

            var statusChangeHandler = StatusChanged;
            if (statusChangeHandler == null)
                return;

            // Post notification to event context
            // NOTE: The compiler transforms the lambda to an inner class with a field for "this", so using "this"
            // inside the lambda is fine and will refer to this class instance as expected
            // See: http://stackoverflow.com/questions/11103745/c-sharp-lambdas-and-this-variable-scope
            eventContext.Post(dummyState =>
            {
                statusChangeHandler(this, new AsyncStatusTransition(oldStatus, AsyncStatus.Loading));
            }, null);
        }

        private void NotifySpecialOperationCompletedCore(TLoadResult notifData)
        {
            var operationCompletedHandler = AsyncOperationCompleted;

            if (operationCompletedHandler == null)
                return;

            // Post notification to event context
            eventContext.Post(dummyState =>
            {
                operationCompletedHandler(this, notifData);
            }, null);
        }

        private void NotifyOperationCompleted(TLoadResult notifData)
        {
            var statusChangeHandler = StatusChanged;
            var operationCompletedHandler = AsyncOperationCompleted;

            if (statusChangeHandler == null && operationCompletedHandler == null)
                return;

            // Post notifications to event context
            eventContext.Post(dummyState =>
            {
                if (statusChangeHandler != null)
                    statusChangeHandler(this, new AsyncStatusTransition(AsyncStatus.Loading, AsyncStatus.Ready));

                if (operationCompletedHandler != null)
                    operationCompletedHandler(this, notifData);
            }, null);
        }

        private void NotifyOperationFailedOrCancelled(AsyncStatus oldStatus, AsyncStatus newStatus, AggregateException exception)
        {
            // Contract
            Debug.Assert(newStatus == AsyncStatus.Cancelled || newStatus == AsyncStatus.Failed);

            var statusChangeHandler = StatusChanged;
            var operationFailedHandler = AsyncOperationFailed;

            // Early return if we have no work to be done
            if (statusChangeHandler == null && (operationFailedHandler == null || exception == null))
                return;

            // Post notifications to event context
            eventContext.Post(state =>
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
            }, null);
        }
        #endregion Notifications
    }
}
