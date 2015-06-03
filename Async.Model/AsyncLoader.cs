using Async.Model.AsyncLoaded;
using Async.Model.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model
{
    public sealed class AsyncLoader<TItem> : AsyncLoaderBase<IEnumerable<ItemChange<TItem>>>, IAsyncCollection<TItem>, IAsyncSeq<TItem>
    {
        // Fields

        private readonly Func<IEnumerable<TItem>, IAsyncSeq<TItem>> seqFactory;
        private readonly Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync;
        private readonly Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync;

        private IAsyncSeq<TItem> seq;


        // Events

        public event CollectionResetHandler CollectionReset;
        public event CollectionChangedHandler<TItem> CollectionChanged
        {
            add
            {
                AsyncOperationCompleted += (s, e) => value(s, e);
            }
            remove
            {
            }
        }
        

        // Members

        public AsyncLoader(
            Func<IEnumerable<TItem>, ISeq<TItem>> seqFactory,
            Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync,
            CancellationToken masterCancellationToken,
            TaskScheduler eventScheduler = null) : base(eventScheduler, masterCancellationToken)
        {
            this.loadDataAsync = loadDataAsync;
            this.fetchUpdatesAsync = fetchUpdatesAsync;

            // If the given seq factory does not produce async seqs, we need to wrap it
            var asyncSeqFactory = seqFactory as Func<IEnumerable<TItem>, IAsyncSeq<TItem>>;
            if (asyncSeqFactory == null)
                asyncSeqFactory = items => seqFactory(items).AsAsync();

            this.seqFactory = asyncSeqFactory;
            this.seq = asyncSeqFactory(Enumerable.Empty<TItem>());
        }

        #region IAsyncCollection API
        public void LoadAsync()
        {
            if (loadDataAsync == null)
                return;

            PerformAsyncOperation(loadDataAsync, InsertLoadedDataInsideLock);
        }

        public void UpdateAsync()
        {
            if (fetchUpdatesAsync == null)
                return;

            IAsyncSeq<TItem> localSeq = ReadSeqWithLock();
            PerformAsyncOperation(token => fetchUpdatesAsync(localSeq, token), PerformUpdatesInsideLock);
        }
        #endregion

        #region IAsyncSeq API
        public IEnumerator<TItem> GetEnumerator()
        {
            return ReadSeqWithLock().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public TakeResult<TItem> Take()
        {
            TakeResult<TItem> take;

            using (mutex.Lock())
            {
                take = seq.Take();
                seq = take.Rest as IAsyncSeq<TItem>;
            }

            NotifyCollectionChanged(ChangeType.Removed, take.First);
            return new TakeResult<TItem>(take.First, this);
        }

        public ISeq<TItem> Conj(TItem item)
        {
            IAsyncSeq<TItem> resultSeq;
            using (mutex.Lock())
            {
                resultSeq = seq.Conj(item) as IAsyncSeq<TItem>;
                seq = resultSeq;
            }

            NotifyCollectionChanged(ChangeType.Added, item);
            return this;
        }

        public async Task<TakeResult<TItem>> TakeAsync(CancellationToken cancellationToken)
        {
            // Support cancellation from 3 sources: the master token given at construction, the Cancel method, the token given to this method
            var lcs = CancellationTokenSource.CreateLinkedTokenSource(masterCancellationSource.Token, cancellationToken);

            TakeResult<TItem> take;
            using (await mutex.LockAsync(lcs.Token))
            {
                take = await seq.TakeAsync(lcs.Token);
                seq = take.Rest as IAsyncSeq<TItem>;
            }

            NotifyCollectionChanged(ChangeType.Removed, take.First);
            return new TakeResult<TItem>(take.First, this);
        }

        public async Task<IAsyncSeq<TItem>> ConjAsync(TItem item, CancellationToken cancellationToken)
        {
            // Support cancellation from 3 sources: the master token given at construction, the Cancel method, the token given to this method
            var lcs = CancellationTokenSource.CreateLinkedTokenSource(masterCancellationSource.Token, cancellationToken);

            using (await mutex.LockAsync(lcs.Token))
            {
                seq = await seq.ConjAsync(item, lcs.Token);
            }

            NotifyCollectionChanged(ChangeType.Added, item);
            return this;
        }
        #endregion

        #region Task continuations
        private IEnumerable<ItemChange<TItem>> InsertLoadedDataInsideLock(IEnumerable<TItem> loadedData, CancellationToken cancellationToken)
        {
            // Materialize to prevent multiple enumerations of source
            // Enumerating loadedData may throw an exception, which will be handled in base class
            loadedData = loadedData.ToArray();

            // Replace items wholesale
            // TODO: Items manually inserted via Conj should not be replaced here
            seq = seqFactory(loadedData);

            // All items count as added
            return loadedData.Select(item => new ItemChange<TItem>(ChangeType.Added, item));
        }

        private IEnumerable<ItemChange<TItem>> PerformUpdatesInsideLock(IEnumerable<ItemChange<TItem>> fetchedUpdates, CancellationToken cancellationToken)
        {
            // Enumerating fetchedUpdates may throw an exception, which will be handled in base class
            var changes = seq
                .FullOuterJoin(fetchedUpdates, i => i, u => u.Item,
                    (i, u, k) => new ItemChange<TItem>(u.Type, (u.Type == ChangeType.Updated) ? u.Item : k))
                .ToArray();  // materialize to prevent multiple enumerations of source

            // Filter out removed items
            var newItems = changes
                .Where(c => c.Type != ChangeType.Removed)
                .Select(c => c.Item);
            seq = seqFactory(newItems);

            // Filter out unchanged items
            return changes.Where(c => c.Type != ChangeType.Unchanged);
        }
        #endregion

        private void NotifyCollectionChanged(ChangeType type, TItem item)
        {
            var changes = new[] { new ItemChange<TItem>(type, item) };
            NotifySpecialOperationCompleted(changes);
        }

        private IAsyncSeq<TItem> ReadSeqWithLock()
        {
            using (mutex.Lock())
            {
                return seq;
            }
        }
    }
}
