using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Async.Model.Sequence;

namespace Async.Model.AsyncLoaded
{
    public sealed class ThreadSafeAsyncLoader<TItem> : AsyncLoader<TItem>
    {

        /// <summary>
        /// An update comparer that deems any item updated by always returning false. Necessary
        /// because we cannot in general know if two items that test equal are in fact identical.
        /// By doing this we choose correctness over performance: we may notify about updates where
        /// none occurred, but at least we will never miss an update.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        class ConservativeUpdateComparer<T> : EqualityComparer<T>
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

        private readonly IEqualityComparer<TItem> updateComparer;

        public ThreadSafeAsyncLoader(
            Func<IEnumerable<TItem>, ISeq<TItem>> seqFactory,
            Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync = null,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync = null,
            IEqualityComparer<TItem> identityComparer = null,
            IEqualityComparer<TItem> updateComparer = null,
            CancellationToken rootCancellationToken = default(CancellationToken),
            SynchronizationContext eventContext = null) : base(seqFactory, loadDataAsync, fetchUpdatesAsync, identityComparer, rootCancellationToken, eventContext)
        {
            // Default to conservative comparer when checking for updated items
            // NOTE: This will generate updates for unchanged items, which should not be a correctness problem
            this.updateComparer = updateComparer ?? new ConservativeUpdateComparer<TItem>();
        }

        // TODO: Move all event notification out of the lock scope to minimize contention

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

        public override void Replace(TItem oldItem, TItem newItem)
        {
            ItemChange<TItem>[] changes;

            Debug.WriteLine("ThreadSafeAsyncLoader.Replace: Taking mutex");
            using (mutex.Lock())
            {
                // NOTE: Cannot use LinqExtensions.Replace here, since we need to know which items
                // were replaced for event notifications
                changes = seq.Select(item =>
                {
                    if (identityComparer.Equals(item, oldItem))
                        return new ItemChange<TItem>(ChangeType.Updated, newItem);
                    else
                        return new ItemChange<TItem>(ChangeType.Unchanged, item);
                }).ToArray();

                // Perform replacement
                seq.ReplaceAll(changes.Select(c => c.Item));
            }
            Debug.WriteLine("ThreadSafeAsyncLoader.Replace: Released mutex");

            NotifyCollectionChanged(changes.Where(c => c.Type == ChangeType.Updated));
        }

        public override void ReplaceAll(IEnumerable<TItem> newItems)
        {
            ItemChange<TItem>[] changes;

            Debug.WriteLine("ThreadSafeAsyncLoader.ReplaceAll: Take mutex");
            using (mutex.Lock())
            {
                changes = newItems.ChangesFrom(seq, identityComparer, updateComparer)
                    .ToArray();  // must materialize before we change seq

                seq.ReplaceAll(newItems);
            }
            Debug.WriteLine("ThreadSafeAsyncLoader.ReplaceAll: Released mutex");

            NotifyCollectionChanged(changes.Where(c => c.Type != ChangeType.Unchanged));
        }

        public override void Clear()
        {
            ItemChange<TItem>[] changes;

            Debug.WriteLine("ThreadSafeAsyncLoader.Clear: Take mutex");
            using (mutex.Lock())
            {
                changes = seq.Select(item => new ItemChange<TItem>(ChangeType.Removed, item))
                    .ToArray();  // must materialize before we change seq

                seq.Clear();
            }
            Debug.WriteLine("ThreadSafeAsyncLoader.Clear: Released mutex");

            NotifyCollectionChanged(changes);
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
