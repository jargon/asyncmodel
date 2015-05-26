using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model
{
    public sealed class AsyncLoadUpdateCollection<TItem, TCollection> : IEnumerable, IAsyncCollection<TItem> where TCollection : ICollection, IEnumerable<TItem>
    {
        // Fields

        private readonly Func<IEnumerable<TItem>, TCollection> collectionFactory;
        private readonly Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsync;
        private readonly Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync;

        private readonly CancellationToken rootCancellationToken;
        private readonly CancellationTokenSource cancellationTokenSource;

        private TCollection items;
        private Task lastCompletedTask;


        // Properties

        public TCollection Items { get { return items; } }
        public int Count { get { return items.Count; } }
        
        public CollectionStatus Status { get; private set; }
        public bool IsComplete { get { return Status >= CollectionStatus.Ready; } }

        public AggregateException Exception { get { return (lastCompletedTask == null) ? null : lastCompletedTask.Exception; } }
        public Exception InnerException { get { return (Exception == null) ? null : Exception.InnerException; } }
        public string ErrorMessage { get { return (InnerException == null) ? null : InnerException.Message; } }


        // Events

        public event EventHandler<IEnumerable<TItem>> CollectionChanged;
        public event EventHandler<CollectionStatus> StatusChanged;
        

        // Members

        public AsyncLoadUpdateCollection(
            Func<IEnumerable<TItem>, TCollection> collectionFactory,
            Func<CancellationToken, Task<IEnumerable<TItem>>> loadDataAsyc,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<ItemChange<TItem>>>> fetchUpdatesAsync,
            CancellationToken cancellationToken)
        {
            this.collectionFactory = collectionFactory;
            this.loadDataAsync = loadDataAsyc;
            this.fetchUpdatesAsync = fetchUpdatesAsync;

            this.rootCancellationToken = cancellationToken;
            this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(rootCancellationToken);

            this.Status = CollectionStatus.Ready;
        }

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

            var updateDataTask = fetchUpdatesAsync(items, token);

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

        public IEnumerator<TItem> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return items.GetEnumerator();
        }

        private void InsertLoadedData(Task<IEnumerable<TItem>> loadDataTask, object cancellationToken)
        {
            var token = (CancellationToken)cancellationToken;
            token.ThrowIfCancellationRequested();

            // This will enumerate Result and may therefore throw an exception in which case we won't change items
            var newItems = collectionFactory(loadDataTask.Result);

            // Update items and status
            ChangeItems(newItems);
            ChangeStatus(CollectionStatus.Ready);

            // NOTE: cannot use the multi-item Add event here, since frameworks like WPF don't support it
            // See: http://www.interact-sw.co.uk/iangblog/2013/02/22/batch-inotifycollectionchanged
        }

        private void PerformUpdates(Task<IEnumerable<ItemChange<TItem>>> updateDataTask, object cancellationToken)
        {
            var token = (CancellationToken)cancellationToken;
            token.ThrowIfCancellationRequested();

            var query = items
                .FullOuterJoin(updateDataTask.Result, i => i, u => u.Item,
                    (i, u, k) => new ItemChange<TItem>(u.Type, (u.Type == ItemChange<TItem>.ChangeType.Updated) ? u.Item : k))
                .Where(u => u.Type != ItemChange<TItem>.ChangeType.Removed)
                .Select(u => u.Item);

            // Enumerating Result may throw an exception, which is fine in this case
            var newItems = collectionFactory(query);

            // Update items and status
            ChangeItems(newItems);
            ChangeStatus(CollectionStatus.Ready);

            // It would be too much work to use the multi-item events here even if they were properly supported,
            // since the indices change with each add and remove
        }

        private void TaskFailedOrCancelled(Task previous)
        {
            // Update status
            lastCompletedTask = previous;
            var newStatus = previous.IsCanceled ? CollectionStatus.Cancelled
                : (Status == CollectionStatus.Loading) ? CollectionStatus.LoadFailed : CollectionStatus.UpdateFailed;

            ChangeStatus(newStatus);
        }

        private void ChangeItems(TCollection newItems)
        {
            var oldItems = items;
            items = newItems;

            var handler = CollectionChanged;
            if (handler == null)
                return;

            handler(this, oldItems);
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
