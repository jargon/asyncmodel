using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using Async.Model.AsyncLoaded;
using Async.Model.Sequence;
using Async.Model.TestExtensions;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
// Because noone wants to type out this every time...
using IntChangesAlias = System.Collections.Generic.IEnumerable<Async.Model.IItemChange<int>>;

namespace Async.Model.UnitTest.AsyncLoaded
{
    // TODO: Add tests that verify locking once we can use Monitor.IsEntered (when we switch to using lock keyword)
    [TestFixture]
    public class ThreadSafeAsyncLoaderTest
    {
        #region Replace
        [Test]
        public void ReplaceWorksForEmptyLoader()
        {
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased);
            loader.Replace(1, 2);  // --- Perform ---
        }

        [Test]
        public void ReplaceCanReplaceSingleton()
        {
            IEnumerable<int> loadedInts = new[] { 1 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(1, 2);  // --- Perform ---

            loader.Should().BeEquivalentTo(new[] { 2 });
        }

        [Test]
        public void ReplaceOnSingletonDoesNothingWhenNotMatched()
        {
            IEnumerable<int> loadedInts = new[] { 1 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(2, 1);  // --- Perform ---

            loader.Should().BeEquivalentTo(new[] { 1 });
        }

        [Test]
        public void ReplaceCanReplaceWhenLoaderHasMultipleItems()
        {
            IEnumerable<int> loadedInts = new[] { 1, 2, 3 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(2, 4);  // --- Perform ---

            loader.Should().BeEquivalentTo(new[] { 1, 4, 3 });
        }

        [Test]
        public void ReplaceCanReplaceMultipleItems()
        {
            IEnumerable<int> loadedInts = new[] { 1, 1, 1 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(1, 2);  // --- Perform ---

            loader.Should().BeEquivalentTo(new[] { 2, 2, 2 });
        }

        [Test]
        public void ReplaceNotifiesOfChange()
        {
            IEnumerable<int> loadedInts = new[] { 2 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(2, 1);   // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Updated, 1))));
        }

        [Test]
        public void ReplaceNotifiesOfEveryChangeMade()
        {
            IEnumerable<int> loadedInts = new[] { 2, 2, 2, 2 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(2, 1);   // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().BeEquivalentTo(Enumerable.Repeat(new ItemChange<int>(ChangeType.Updated, 1), 4))));
        }

        [Test]
        public void ReplaceDoesNotNotifyIfLoaderIsEmpty()
        {
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, eventScheduler: new CurrentThreadTaskScheduler());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(1, 2);  // --- Perform ---


            listener.DidNotReceive().Invoke(loader, Arg.Any<IntChangesAlias>());
        }

        [Test]
        public void ReplaceDoesNotNotifyIfItemNotFound()
        {
            IEnumerable<int> initialValues = new[] { 1, 2 };

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(initialValues),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();  // load initial values

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(3, 1);  // --- Perform ---


            listener.DidNotReceive().Invoke(loader, Arg.Any<IntChangesAlias>());
        }
        #endregion Replace

        #region ReplaceAll
        [Test]
        public void ReplaceAllDelegatesToUnderlyingSeq()
        {
            var items = new[] { 1, 2, 3, 4 };
            var seq = Substitute.For<ISeq<int>>();
            var loader = new ThreadSafeAsyncLoader<int>(enumerable => seq);

            loader.ReplaceAll(items);  // --- Perform ---

            seq.Received().ReplaceAll(items);
        }

        [Test]
        public void ReplaceAllNotifiesOfChangesMade()
        {
            IEnumerable<int> loadedInts = new int[] { 1, 2, 3, 4 };
            var replacements = new int[] { 5, 6 };

            var expectedChanges = loadedInts.Select(i => new ItemChange<int>(ChangeType.Removed, i))
                .Concat(replacements.Select(i => new ItemChange<int>(ChangeType.Added, i)));

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Should().Equal(loadedInts);  // sanity check
            loader.ReplaceAll(replacements);  // --- Perform ---
            loader.Should().Equal(replacements);  // sanity check


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(
                changes => changes.Should().BeEquivalentTo(expectedChanges)));
        }

        /// <summary>
        /// This test illustrates an important limitation of ThreadSafeAsyncLoader: since it cannot
        /// know in general whether two objects that test equal are identical or just similar, it
        /// makes the conservative choice of always assuming that an item present in both the old
        /// and new sequences have been updated. This should not cause correctness problems, but
        /// could potentially cause performance issues.
        /// </summary>
        [Test]
        [Category("Limitations")]
        public void ReplaceAllIssuesUpdateNotificationsForUnchangedItems()
        {
            IEnumerable<int> loadedInts = new int[] { 1 };
            var replacements = new int[] { 1, 2 };

            var expectedChanges = new IItemChange<int>[]
            {
                new ItemChange<int>(ChangeType.Updated, 1),
                new ItemChange<int>(ChangeType.Added, 2)
            };

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.ReplaceAll(replacements);  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(
                changes => changes.Should().BeEquivalentTo(expectedChanges)));
        }

        /// <summary>
        /// This test shows an important limitation of ReplaceAll: it will not accept the presence
        /// of duplicates in the <see cref="ThreadSafeAsyncLoader{TItem}"/> at the time of calling
        /// the method.
        /// </summary>
        [Test]
        [Category("Limitations")]
        public void ReplaceAllDoesNotAllowDuplicatesInLoader()
        {
            IEnumerable<int> loadedInts = new[] { 1, 1, 1 };
            var replacements = new[] { 2, 3 };

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();


            // --- Perform ---
            Action callingReplaceAll = () => loader.ReplaceAll(replacements);
            callingReplaceAll.ShouldThrow<ArgumentException>("because duplicates are not allowed").WithMessage("*duplicates*");
        }

        /// <summary>
        /// This test shows off an important limitation of ReplaceAll: it will not accept the
        /// presence of duplicates in <paramref name="newItems"/>.
        /// </summary>
        [Test]
        [Category("Limitations")]
        public void ReplaceAllDoesNotAllowDuplicatesInNewItems()
        {
            IEnumerable<int> loadedInts = new[] { 1, 2, 3 };
            var replacements = new[] { 2, 2 };

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();


            // --- Perform ---
            Action callingReplaceAll = () => loader.ReplaceAll(replacements);
            callingReplaceAll.ShouldThrow<ArgumentException>("because duplicates are not allowed").WithMessage("*duplicates*");
        }
        #endregion ReplaceAll

        #region Clear
        [Test]
        public void ClearDelegatesToUnderlyingSeq()
        {
            var seq = Substitute.For<ISeq<int>>();
            var loader = new ThreadSafeAsyncLoader<int>(enumerable => seq);

            loader.Clear();  // --- Perform ---

            seq.Received().Clear();
        }

        [Test]
        public void ClearNotifiesOfChangeForSingletonSeq()
        {
            IEnumerable<int> initialValues = new[] { 1 };

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(initialValues),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();  // load initial values

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Clear();  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes => changes
                .Should().ContainSingle("because loader contained single item before Clear")
                .Which.Should().BeRemovalOf(1, "because loader contained the value 1 before Clear")));
        }

        [Test]
        public void ClearNotifiesOfChangesMade()
        {
            IEnumerable<int> initialValues = new[] { 1, 2, 3 };
            var expectedChanges = initialValues.Select(i => new ItemChange<int>(ChangeType.Removed, i));

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(initialValues),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();  // load initial values

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Clear();  // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(
                changes => changes.Should().BeEquivalentTo(expectedChanges)));
        }

        [Test]
        public void ClearDoesNotNotifyIfNoChangesWereMade()
        {
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, eventScheduler: new CurrentThreadTaskScheduler());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Clear();  // --- Perform ---


            listener.DidNotReceive().Invoke(loader, Arg.Any<IntChangesAlias>());
        }
        #endregion Clear
    }
}
