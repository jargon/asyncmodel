using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Async.Model.Sequence;

namespace Async.Model.AsyncLoaded
{
    public sealed class ThreadSafeAsyncLoader<TItem> : AsyncLoader<TItem>
    {
        public ThreadSafeAsyncLoader(
            Func<IEnumerable<TItem>, ISeq<TItem>> seqFactory,
            Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync = null,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync = null,
            CancellationToken rootCancellationToken = default(CancellationToken),
            TaskScheduler eventScheduler = null) : base(seqFactory, loadDataAsync, fetchUpdatesAsync, rootCancellationToken, eventScheduler)
        {
            // Do nothing: base constructor handles everything
        }

        public override TItem Take()
        {
            using (mutex.Lock())
            {
                return base.Take();
            }
        }

        public override void Conj(TItem item)
        {
            using (mutex.Lock())
            {
                base.Conj(item);
            }
        }

        public override void ReplaceAll(IEnumerable<TItem> newItems)
        {
            using (mutex.Lock())
            {
                base.ReplaceAll(newItems);

            }
        }

        public override void Clear()
        {
            using (mutex.Lock())
            {
                base.Clear(); 
            }
        }

        public override IEnumerator<TItem> GetEnumerator()
        {
            using (mutex.Lock())
            {
                // Take a snapshot under lock and return an enumerator of the snapshot
                return seq.ToList().GetEnumerator();
            }
        }

        public override Task<TItem> TakeAsync(CancellationToken cancellationToken)
        {
            // TODO: Confirm that it doesn't make sense
            throw new NotSupportedException("Does not make sense for a locking collection");
        }

        public override Task ConjAsync(TItem item, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Does not make sense for a locking collection");
        }
    }
}
