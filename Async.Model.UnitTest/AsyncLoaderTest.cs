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

namespace Async.Model.UnitTest
{
    [TestFixture]
    public class AsyncLoaderTest
    {
        [Test]
        public void CanLoadEmptyList()
        {
            var loader = new AsyncLoader<string>(
                seqFactory: Seq.ListBased,
                loadDataAsync: token => Task.FromResult((IEnumerable<string>)new string[] { }),
                fetchUpdatesAsync: null,
                masterCancellationToken: CancellationToken.None);

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
                masterCancellationToken: CancellationToken.None);
            IEnumerable<int> values = loader;


            loader.LoadAsync();  // --- Perform ---


            Assert.That(loader, Is.EqualTo(loadedItems));
        }

        /// <summary>
        /// This test verifies that the tested class circumvents the issue with normal compiler generated event handler
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
                masterCancellationToken: CancellationToken.None);

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
                masterCancellationToken: CancellationToken.None,
                eventScheduler: new CurrentThreadTaskScheduler());

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.LoadAsync();  // --- Perform ---


            listener.Received().Invoke(loader, Arg.Is<IEnumerable<IItemChange<int>>>(c => c.WasAdded(loadedItems)));
        }

        [Test]
        public void CollectionChangeHandlerInvokedForLoad()
        {
            IEnumerable<Item> loadedItems = new[] { new Item(), new Item() };

            var loader = new AsyncLoader<Item>(
                seqFactory: Seq.ListBased,
                loadDataAsync: t => Task.FromResult(loadedItems),
                fetchUpdatesAsync: null,
                masterCancellationToken: CancellationToken.None,
                eventScheduler: new CurrentThreadTaskScheduler());

            // Simulate an external consumer of this collection
            IAsyncCollection<IItem> externalView = loader;

            var listener = Substitute.For<CollectionChangedHandler<IItem>>();
            externalView.CollectionChanged += listener;


            loader.LoadAsync();  // --- Perform ---


            listener.Received().Invoke(loader, Arg.Is<IEnumerable<IItemChange<Item>>>(c => c.WasAdded(loadedItems)));
        }

        interface IRoot { }
        interface IItem : IRoot { }

        class Item : IItem { }

        void DummyChangesListener(object sender, IEnumerable<IItemChange<IItem>> changes) { }
        void DummyRootListener(object sender, IEnumerable<IItemChange<IRoot>> changes) { }

        [Test]
        public void CanEnumerateWithoutLoadOrUpdate()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased, null, null, CancellationToken.None);
            foreach (var item in loader)
                Console.WriteLine("Item: " + item);
        }

        [Test]
        public void CanConjWithoutLoadOrUpdate()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased, null, null, CancellationToken.None);
            loader.Conj(1);
        }

        [Test]
        public void CollectionChangedHandlerInvokedForConj()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased, null, null, CancellationToken.None, eventScheduler: new CurrentThreadTaskScheduler());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Conj(1);  // --- Perform ---


            listener.Received().Invoke(loader, Arg.Is<IEnumerable<IItemChange<int>>>(c => c.Single().WasAdded(1)));
        }

        [Test]
        public async Task CollectionChangedHandlerInvokedForConjAsync()
        {
            var loader = new AsyncLoader<int>(Seq.ListBased, null, null, CancellationToken.None, eventScheduler: new CurrentThreadTaskScheduler());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            await loader.ConjAsync(1, CancellationToken.None);  // --- Perform ---


            listener.Received().Invoke(loader, Arg.Is<IEnumerable<IItemChange<int>>>(c => c.Single().WasAdded(1)));
        }

        // A smart trick for unit testing - use SpinWait.SpinUntil
        // See: http://blogs.msdn.com/b/pfxteam/archive/2011/02/15/10129633.aspx
    }
}
