using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model
{
    public class AsyncLoadUpdateCollection<T> : ObservableCollection<T>, IList
    {
        public enum CollectionStatus
        {
            // Constants partially ordered, so everything from Ready represent completed states.
            Loading,
            Updating,
            Ready,
            LoadFailed,
            UpdateFailed,
            Cancelled
        }

        private readonly TaskScheduler scheduler;
        private readonly CancellationToken cancellationToken;

        private Task lastCompletedTask;

        public CollectionStatus Status { get; private set; }

        public bool IsComplete { get { return Status >= CollectionStatus.Ready; } }
        public bool IsReady { get { return Status == CollectionStatus.Ready; } }

        public AggregateException Exception { get { return (lastCompletedTask == null) ? null : lastCompletedTask.Exception; } }
        public Exception InnerException { get { return (Exception == null) ? null : Exception.InnerException; } }
        public string ErrorMessage { get { return (InnerException == null) ? null : InnerException.Message; } }

        bool IList.IsReadOnly { get { return true; } }
        bool IList.IsFixedSize { get { return false; } }

        public AsyncLoadUpdateCollection(Func<Task<IEnumerable<T>>> loadDataAsyc, Func<IList<T>, Task<IEnumerable<ItemChange<T>>>> FetchUpdatesAsync, CancellationToken cancellationToken)
        {
            this.scheduler = (SynchronizationContext.Current == null) ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
            this.cancellationToken = cancellationToken;

            this.Status = CollectionStatus.Loading;

            // The task tree we setup here is:
            // loadDataAsync (fail or cancel) -> TaskFailedOrCancelled
            // loadDataAsync (success)        -> InsertLoadedData (fail or cancel) -> TaskFailedOrCancelled
            // loadDataAsync (success)        -> InsertLoadedData (success)        -> FetchUpdatesAsync (fail or cancel) -> TaskFailedOrCancelled
            // loadDataAsync (success)        -> InsertLoadedData (success)        -> FetchUpdatesAsync (success)        -> PerformUpdates (fail or cancel) -> TaskFailedOrCancelled
            // loadDataAsync (success)        -> InsertLoadedData (success)        -> FetchUpdatesAsync (success)        -> PerformUpdates (success)        -> Done

            var loadDataTask = loadDataAsyc();

            var insertTask = loadDataTask.ContinueWith(InsertLoadedData,
                cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);

            insertTask.ContinueWith(t =>
                {
                    // Since we cannot use ContinueWith with pre-existing tasks, we must setup the rest of the task tree inside this task
                    var fetchUpdatesTask = FetchUpdatesAsync(Items);

                    var updateTask = fetchUpdatesTask.ContinueWith(PerformUpdates,
                        cancellationToken,
                        TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                        scheduler);

                    // Handle errors for the fetch and update tasks

                    fetchUpdatesTask.ContinueWith(TaskFailedOrCancelled,
                        cancellationToken,
                        TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                        scheduler);

                    updateTask.ContinueWith(TaskFailedOrCancelled,
                        cancellationToken,
                        TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                        scheduler);
                },
                cancellationToken,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);


            // Handle errors for the load and insert tasks

            loadDataTask.ContinueWith(TaskFailedOrCancelled,
                cancellationToken,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);

            insertTask.ContinueWith(TaskFailedOrCancelled,
                cancellationToken,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                scheduler);
        }

        protected override void ClearItems()
        {
            throw new NotSupportedException("Collection is read-only");
        }

        protected override void InsertItem(int index, T item)
        {
            throw new NotSupportedException("Collection is read-only");
        }

        protected override void RemoveItem(int index)
        {
            throw new NotSupportedException("Collection is read-only");
        }

        protected override void SetItem(int index, T item)
        {
            throw new NotSupportedException("Collection is read-only");
        }

        private void InsertLoadedData(Task<IEnumerable<T>> loadDataTask)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // We need access to AddRange and happen to know that ObservableCollection always use List<T>...
            var items = Items as List<T>;

            // We don't know if the enumeration can fail mid-way through, so make the insert "transactional"
            try
            {
                items.AddRange(loadDataTask.Result);
            }
            catch (Exception)
            {
                items.Clear();
                throw;
            }

            // Prepare for update operation
            Status = CollectionStatus.Updating;

            // Fire notification events

            // NOTE: cannot use the multi-item Add event here, since frameworks like WPF don't support it
            // See: http://www.interact-sw.co.uk/iangblog/2013/02/22/batch-inotifycollectionchanged
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));

            OnPropertyChanged(new PropertyChangedEventArgs("Status"));
        }

        private void PerformUpdates(Task<IEnumerable<ItemChange<T>>> updateDataTask)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // We need access to AddRange and happen to know that ObservableCollection always use List<T>...
            var items = Items as List<T>;

            // Enumerating Result may throw an exception, which is fine in this case
            var newItems = items
                .FullOuterJoin(updateDataTask.Result, i => i, u => u.Item,
                    (i, u, k) => new ItemChange<T>(u.Type, (u.Type == ItemChange<T>.ChangeType.Updated) ? u.Item : k))
                .Where(u => u.Type != ItemChange<T>.ChangeType.Removed)
                .Select(u => u.Item)
                .ToArray();

            items.Clear();
            items.AddRange(newItems);

            Status = CollectionStatus.Ready;

            // Fire notification events

            // It would be too much work to use the multi-item events here even if they were properly supported,
            // since the indices change with each add and remove
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));

            OnPropertyChanged(new PropertyChangedEventArgs("Status"));
            OnPropertyChanged(new PropertyChangedEventArgs("IsComplete"));
            OnPropertyChanged(new PropertyChangedEventArgs("IsReady"));
        }

        private void TaskFailedOrCancelled(Task previous)
        {
            // Update fields and properties
            lastCompletedTask = previous;
            Status = previous.IsCanceled ? CollectionStatus.Cancelled
                : (Status == CollectionStatus.Loading) ? CollectionStatus.LoadFailed : CollectionStatus.UpdateFailed;

            // Fire events for properties that changed

            OnPropertyChanged(new PropertyChangedEventArgs("Status"));
            OnPropertyChanged(new PropertyChangedEventArgs("IsComplete"));

            if (previous.IsFaulted)
            {
                OnPropertyChanged(new PropertyChangedEventArgs("Exception"));
                OnPropertyChanged(new PropertyChangedEventArgs("InnerException"));
                OnPropertyChanged(new PropertyChangedEventArgs("ErrorMessage"));
            }
        }
    }
}
