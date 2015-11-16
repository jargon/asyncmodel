using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

namespace Async.Model.Context
{
    /// <summary>
    /// A <see cref="SynchronizationContext"/> that schedules posted callbacks to an underlying
    /// <see cref="TaskScheduler"/>.
    /// <remarks>Depending on the used task scheduler, the contract of <see cref="Post(SendOrPostCallback, object)"/>
    /// may be breached. For example, using <c>CurrentThreadTaskScheduler</c> from Parallel Extensions Extras
    /// (http://blogs.msdn.com/b/pfxteam/archive/2010/04/09/9990424.aspx) will cause posted callbacks to be executed
    /// synchronously inline on the current thread instead of asynchronously.</remarks>
    /// </summary>
    public sealed class TaskSchedulerSynchronizationContext : SynchronizationContext
    {
        /// <summary>A <see cref="TaskFactory"/> used to run tasks on the underlying <see cref="TaskScheduler"/>.</summary>
        private readonly TaskFactory taskFactory;

        /// <summary>
        /// Constructs a new <see cref="TaskSchedulerSynchronizationContext"/> that will execute posted and sent 
        /// callbacks on the given <see cref="TaskScheduler"/> instance.
        /// </summary>
        /// <param name="taskScheduler">The task scheduler to execute tasks on.</param>
        public TaskSchedulerSynchronizationContext(TaskScheduler taskScheduler)
        {
            this.taskFactory = new TaskFactory(taskScheduler);
        }

        /// <summary>
        /// Executes the given callback as a task on the underlying <see cref="TaskScheduler"/>. See
        /// <see cref="SynchronizationContext.Post(SendOrPostCallback, object)"/> for further details.
        /// </summary>
        public override void Post(SendOrPostCallback d, object state)
        {
            taskFactory.Run(() => d(state));
        }

        /// <summary>
        /// Executes the given callback as a task on the underlying <see cref="TaskScheduler"/> and waits for its
        /// completion. Unwraps any exception thrown. See
        /// <see cref="SynchronizationContext.Send(SendOrPostCallback, object)"/> for further details.
        /// </summary>
        public override void Send(SendOrPostCallback d, object state)
        {
            var task = taskFactory.Run(() => d(state));
            task.WaitAndUnwrapException();
        }
    }
}
