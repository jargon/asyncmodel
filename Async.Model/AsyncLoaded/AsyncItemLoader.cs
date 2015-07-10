using System;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public sealed class AsyncItemLoader<T> : AsyncLoaderBase<Tuple<T, T>>, IAsyncItemLoader<T>
    {
        private readonly Func<CancellationToken, Task<T>> loadAsync;
        private readonly Func<T, CancellationToken, Task<T>> updateAsync;

        private T item;

        public T Item
        {
            get
            {
                using (mutex.Lock())
                {
                    return item;
                }
            }
        }

        public event ItemChangedHandler<T> ItemChanged
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

        public AsyncItemLoader(Func<CancellationToken, Task<T>> loadAsync, Func<T, CancellationToken, Task<T>> updateAsync, CancellationToken rootCancellationToken)
            : base(rootCancellationToken)
        {
            this.loadAsync = loadAsync;
            this.updateAsync = updateAsync;
        }

        public Task LoadAsync()
        {
            return PerformAsyncOperation(loadAsync, ProcessItemUnderLock);
        }

        public Task UpdateAsync()
        {
            var it = Item;  // read under lock
            return PerformAsyncOperation(token => updateAsync(it, token), ProcessItemUnderLock);
        }

        private Tuple<T, T> ProcessItemUnderLock(T newItem, CancellationToken cancellationToken)
        {
            var oldItem = this.item;
            this.item = newItem;

            return Tuple.Create(oldItem, newItem);
        }
    }
}
