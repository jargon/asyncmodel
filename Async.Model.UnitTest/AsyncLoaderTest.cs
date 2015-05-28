using Async.Model;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.UnitTest
{
    [TestFixture]
    public class AsyncLoaderTest
    {
        [Test]
        public void CanLoadEmptyList()
        {
            var loader = new AsyncLoader<string, List<string>>(
                collectionFactory: items => new List<string>(items),
                loadDataAsyc: token => Task.FromResult((IEnumerable<string>)new string[] { }),
                fetchUpdatesAsync: null,
                cancellationToken: CancellationToken.None);

            loader.LoadAsync();
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
            var loader = new AsyncLoader<Item, List<Item>>(
                collectionFactory: items => new List<Item>(items),
                loadDataAsyc: null,
                fetchUpdatesAsync: null,
                cancellationToken: CancellationToken.None);

            // Simulate an external consumer of this collection
            IAsyncCollection<IItem> externalView = loader;

            externalView.CollectionChanged += DummyChangesListener;
            //externalView.CollectionChanges += DummyRootListener;  // does not compile??
            externalView.CollectionChanged += new CollectionChangedHandler<IRoot>(DummyRootListener);
        }

        [Test]
        public void CollectionChangeHandlerInvokedForLoad()
        {
            IEnumerable<Item> loadedItems = new[] { new Item(), new Item() };

            var loader = new AsyncLoader<Item, List<Item>>(
                collectionFactory: items => new List<Item>(items),
                loadDataAsyc: t => Task.FromResult(loadedItems),
                fetchUpdatesAsync: null,
                cancellationToken: CancellationToken.None);

            // Simulate an external consumer of this collection
            IAsyncCollection<IItem> externalView = loader;

            var listener = Substitute.For<CollectionChangedHandler<IItem>>();


            loader.LoadAsync();  // --- Perform ---


            IEnumerable<ItemChange<IItem>> expectedChanges = new[]
            {
                new ItemChange<IItem>(ChangeType.Added, loadedItems.ElementAt(0)),
                new ItemChange<IItem>(ChangeType.Added, loadedItems.ElementAt(1))
            };

            listener.Received().Invoke(loader, expectedChanges);
        }

        interface IRoot { }
        interface IItem : IRoot { }

        class Item : IItem { }

        void DummyChangesListener(object sender, IEnumerable<IItemChange<IItem>> changes) { }
        void DummyRootListener(object sender, IEnumerable<IItemChange<IRoot>> changes) { }
    }
}
