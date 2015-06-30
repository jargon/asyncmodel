using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public sealed class AsyncItemLoader<T> : AsyncLoaderBase<T>, IAsyncItemLoader<T>
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
                // Simply forward to the internal operation completed event
                // NOTE: Cannot simply add given event handler, since it uses a specialized delegate
                AsyncOperationCompleted += (s, e) => value(s, e);
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

        private T ProcessItemUnderLock(T item, CancellationToken cancellationToken)
        {
            this.item = item;
            return item;
        }
    }
}
