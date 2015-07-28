using FluentAssertions;
using Async.Model;
using Async.Model.Sequence;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using FluentAssertions.Formatting;
using EnumerableOfIntegerChangesAlias = System.Collections.Generic.IEnumerable<Async.Model.IItemChange<int>>;

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
                loadDataAsync: token => Task.FromResult((IEnumerable<string>)new string[] { }),
                fetchUpdatesAsync: null,
                rootCancellationToken: CancellationToken.None);

            loader.LoadAsync();


            Assert.That(loader.ToList(), Is.Empty);
        }

        [Test]
        public void CanEnumerateLoadedItems()
        {
            var loadedItems = new[] { 1, 2, 3 };

            var loader = new AsyncLoader<int>(
                seqFactory: Seq.ListBased,
                loadDataAsync: t => Task.FromResult(loadedItems.AsEnumerable()),
                fetchUpdatesAsync: null,
                rootCancellationToken: CancellationToken.None);
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
            Assert.That(loader, Is.EqualTo(originalItems));  // sanity check


            loader.LoadAsync();  // --- Perform ---
            Assert.That(loader, Is.EqualTo(loadedItems));
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
            var loader = new AsyncLoader<Item>(
                seqFactory: Seq.ListBased,
                loadDataAsync: null,
                fetchUpdatesAsync: null,
                rootCancellationToken: CancellationToken.None);

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
                fetchUpdatesAsync: null,
                rootCancellationToken: CancellationToken.None,
                eventScheduler: new CurrentThreadTaskScheduler());

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.LoadAsync();  // --- Perform ---


            var expectedChanges = loadedItems.Select(i => new ItemChange<int>(ChangeType.Added, i));
            listener.Received().Invoke(loader, Fluent.Match<EnumerableOfIntegerChangesAlias>(coll =>
                coll.Should().BeEquivalentTo(expectedChanges)));
        }

        [Test]
        public void CollectionChangeHandlerInvokedForLoadWithContravariantHandler()
        {
            IEnumerable<Item> loadedItems = new[] { new Item(), new Item() };

            var loader = new AsyncLoader<Item>(
                seqFactory: Seq.ListBased,
                loadDataAsync: t => Task.FromResult(loadedItems),
                fetchUpdatesAsync: null,
                rootCancellationToken: CancellationToken.None,
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

            
            listener.Received().Invoke(loader, Fluent.Match<EnumerableOfIntegerChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Added, 1))));
        }

        [Test]
        public async Task CollectionChangedHandlerInvokedForConjAsync()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased, eventScheduler: new CurrentThreadTaskScheduler());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            await loader.ConjAsync(1, CancellationToken.None);  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<EnumerableOfIntegerChangesAlias>(changes =>
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


            listener.Received().Invoke(loader, Fluent.Match<EnumerableOfIntegerChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Removed, 42))));
        }

        [Test]
        public async Task CollectionChangedHandlerInvokedForTakeAsync()
        {
            IEnumerable<int> loadedInts = new[] { 35 };
            var loader = new AsyncLoader<int>(Seq.ListBased, loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            await loader.TakeAsync(CancellationToken.None);  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<EnumerableOfIntegerChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Removed, 35))));
        }

        [Test]
        public void AsyncLoaderDoesNotSupportReplace()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased);
            Action callingReplace = () => loader.Replace(1, 2);
            callingReplace.ShouldThrow<NotSupportedException>();
        }

        [Test]
        public void AsyncLoaderDoesNotSupportReplaceAll()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased);
            Action callingReplaceAll = () => loader.ReplaceAll(new int[0]);
            callingReplaceAll.ShouldThrow<NotSupportedException>();
        }

        [Test]
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
