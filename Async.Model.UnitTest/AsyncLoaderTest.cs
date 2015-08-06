using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using Async.Model.Sequence;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using IntChangesAlias = System.Collections.Generic.IEnumerable<Async.Model.IItemChange<int>>;

namespace Async.Model.UnitTest
{
    [TestFixture]
    public class AsyncLoaderTest
    {
        [SetUp]
        public void BeforeEachTest()
        {
            // Match enums using name instead of value, otherwise error messages will only show the numeric value of the enum (useless)
            FluentAssertions.AssertionOptions.AssertEquivalencyUsing(options => options.ComparingEnumsByName());
        }

        [Test]
        public void CanLoadEmptyList()
        {
            var loader = new AsyncLoader<string>(
                seqFactory: Seq.ListBased,
                loadDataAsync: token => Task.FromResult(Enumerable.Empty<string>()));

            loader.LoadAsync();


            Assert.That(loader.ToList(), Is.Empty);
        }

        [Test]
        public void CanEnumerateLoadedItems()
        {
            var loadedItems = new[] { 1, 2, 3 };

            var loader = new AsyncLoader<int>(
                seqFactory: Seq.ListBased,
                loadDataAsync: t => Task.FromResult(loadedItems.AsEnumerable()));
            IEnumerable<int> values = loader;


            loader.LoadAsync();  // --- Perform ---


            Assert.That(loader, Is.EqualTo(loadedItems));
        }

        [Test]
        public void LoadAsyncClearsPreviousContents()
        {
            IEnumerable<int> originalItems = new[] { 1, 2, 3 };
            IEnumerable<int> loadedItems = new[] { 4, 5, 6 };

            var loadFunc = Substitute.For<Func<CancellationToken, Task<IEnumerable<int>>>>();
            loadFunc.Invoke(Arg.Any<CancellationToken>()).Returns(Task.FromResult(originalItems), Task.FromResult(loadedItems));

            var loader = new AsyncLoader<int>(seqFactory: Seq.ListBased, loadDataAsync: loadFunc);

            loader.LoadAsync();  // initial load
            loader.Should().BeEquivalentTo(originalItems);  // sanity check


            loader.LoadAsync();  // --- Perform ---
            loader.Should().BeEquivalentTo(loadedItems);
        }

        /// <summary>
        /// This is an important use-case for the send queue: while loading any old contents of the
        /// queue, the user should be allowed to compose and queue a new teamstring for sending.
        /// This means that LoadAsync must keep items added during the load.
        /// </summary>
        [Test]
        public async Task LoadAsyncPreservesItemsAddedDuringLoad()
        {
            IEnumerable<int> itemsToLoad = new[] { 2, 3, 4 };
            var loadTask = new TaskCompletionSource<IEnumerable<int>>();
            var loader = new AsyncLoader<int>(seqFactory: Seq.ListBased, loadDataAsync: tok => loadTask.Task);


            // --- Perform ---
            var loadComplete = loader.LoadAsync();  // Will get stuck, waiting for loadTask to complete
            loader.Conj(1);


            loader.Should().BeEquivalentTo(new[] { 1 });  // sanity check
            loadTask.SetResult(itemsToLoad);  // complete loading
            await loadComplete;  // wait for LoadAsync to finish

            loader.Should().BeEquivalentTo(new[] { 1, 2, 3, 4 });
        }

        /// <summary>
        /// This test verifies that the tested class circumvents the issue which normal compiler generated event handler
        /// add/remove methods run into, namely that Delegate.Combine does not support delegates of different generic
        /// types.
        /// </summary>
        /// <see cref="http://stackoverflow.com/questions/1120688/event-and-delegate-contravariance-in-net-4-0-and-c-sharp-4-0"/>
        [Test]
        public void CanAddCollectionChangeHandlersOfDifferentTypes()
        {
            var loader = new AsyncLoader<Item>(seqFactory: Seq.ListBased);

            // Simulate an external consumer of this collection
            IAsyncCollection<IItem> externalView = loader;

            externalView.CollectionChanged += DummyChangesListener;
            //externalView.CollectionChanges += DummyRootListener;  // does not compile??
            externalView.CollectionChanged += new CollectionChangedHandler<IRoot>(DummyRootListener);
        }

        [Test]
        public void CollectionChangedHandlerInvokedForLoadOfInts()
        {
            var loadedItems = new[] { 1, 2, 3 };

            var loader = new AsyncLoader<int>(
                seqFactory: Seq.ListBased,
                loadDataAsync: t => Task.FromResult(loadedItems.AsEnumerable()),
                eventScheduler: new CurrentThreadTaskScheduler());

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.LoadAsync();  // --- Perform ---

            
            var expectedChanges = loadedItems.Select(i => new ItemChange<int>(ChangeType.Added, i));
            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(coll =>
                coll.Should().BeEquivalentTo(expectedChanges)));
        }

        [Test]
        public void CollectionChangeHandlerInvokedForLoadWithContravariantHandler()
        {
            IEnumerable<Item> loadedItems = new[] { new Item(), new Item() };

            var loader = new AsyncLoader<Item>(
                seqFactory: Seq.ListBased,
                loadDataAsync: t => Task.FromResult(loadedItems),
                eventScheduler: new CurrentThreadTaskScheduler());

            // Simulate an external consumer of this collection
            IAsyncCollection<IItem> externalView = loader;

            var listener = Substitute.For<CollectionChangedHandler<IItem>>();
            externalView.CollectionChanged += listener;


            loader.LoadAsync();  // --- Perform ---


            var expectedChanges = loadedItems.Select(item => new ItemChange<Item>(ChangeType.Added, item));
            listener.Received().Invoke(loader, Fluent.Match<IEnumerable<IItemChange<Item>>>(coll =>
                coll.Should().BeEquivalentTo(expectedChanges)));
        }

        interface IRoot { }
        interface IItem : IRoot { }

        class Item : IItem { }

        void DummyChangesListener(object sender, IEnumerable<IItemChange<IItem>> changes) { }
        void DummyRootListener(object sender, IEnumerable<IItemChange<IRoot>> changes) { }

        [Test]
        public void LoadAsyncNotifiesOfClearedItems()
        {
            IEnumerable<int> initialItems = new[] { 1, 2, 3 };
            IEnumerable<int> loadedItems = new[] { 4, 5, 6 };

            var actualChanges = new List<IItemChange<int>>();
            IntChangesAlias expectedChanges = initialItems
                .Select(i => new ItemChange<int>(ChangeType.Removed, i))
                .Concat(loadedItems.Select(i => new ItemChange<int>(ChangeType.Added, i)));

            var loadFunc = Substitute.For<Func<CancellationToken, Task<IEnumerable<int>>>>();
            loadFunc.Invoke(Arg.Any<CancellationToken>()).Returns(Task.FromResult(initialItems), Task.FromResult(loadedItems));

            var loader = new AsyncLoader<int>(Seq.ListBased, loadDataAsync: loadFunc, eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();  // initial load
            loader.CollectionChanged += (s, e) => actualChanges.AddRange(e);  // add all emitted changes to a list


            loader.LoadAsync();  // --- Perform ---


            actualChanges.Should().BeEquivalentTo(expectedChanges);
        }

        [Test]
        public void LoadAsyncDoesNotNotifyOfClearIfEmptyBeforeCall()
        {
            IEnumerable<int> loadedItems = new[] { 1 };

            var loader = new AsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedItems),
                eventScheduler: new CurrentThreadTaskScheduler());

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.LoadAsync();  // --- Perform ---


            listener.Received(1).Invoke(loader, Arg.Any<IntChangesAlias>());
        }

        [Test]
        public void CanEnumerateWithoutLoadOrUpdate()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased);
            foreach (var item in loader)
                Console.WriteLine("Item: " + item);
        }

        [Test]
        public void CanConjWithoutLoadOrUpdate()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased);
            loader.Conj(1);
        }

        [Test]
        public void CollectionChangedHandlerInvokedForConj()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased, eventScheduler: new CurrentThreadTaskScheduler());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Conj(1);  // --- Perform ---

            
            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Added, 1))));
        }

        [Test]
        public async Task CollectionChangedHandlerInvokedForConjAsync()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased, eventScheduler: new CurrentThreadTaskScheduler());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            await loader.ConjAsync(1, CancellationToken.None);  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Added, 1))));
        }

        [Test]
        public void CollectionChangedHandlerInvokedForTake()
        {
            IEnumerable<int> loadedInts = new[] { 42 };
            var loader = new AsyncLoader<int>(Seq.ListBased, loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Take();  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Removed, 42))));
        }

        [Test]
        public async Task CollectionChangedHandlerInvokedForTakeAsync()
        {
            IEnumerable<int> loadedInts = new[] { 35 };
            var loader = new AsyncLoader<int>(Seq.ListBased, loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            await loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            await loader.TakeAsync(CancellationToken.None);  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Removed, 35))));
        }

        [Test]
        [Category("Limitations")]
        public void AsyncLoaderDoesNotSupportReplace()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased);
            Action callingReplace = () => loader.Replace(1, 2);
            callingReplace.ShouldThrow<NotSupportedException>();
        }

        [Test]
        [Category("Limitations")]
        public void AsyncLoaderDoesNotSupportReplaceAll()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased);
            Action callingReplaceAll = () => loader.ReplaceAll(new int[0]);
            callingReplaceAll.ShouldThrow<NotSupportedException>();
        }

        [Test]
        [Category("Limitations")]
        public void AsyncLoaderDoesNotSupportClear()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased);
            Action callingClear = () => loader.Clear();
            callingClear.ShouldThrow<NotSupportedException>();
        }

        // A smart trick for unit testing - use SpinWait.SpinUntil
        // See: http://blogs.msdn.com/b/pfxteam/archive/2011/02/15/10129633.aspx
    }
}
