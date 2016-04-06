using System;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public sealed class AsyncItemLoader<TItem, TProgress> : AsyncLoaderBase<Tuple<TItem, TItem>>, IAsyncItemLoader<TItem, TProgress>
    {
        private readonly Func<IProgress<TProgress>, CancellationToken, Task<TItem>> loadAsync;
        private readonly Func<TItem, IProgress<TProgress>, CancellationToken, Task<TItem>> updateAsync;

        private TItem item;

        public TItem Item
        {
            get
            {
                using (mutex.Lock())
                {
                    return item;
                }
            }
        }

        public event ItemChangedHandler<TItem> ItemChanged
        {
            add
            {
                // TODO: Should add weak event handler here to prevent leaks
                // Forward to the internal operation completed event
                // NOTE: e is a Tuple where Item1 is old item and Item2 is new item, see method ProcessItemUnderLock below
                AsyncOperationCompleted += (s, e) => value(s, e.Item1, e.Item2);
            }
            remove
            {
                // Do nothing
            }
        }

        public AsyncItemLoader(Func<IProgress<TProgress>, CancellationToken, Task<TItem>> loadAsync, Func<TItem, IProgress<TProgress>, CancellationToken, Task<TItem>> updateAsync, CancellationToken rootCancellationToken)
            : base(rootCancellationToken)
        {
            this.loadAsync = loadAsync;
            this.updateAsync = updateAsync;
        }

        public Task LoadAsync(IProgress<TProgress> progress)
        {
            // TODO: Should we follow behaviour of AsyncLoader and clear item during load?
            return PerformAsyncOperation(() => { }, tok => loadAsync(progress, tok), ProcessItemUnderLock);
        }

        public Task UpdateAsync(IProgress<TProgress> progress)
        {
            var it = Item;  // read under lock
            return PerformAsyncOperation(() => { }, tok => updateAsync(it, progress, tok), ProcessItemUnderLock);
        }

        private Tuple<TItem, TItem> ProcessItemUnderLock(TItem newItem, CancellationToken cancellationToken)
        {
            var oldItem = this.item;
            this.item = newItem;

            return Tuple.Create(oldItem, newItem);
        }
    }
}
