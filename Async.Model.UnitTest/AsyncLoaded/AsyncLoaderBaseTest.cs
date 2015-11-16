using Async.Model.AsyncLoaded;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using Async.Model.Context;

namespace Async.Model.UnitTest.AsyncLoaded
{
    [TestFixture]
    public class AsyncLoaderBaseTest
    {
        #region Execution flow
        [Test]
        public void RunsOperation()
        {
            var operation = Substitute.For<Func<CancellationToken, Task<bool>>>();

            var asyncLoader = new AsyncLoaderTestImpl();
            asyncLoader.PerformAsyncOperation(operation);

            operation.Received().Invoke(Arg.Any<CancellationToken>());
        }

        [Test]
        public void RunsPostProcessStep()
        {
            var postProcess = Substitute.For<Func<bool, CancellationToken, bool>>();

            var asyncLoader = new AsyncLoaderTestImpl();
            // Here we check with a task that returns false
            asyncLoader.PerformAsyncOperation(tok => Task.FromResult(false), postProcess);

            postProcess.Received().Invoke(false, Arg.Any<CancellationToken>());
        }

        [Test]
        public void RunsPostProcessStepWithResultFromOperation()
        {
            var postProcess = Substitute.For<Func<bool, CancellationToken, bool>>();

            var asyncLoader = new AsyncLoaderTestImpl();
            // Now we check with a task that returns _true_
            asyncLoader.PerformAsyncOperation(tok => Task.FromResult(true), postProcess);

            postProcess.Received().Invoke(true, Arg.Any<CancellationToken>());
        }

        [Test]
        public void DoesNotRunPostProcessingStepIfOperationFails()
        {
            var postProcess = Substitute.For<Func<bool, CancellationToken, bool>>();
            postProcess.Invoke(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

            var asyncLoader = new AsyncLoaderTestImpl();
            asyncLoader.PerformAsyncOperation(token => FromException(new Exception()), postProcess);

            postProcess.DidNotReceive().Invoke(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void DoesNotRunPostProcessingIfOperationCancelled()
        {
            var postProcess = Substitute.For<Func<bool, CancellationToken, bool>>();
            postProcess.Invoke(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(true);

            var asyncLoader = new AsyncLoaderTestImpl();
            asyncLoader.PerformAsyncOperation(token => TaskConstants<bool>.Canceled, postProcess);

            postProcess.DidNotReceive().Invoke(Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }
        #endregion Execution flow

        #region Status property and StatusChanged event
        [Test]
        public void TransitionsStatusAsExpectedForOperationThatRunsToCompletion()
        {
            var longTask = new TaskCompletionSource<bool>();
            var statusHandler = Substitute.For<EventHandler<AsyncStatusTransition>>();

            // Have event notifications be executed inline on current thread
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.StatusChanged += statusHandler;

            Assert.That(asyncLoader.Status, Is.EqualTo(AsyncStatus.Ready));

            asyncLoader.PerformAsyncOperation(token => longTask.Task);
            Assert.That(asyncLoader.Status, Is.EqualTo(AsyncStatus.Loading));
            statusHandler.Received().Invoke(asyncLoader, new AsyncStatusTransition(AsyncStatus.Ready, AsyncStatus.Loading));

            longTask.SetResult(true);
            Assert.That(asyncLoader.Status, Is.EqualTo(AsyncStatus.Ready));
            statusHandler.Received().Invoke(asyncLoader, new AsyncStatusTransition(AsyncStatus.Loading, AsyncStatus.Ready));
        }

        [Test]
        public void TransitionsStatusToFailedForOperationThatFails()
        {
            var statusHandler = Substitute.For<EventHandler<AsyncStatusTransition>>();
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.StatusChanged += statusHandler;

            asyncLoader.PerformAsyncOperation(token => FromException(new Exception()));

            Assert.That(asyncLoader.Status, Is.EqualTo(AsyncStatus.Failed));
            statusHandler.Received().Invoke(asyncLoader, new AsyncStatusTransition(AsyncStatus.Ready, AsyncStatus.Loading));
            statusHandler.Received().Invoke(asyncLoader, new AsyncStatusTransition(AsyncStatus.Loading, AsyncStatus.Failed));
        }

        [Test]
        public void TransitionsStatusToCancelledForCancelledOperation()
        {
            var statusHandler = Substitute.For<EventHandler<AsyncStatusTransition>>();
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.StatusChanged += statusHandler;

            asyncLoader.PerformAsyncOperation(token => TaskConstants<bool>.Canceled);

            Assert.That(asyncLoader.Status, Is.EqualTo(AsyncStatus.Cancelled));
            statusHandler.Received().Invoke(asyncLoader, new AsyncStatusTransition(AsyncStatus.Ready, AsyncStatus.Loading));
            statusHandler.Received().Invoke(asyncLoader, new AsyncStatusTransition(AsyncStatus.Loading, AsyncStatus.Cancelled));
        }
        #endregion Status property and StatusChanged event

        #region Exception/InnerException/ErrorMessage properties and AsyncOperationFailed event
        [Test]
        public void DoesNotReportExceptionOnSuccess()
        {
            // Have event notifications be executed inline on current thread
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            var handler = Substitute.For<EventHandler<Exception>>();
            asyncLoader.AsyncOperationFailed += handler;

            asyncLoader.PerformAsyncOperation(token => Task.FromResult(true));

            Assert.That(asyncLoader.Exception, Is.Null);
            handler.DidNotReceive().Invoke(Arg.Any<object>(), Arg.Any<Exception>());
        }

        [Test]
        public void DoesNotReportExceptionOnCancel()
        {
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            var handler = Substitute.For<EventHandler<Exception>>();
            asyncLoader.AsyncOperationFailed += handler;

            asyncLoader.PerformAsyncOperation(token => TaskConstants<bool>.Canceled);

            Assert.That(asyncLoader.Exception, Is.Null);
            handler.DidNotReceive().Invoke(Arg.Any<object>(), Arg.Any<Exception>());
        }

        [Test]
        public void ReportsExceptionIfOperationFails()
        {
            var exception = new Exception();
            var handler = Substitute.For<EventHandler<Exception>>();

            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.AsyncOperationFailed += handler;

            asyncLoader.PerformAsyncOperation(token => FromException(exception));

            Assert.That(asyncLoader.Exception, Is.Not.Null);
            Assert.That(asyncLoader.InnerException, Is.EqualTo(exception));

            handler.Received(1).Invoke(asyncLoader, exception);
        }

        [Test]
        public void ReportsAllExceptionsFromFailedOperation()
        {
            var exc1 = new Exception();
            var exc2 = new Exception();
            var handler = Substitute.For<EventHandler<Exception>>();

            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.AsyncOperationFailed += handler;

            asyncLoader.PerformAsyncOperation(token => FromExceptions(exc1, exc2));

            handler.Received(1).Invoke(asyncLoader, exc1);
            handler.Received(1).Invoke(asyncLoader, exc2);
        }

        [Test]
        public void ClearsPreviousExceptionWhenNewOperationIsStarted()
        {
            var longTask = new TaskCompletionSource<bool>();
            var asyncLoader = new AsyncLoaderTestImpl();

            // First perform an operation that fails
            asyncLoader.PerformAsyncOperation(token => FromException(new Exception()));
            Assert.That(asyncLoader.Exception, Is.Not.Null);

            // Then start a new one that will not complete until we say so
            asyncLoader.PerformAsyncOperation(token => longTask.Task);
            Assert.That(asyncLoader.Exception, Is.Null);
        }
        #endregion Exception/InnerException/ErrorMessage properties and AsyncOperationFailed event

        #region AsyncOperationCompleted event
        [Test]
        public void ReportsAsyncOperationCompletedWhenOperationComplete()
        {
            var longTask = new TaskCompletionSource<bool>();
            var completedHandler = Substitute.For<EventHandler<bool>>();

            // Have event notifications be executed inline on current thread
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.AsyncOperationCompletedTunnel += completedHandler;

            // Here we check with a post process step that returns false
            asyncLoader.PerformAsyncOperation(token => longTask.Task, (b, c) => false);
            // Operation not complete yet
            completedHandler.DidNotReceive().Invoke(Arg.Any<object>(), Arg.Any<bool>());

            longTask.SetResult(true);
            completedHandler.Received().Invoke(asyncLoader, false);
        }

        [Test]
        public void ReportsAsyncOperationCompletedWithResultFromPostProcess()
        {
            var completedHandler = Substitute.For<EventHandler<bool>>();
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.AsyncOperationCompletedTunnel += completedHandler;

            // Now we check with a post process step that returns _true_
            asyncLoader.PerformAsyncOperation(token => Task.FromResult(false), (b, c) => true);

            completedHandler.Received().Invoke(asyncLoader, true);
        }

        [Test]
        public void ReportsAsyncOperationCompletedForSpecialOperations()
        {
            var completedHandler = Substitute.For<EventHandler<bool>>();
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.AsyncOperationCompletedTunnel += completedHandler;

            asyncLoader.NotifySpecialOperationCompletedTunnel(false);
            completedHandler.Received().Invoke(asyncLoader, false);

            asyncLoader.NotifySpecialOperationCompletedTunnel(true);
            completedHandler.Received().Invoke(asyncLoader, true);
        }

        [Test]
        public void DoesNotReportAsyncOperationCompletedWhenOperationFails()
        {
            var completedHandler = Substitute.For<EventHandler<bool>>();
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.AsyncOperationCompletedTunnel += completedHandler;

            asyncLoader.PerformAsyncOperation(token => FromException(new Exception()));

            completedHandler.DidNotReceive().Invoke(Arg.Any<object>(), Arg.Any<bool>());
        }

        [Test]
        public void DoesNotReportAsyncOperationCompletedWhenOperationCancelled()
        {
            var completedHandler = Substitute.For<EventHandler<bool>>();
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.AsyncOperationCompletedTunnel += completedHandler;

            asyncLoader.PerformAsyncOperation(token => TaskConstants<bool>.Canceled);

            completedHandler.DidNotReceive().Invoke(Arg.Any<object>(), Arg.Any<bool>());
        }
        #endregion AsyncOperationCompleted event

        #region Event notification thread
        [Test]
        public void NotifiesOfOperationStartOnCurrentThreadWhenUsingCurrentThreadScheduler()
        {
            var longTask = new TaskCompletionSource<bool>();

            // Since we use CurrentThreadTaskScheduler, event notifications should be run synchronously on current thread
            int expectedEventThreadId = GetCurrentThreadId();
            int actualEventThreadId = 0;

            // Have event notifications be executed inline on current thread
            var asyncLoader = new AsyncLoaderTestImpl(new RunInlineSynchronizationContext());
            asyncLoader.StatusChanged += (s, e) =>
            {
                // NOTE: Cannot perform asserts here, since the exceptions are eaten by the task scheduling mechanics.
                actualEventThreadId = GetCurrentThreadId();
            };

            // Start an operation that will not finish until we say so
            asyncLoader.PerformAsyncOperation(token => longTask.Task);

            Assert.That(actualEventThreadId, Is.EqualTo(expectedEventThreadId));
        }

        [Test]
        public void NotifiesOfOperationStartOnThreadPoolIfEventContextIsNullAndNoAmbientContext()
        {
            // Need to marshal the notification thread id back to main thread, we can use TCS for that
            var notificationResult = new TaskCompletionSource<int>();

            // Now create a loader that uses the thread pool for notifications
            var asyncLoader = new AsyncLoaderTestImpl(eventContext: null);
            asyncLoader.StatusChanged += (s, e) => { notificationResult.SetResult(GetCurrentThreadId()); };

            asyncLoader.PerformAsyncOperation(token => TaskConstants<bool>.Never);

            bool finished = notificationResult.Task.Wait(TimeSpan.FromSeconds(5));
            Assert.That(finished, Is.True);

            // Now the thread id must NOT equate the current thread id
            Assert.That(notificationResult.Task.Result, Is.Not.EqualTo(GetCurrentThreadId()));
        }

        [Test]
        public void NotifiesOfOperationCompletionOnThreadDecidedByEventContext()
        {
            // Need to marshal thread ids back to main thread, we can use TCS for that
            var schedulerThreadId = new TaskCompletionSource<int>();
            var notificationThreadId = new TaskCompletionSource<int>();

            // Create a task scheduler that uses a single thread that reports its id at start
            var scheduler = CreateSingleThreadTaskScheduler(threadInit: () => schedulerThreadId.SetResult(GetCurrentThreadId()));

            var asyncLoader = new AsyncLoaderTestImpl(new TaskSchedulerSynchronizationContext(scheduler));
            asyncLoader.StatusChanged += (s, e) => { notificationThreadId.SetResult(GetCurrentThreadId()); };

            asyncLoader.PerformAsyncOperation(token => Task.FromResult(true));  // A succeeded task

            bool finished = Task.WaitAll(new[] { schedulerThreadId.Task, notificationThreadId.Task }, TimeSpan.FromSeconds(5));
            Assert.That(finished, Is.True);

            // Sanity check
            Assert.That(schedulerThreadId.Task.Result, Is.Not.EqualTo(GetCurrentThreadId()));

            // Handlers must be notified on scheduler thread
            Assert.That(notificationThreadId.Task.Result, Is.EqualTo(schedulerThreadId.Task.Result));
        }

        [Test]
        public void NotifiesOfOperationFailedOnThreadDecidedByEventContext()
        {
            // Need to marshal thread ids back to main thread, we can use TCS for that
            var schedulerThreadId = new TaskCompletionSource<int>();
            var notificationThreadId = new TaskCompletionSource<int>();

            // Create a task scheduler that uses a single thread that reports its id at start
            var scheduler = CreateSingleThreadTaskScheduler(threadInit: () => schedulerThreadId.SetResult(GetCurrentThreadId()));

            var asyncLoader = new AsyncLoaderTestImpl(new TaskSchedulerSynchronizationContext(scheduler));
            asyncLoader.StatusChanged += (s, e) => { notificationThreadId.SetResult(GetCurrentThreadId()); };

            asyncLoader.PerformAsyncOperation(token => FromException(new Exception()));  // A failed task

            bool finished = Task.WaitAll(new[] { schedulerThreadId.Task, notificationThreadId.Task }, TimeSpan.FromSeconds(5));
            Assert.That(finished, Is.True);

            // Sanity check
            Assert.That(schedulerThreadId.Task.Result, Is.Not.EqualTo(GetCurrentThreadId()));

            // Handlers must be notified on scheduler thread
            Assert.That(notificationThreadId.Task.Result, Is.EqualTo(schedulerThreadId.Task.Result));
        }

        [Test]
        public void NotifiesOfSpecialOperationCompletedOnThreadDecidedByEventContext()
        {
            // Need to marshal thread ids back to main thread, we can use TCS for that
            var schedulerThreadId = new TaskCompletionSource<int>();
            var notificationThreadId = new TaskCompletionSource<int>();

            // Create a task scheduler that uses a single thread that reports its id at start
            var scheduler = CreateSingleThreadTaskScheduler(threadInit: () => schedulerThreadId.SetResult(GetCurrentThreadId()));

            var asyncLoader = new AsyncLoaderTestImpl(new TaskSchedulerSynchronizationContext(scheduler));
            asyncLoader.AsyncOperationCompletedTunnel += (s, e) => { notificationThreadId.SetResult(GetCurrentThreadId()); };

            asyncLoader.NotifySpecialOperationCompletedTunnel(true);

            bool finished = Task.WaitAll(new[] { schedulerThreadId.Task, notificationThreadId.Task }, TimeSpan.FromSeconds(5));
            Assert.That(finished, Is.True);

            // Sanity check
            Assert.That(schedulerThreadId.Task.Result, Is.Not.EqualTo(GetCurrentThreadId()));

            // Handlers must be notified on scheduler thread
            Assert.That(notificationThreadId.Task.Result, Is.EqualTo(schedulerThreadId.Task.Result));
        }
        #endregion Event notification thread

        #region Returned task
        [Test]
        public void ReturnsTaskThatOnlyCompletesWhenEntireOperationIsComplete()
        {
            using (var postProcessPause = new ManualResetEventSlim(false))
            using (var postProcessExecuting = new ManualResetEventSlim(false))
            {
                var longTask = new TaskCompletionSource<bool>();

                // Need to run this on another thread, since we intend to later block that thread during a callback
                Func<CancellationToken, Task<bool>> longAsyncOp =
                    token => Task.Run(async () => await longTask.Task);
                // This post process step will wait until handle is signalled
                Func<bool, CancellationToken, bool> processOp = (res, tok) =>
                {
                    postProcessExecuting.Set();  // signal that we have arrived at post process step
                    postProcessPause.Wait();
                    return true;
                };

                var asyncLoader = new AsyncLoaderTestImpl();
                var task = asyncLoader.PerformAsyncOperation(longAsyncOp, processOp);

                // Verify that returned task does not complete before async op is done
                Assert.That(task.IsCompleted, Is.False);

                // Complete async op, then wait for execution to reach post process step
                longTask.SetResult(true);
                postProcessExecuting.Wait();

                // Verify that the task does not complete before the post-process step is done
                Assert.That(task.IsCompleted, Is.False);

                // Signal the operation to proceed and finish
                postProcessPause.Set();

                bool finished = task.Wait(TimeSpan.FromSeconds(5));
                Assert.That(finished, Is.True);
            }
        }

        [Test]
        public void ReturnsTaskThatFailsIfOperationFails()
        {
            var exception = new Exception();

            var asyncLoader = new AsyncLoaderTestImpl();
            var task = asyncLoader.PerformAsyncOperation(token => FromException(exception));

            Assert.That(task.IsFaulted, Is.True);
            // Exception will always be wrapped in an AggregateException
            Assert.That(task.Exception.InnerException, Is.EqualTo(exception));
        }

        [Test]
        public void ReturnsTaskThatFailsIfPostProcessingFails()
        {
            var exception = new Exception();

            var asyncLoader = new AsyncLoaderTestImpl();
            var task = asyncLoader.PerformAsyncOperation(token => Task.FromResult(true), (res, tok) => { throw exception; });

            Assert.That(task.IsFaulted, Is.True);
            // Exception will always be wrapped in an AggregateException
            Assert.That(task.Exception.InnerException, Is.EqualTo(exception));
        }

        [Test]
        public void ReturnsCompletedTaskWhenBusy()
        {
            var longTask = new TaskCompletionSource<bool>();
            bool operationWasRunWhenBusy = false;

            var asyncLoader = new AsyncLoaderTestImpl();

            // Start an operation that will not finish until we say so
            asyncLoader.PerformAsyncOperation(token => longTask.Task);

            // Now try to start another operation
            // NOTE: It is important for the test that this task is guaranteed to not complete
            var task = asyncLoader.PerformAsyncOperation(token =>
            {
                operationWasRunWhenBusy = true;
                return TaskConstants<bool>.Never;
            });

            Assert.That(task.IsCompleted);
            Assert.That(operationWasRunWhenBusy, Is.False);

            // TODO: Should the returned task actually be faulted or cancelled instead?
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));

            // Cleanup - is this needed?
            asyncLoader.Cancel();
            longTask.SetResult(true);
        }
        #endregion Returned task

        #region Helpers
        internal static int GetCurrentThreadId()
        {
            return Thread.CurrentThread.ManagedThreadId;
        }

        // TODO: Replace with Task.FromException when we upgrade to .NET 4.6
        internal static Task<bool> FromException(Exception e)
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetException(e);
            return tcs.Task;
        }

        internal static Task<bool> FromExceptions(params Exception[] exceptions)
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetException(exceptions);
            return tcs.Task;
        }

        internal static QueuedTaskScheduler CreateSingleThreadTaskScheduler(Action threadInit)
        {
            return new QueuedTaskScheduler(threadCount: 1, threadInit: threadInit);
        }

        class AsyncLoaderTestImpl : AsyncLoaderBase<bool>
        {
            // Provide an event tunnel to the protected AsyncOperationCompleted event
            public event EventHandler<bool> AsyncOperationCompletedTunnel
            {
                add
                {
                    base.AsyncOperationCompleted += value;
                }
                remove
                {
                    base.AsyncOperationCompleted -= value;
                }
            }

            public AsyncLoaderTestImpl() : base(CancellationToken.None) { }

            public AsyncLoaderTestImpl(SynchronizationContext eventContext) : base(eventContext, CancellationToken.None) { }

            public Task PerformAsyncOperation(Func<CancellationToken, Task<bool>> operation)
            {
                return base.PerformAsyncOperation(() => { }, operation, (b, c) => b);
            }

            public Task PerformAsyncOperation(Func<CancellationToken, Task<bool>> operation, Func<bool, CancellationToken, bool> processResult)
            {
                return base.PerformAsyncOperation(() => { }, operation, processResult);
            }

            public void NotifySpecialOperationCompletedTunnel(bool result)
            {
                base.NotifySpecialOperationCompleted(result);
            }
        }
        #endregion Helpers
    }
}
