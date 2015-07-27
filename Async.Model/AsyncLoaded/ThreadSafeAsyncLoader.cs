using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Async.Model;
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

        public override void Replace(TItem newItem)
        {
            using (mutex.Lock())
            {
                seq.Replace(newItem);
                NotifyCollectionChanged(ChangeType.Updated, newItem);
            }
        }

        public override void ReplaceAll(IEnumerable<TItem> newItems)
        {
            using (mutex.Lock())
            {
                seq.ReplaceAll(newItems);

                // TODO: This will generate updates for unchanged items, is this acceptable?
                var changes = newItems.ChangesFrom(seq, i => i, i => i, EqualityComparer<TItem>.Default, new NeverEqualsComparer<TItem>());
                NotifySpecialOperationCompleted(changes);
            }
        }

        class NeverEqualsComparer<T> : EqualityComparer<T>
        {
            public override bool Equals(T x, T y)
            {
                return false;
            }

            public override int GetHashCode(T obj)
            {
                return obj.GetHashCode();
            }
        }

        public override void Clear()
        {
            using (mutex.Lock())
            {
                var changes = seq.Select(item => new ItemChange<TItem>(ChangeType.Removed, item)).ToArray();
                seq.Clear();
                NotifySpecialOperationCompleted(changes);
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
