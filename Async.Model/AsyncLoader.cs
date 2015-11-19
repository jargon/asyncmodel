using Async.Model.AsyncLoaded;
using Async.Model.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace Async.Model
{
    public class AsyncLoader<TItem> : AsyncLoaderBase<IEnumerable<ItemChange<TItem>>>, IAsyncCollectionLoader<TItem>
    {
        private readonly Func<IEnumerable<TItem>, IAsyncSeq<TItem>> seqFactory;
        private readonly Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync;
        private readonly Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync;

        protected readonly IAsyncSeq<TItem> seq;
        protected readonly IEqualityComparer<TItem> identityComparer;


        public AsyncLoader(
            // TODO: There's no longer a point in taking a factory, change to take an ISeq directly
            Func<IEnumerable<TItem>, ISeq<TItem>> seqFactory,
            Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync = null,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync = null,
            IEqualityComparer<TItem> identityComparer = null,
            CancellationToken rootCancellationToken = default(CancellationToken),
            SynchronizationContext eventContext = null) : base(eventContext, rootCancellationToken)
        {
            this.loadDataAsync = loadDataAsync;
            this.fetchUpdatesAsync = fetchUpdatesAsync;

            // If the given seq factory does not produce async seqs, we need to wrap it
            var asyncSeqFactory = seqFactory as Func<IEnumerable<TItem>, IAsyncSeq<TItem>>;
            if (asyncSeqFactory == null)
                asyncSeqFactory = items => seqFactory(items).AsAsync();

            this.seqFactory = asyncSeqFactory;
            this.seq = asyncSeqFactory(Enumerable.Empty<TItem>());

            this.identityComparer = identityComparer ?? EqualityComparer<TItem>.Default;
        }

        #region IAsyncCollectionLoader API
        public event CollectionChangedHandler<TItem> CollectionChanged
        {
            add
            {
                // TODO: Should add weak event handler here to prevent leaks
                // NOTE: Cannot simply add given event handler, since it uses a specialized delegate
                AsyncOperationCompleted += (s, e) => value(s, e);
            }
            remove
            {
            }
        }
        
        /// <summary>
        /// Performs an asynchronous load using the loadDataAsync function previously given in the constructor. This
        /// will clear the underlying seq before starting the load operation. This method is intended to be used for
        /// initial data load. Use UpdateAsync for keeping the loaded data up to date.
        /// </summary>
        /// <remarks>
        /// Note that it is okay to Conj items to the collection while loading. When the loading completes, any items
        /// in the seq will be concatenated to the loaded items.
        /// </remarks>
        public Task LoadAsync()
        {
            if (loadDataAsync == null)
            {
                // We still need to clear the seq under lock
                using (mutex.Lock())
                {
                    ClearInsideLock();
                }
                return TaskConstants.Completed;
            }

            return PerformAsyncOperation(() => ClearInsideLock(), loadDataAsync, InsertLoadedDataInsideLock);
        }

        public Task UpdateAsync()
        {
            if (fetchUpdatesAsync == null)
                return TaskConstants.Completed;

            return PerformAsyncOperation(() => { }, token => fetchUpdatesAsync(seq, token), PerformUpdatesInsideLock);
        }
        #endregion IAsyncCollectionLoader API

        #region IAsyncSeq API
        public virtual IEnumerator<TItem> GetEnumerator()
        {
            return seq.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public virtual TItem Take()
        {
            var item = seq.Take();

            NotifyCollectionChanged(ChangeType.Removed, item);
            return item;
        }

        public virtual void Conj(TItem item)
        {
            seq.Conj(item);

            NotifyCollectionChanged(ChangeType.Added, item);
        }

        public virtual void Replace(TItem oldItem, TItem newItem)
        {
            throw new NotSupportedException("AsyncLoader does not support Replace");
        }

        public virtual void ReplaceAll(IEnumerable<TItem> newItems)
        {
            throw new NotSupportedException("AsyncLoader does not support ReplaceAll");
        }

        // TODO: AsyncLoader should implement Clear instead of using ClearInsideLock once we switch mutex to a reentrant lock
        public virtual void Clear()
        {
            throw new NotSupportedException("AsyncLoader does not support Clear");
        }

        private void ClearInsideLock()
        {
            var changes = seq.Select(item => new ItemChange<TItem>(ChangeType.Removed, item))
                .ToArray();  // must materialize before we change seq

            seq.Clear();

            NotifyCollectionChanged(changes);
        }

        public virtual async Task<TItem> TakeAsync(CancellationToken cancellationToken)
        {
            using (var lcs = CancellationTokenSource.CreateLinkedTokenSource(rootCancellationToken, cancellationToken))
            {
                var item = await seq.TakeAsync(lcs.Token);

                NotifyCollectionChanged(ChangeType.Removed, item);
                return item;
            }
        }

        public virtual async Task ConjAsync(TItem item, CancellationToken cancellationToken)
        {
            using (var lcs = CancellationTokenSource.CreateLinkedTokenSource(rootCancellationToken, cancellationToken))
            {
                await seq.ConjAsync(item, lcs.Token);

                NotifyCollectionChanged(ChangeType.Added, item);
            }
        }
        #endregion IAsyncSeq API

        #region Task continuations
        private IEnumerable<ItemChange<TItem>> InsertLoadedDataInsideLock(IEnumerable<TItem> loadedData, CancellationToken cancellationToken)
        {
            // Materialize to prevent multiple enumerations of source
            // Enumerating loadedData may throw an exception, which will be handled in base class
            loadedData = loadedData.ToArray();

            // Since LoadAsync clears the collection, any items now present in seq has been Conj'ed and should therefore be kept
            // Need to force eager enumeration of seq (by using ToArray), since ReplaceAll modifies the same seq
            seq.ReplaceAll(seq.Concat(loadedData).ToArray());

            // Loaded items count as added
            return loadedData.Select(item => new ItemChange<TItem>(ChangeType.Added, item));
        }

        private IEnumerable<ItemChange<TItem>> PerformUpdatesInsideLock(IEnumerable<ItemChange<TItem>> fetchedUpdates, CancellationToken cancellationToken)
        {
            // Enumerating fetchedUpdates may throw an exception, which will be handled in base class
            // NOTE: We preserve any items from seq not found in fetchedUpdates, since they must have been Conj'ed
            var changes = seq
                .FullOuterJoin(fetchedUpdates, i => i, u => u.Item,
                    (i, u, k) => u ?? new ItemChange<TItem>(ChangeType.Unchanged, i), identityComparer)
                .ToArray();  // materialize to prevent multiple enumerations of source

            // TODO: What do we do about invalid item changes, such as update or removal of a non-existing item?
            // We have several options:
            // 1. We throw an exception. This has the advantage of helping to uncover bugs. But there are problems.
            //    First, if we throw an exception directly in this method, it will be caught by AsyncLoaderBase and
            //    treated as a failed async operation. To solve this, we could post a callback on the event
            //    synchronization context that throws the exception on the UI thread. Second, treating these invalid
            //    item changes as errors could turn otherwise benign race conditions into fatal errors.
            // 2. We ignore the item change. This could make our code simpler, because we might not need as much
            //    coordination between what happens in the UI and what happens in asynchronous background operations.
            //    The disadvantage of this approach is that it could hide bugs.

            // Filter out removed items
            var newItems = changes
                .Where(c => c.Type != ChangeType.Removed)
                .Select(c => c.Item);

            seq.ReplaceAll(newItems);

            // Filter out unchanged items
            return changes.Where(c => c.Type != ChangeType.Unchanged);
        }
        #endregion

        protected void NotifyCollectionChanged(ChangeType type, TItem item)
        {
            var changes = new[] { new ItemChange<TItem>(type, item) };
            NotifySpecialOperationCompleted(changes);
        }

        protected void NotifyCollectionChanged(IEnumerable<ItemChange<TItem>> changes)
        {
            if (changes.Any())
            {
                // Only notify if actual changes were made
                NotifySpecialOperationCompleted(changes);
            }
        }
    }
}
