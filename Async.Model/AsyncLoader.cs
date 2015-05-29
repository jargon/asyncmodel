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
    public sealed class AsyncLoader<TItem> : IAsyncCollection<TItem>, ISeq<TItem>
    {
        // Fields

        private readonly Func<IEnumerable<TItem>, ISeq<TItem>> seqFactory;
        private readonly Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync;
        private readonly Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync;

        private readonly CancellationToken rootCancellationToken;
        private readonly CancellationTokenSource cancellationTokenSource;

        private ISeq<TItem> seq;
        private Task lastCompletedTask;
        private IImmutableList<CollectionChangedHandler<TItem>> collectionChangedHandlers;


        // Properties

        public CollectionStatus Status { get; private set; }
        public bool IsComplete { get { return Status >= CollectionStatus.Ready; } }

        public AggregateException Exception { get { return (lastCompletedTask == null) ? null : lastCompletedTask.Exception; } }
        public Exception InnerException { get { return (Exception == null) ? null : Exception.InnerException; } }
        public string ErrorMessage { get { return (InnerException == null) ? null : InnerException.Message; } }


        // Events

        public event CollectionResetHandler CollectionReset;
        public event CollectionChangedHandler<TItem> CollectionChanged
        {
            add
            {
                bool wonRace = false;

                // We could be racing against other additions or removals, so we need to use CompareExchange in a loop
                do
                {
                    var expectedList = Interlocked.CompareExchange(ref collectionChangedHandlers, null, null);
                    var newList = (expectedList == null) ? ImmutableArray.Create(value) : expectedList.Add(value);
                    var actualList = Interlocked.CompareExchange(ref collectionChangedHandlers, newList, expectedList);

                    // If actualList is the same reference as expectedList, noone changed the field inside our read-update-write cycle
                    wonRace = Object.ReferenceEquals(actualList, expectedList);
                }
                while (!wonRace);
            }
            remove
            {
                bool wonRace = false;

                // We could be racing against other additions or removals, so we need to use CompareExchange in a loop
                do
                {
                    var expectedList = Interlocked.CompareExchange(ref collectionChangedHandlers, null, null);
                    if (expectedList == null)
                        return;  // handler definitely not registered given that our registration list is non-existant

                    var newList = expectedList.Remove(value);
                    if (newList == expectedList)
                        return;  // handler was not registered, given that Remove returned the same list

                    var actualList = Interlocked.CompareExchange(ref collectionChangedHandlers, newList, expectedList);

                    wonRace = Object.ReferenceEquals(actualList, expectedList);
                }
                while (!wonRace);
            }
        }
        public event EventHandler<CollectionStatus> StatusChanged;
        

        // Members

        public AsyncLoader(
            Func<IEnumerable<TItem>, ISeq<TItem>> seqFactory,
            Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync,
            CancellationToken cancellationToken)
        {
            this.seqFactory = seqFactory;
            this.loadDataAsync = loadDataAsync;
            this.fetchUpdatesAsync = fetchUpdatesAsync;

            this.rootCancellationToken = cancellationToken;
            this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(rootCancellationToken);

            this.seq = seqFactory(Enumerable.Empty<TItem>());
            this.Status = CollectionStatus.Ready;
        }

        #region IAsyncCollection API
        public void Cancel()
        {
            cancellationTokenSource.Cancel();
        }

        public void LoadAsync()
        {
            if (loadDataAsync == null)
                return;

            ChangeStatus(CollectionStatus.Loading);

            var scheduler = (SynchronizationContext.Current == null) ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
            var token = cancellationTokenSource.Token;

            var loadDataTask = loadDataAsync(token);

            var insertDataTask = loadDataTask.ContinueWith(InsertLoadedData,
                token,
                token,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);


            // Handle errors for the load and insert tasks

            loadDataTask.ContinueWith(TaskFailedOrCancelled,
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);

            insertDataTask.ContinueWith(TaskFailedOrCancelled,
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);
        }

        public void UpdateAsync()
        {
            if (fetchUpdatesAsync == null)
                return;

            ChangeStatus(CollectionStatus.Updating);

            var scheduler = (SynchronizationContext.Current == null) ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
            var token = cancellationTokenSource.Token;

            var updateDataTask = fetchUpdatesAsync(seq, token);

            var performUpdatesTask = updateDataTask.ContinueWith(PerformUpdates,
                token,
                token,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);


            // Handle errors for the fetch and perform tasks

            updateDataTask.ContinueWith(TaskFailedOrCancelled,
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);

            performUpdatesTask.ContinueWith(TaskFailedOrCancelled,
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);
        }
        #endregion

        #region ISeq API
        public IEnumerator<TItem> GetEnumerator()
        {
            return seq.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return seq.GetEnumerator();
        }

        public TItem First()
        {
            return seq.First();
        }

        public ISeq<TItem> Conj(TItem item)
        {
            seq.Conj(item);
            return this;
        }
        #endregion

        private void InsertLoadedData(Task<IEnumerable<TItem>> loadDataTask, object cancellationToken)
        {
            var token = (CancellationToken)cancellationToken;
            token.ThrowIfCancellationRequested();

            var changes = loadDataTask.Result
                .Select(item => new ItemChange<TItem>(ChangeType.Added, item));

            // Update items and status
            ChangeItems(changes);
            ChangeStatus(CollectionStatus.Ready);
        }

        private void PerformUpdates(Task<IEnumerable<ItemChange<TItem>>> updateDataTask, object cancellationToken)
        {
            var token = (CancellationToken)cancellationToken;
            token.ThrowIfCancellationRequested();

            // Enumerating Result may throw an exception, which is fine in this case
            var changes = seq
                .FullOuterJoin(updateDataTask.Result, i => i, u => u.Item,
                    (i, u, k) => new ItemChange<TItem>(u.Type, (u.Type == ChangeType.Updated) ? u.Item : k));

            // Update items and status
            ChangeItems(changes);
            ChangeStatus(CollectionStatus.Ready);
        }

        private void TaskFailedOrCancelled(Task previous)
        {
            // Update status
            lastCompletedTask = previous;
            var newStatus = previous.IsCanceled ? CollectionStatus.Cancelled
                : (Status == CollectionStatus.Loading) ? CollectionStatus.LoadFailed : CollectionStatus.UpdateFailed;

            ChangeStatus(newStatus);
        }

        private void ChangeItems(IEnumerable<ItemChange<TItem>> changes)
        {
            // Materialize to prevent multiple enumerations of source
            changes = changes.ToArray();

            var newItems = changes
                .Where(c => c.Type != ChangeType.Removed)
                .Select(c => c.Item);
            seq = seqFactory(newItems);

            // Notify collection reset listeners
            // Use CompareExchange to make a safe read of collectionChangedHandlers
            var resetHandler = CollectionReset;
            if (resetHandler != null)
            {
                resetHandler(this);
            }

            // Notify item change listeners
            var changeHandlers = Interlocked.CompareExchange(ref collectionChangedHandlers, null, null);
            if (changeHandlers != null)
            {
                // We don't want to include unchanged items in our notification
                var actualChanges = changes
                    .Where(c => c.Type != ChangeType.Unchanged);

                foreach (var changeHandler in changeHandlers)
                    changeHandler(this, actualChanges);
                
            }
        }

        private void ChangeStatus(CollectionStatus newStatus)
        {
            var oldStatus = Status;
            Status = newStatus;

            var handler = StatusChanged;
            if (handler == null)
                return;

            handler(this, oldStatus);
        }
    }
}
