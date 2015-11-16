using System.Threading;

namespace Async.Model.Context
{
    /// <summary>
    /// A <see cref="SynchronizationContext"/> that will execute all callbacks inline on the current thread. Among
    /// other things, this is very useful for testing scenarios, since you can often avoid complicated synchronization
    /// by using this class.
    /// <remarks>Note that this class intentionally breaks the contract of
    /// <see cref="SynchronizationContext.Post(SendOrPostCallback, object)"/>, since the callback will be executed
    /// synchronously rather than asynchronously.</remarks>
    /// </summary>
    public sealed class RunInlineSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state)
        {
            d(state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            d(state);
        }
    }
}
