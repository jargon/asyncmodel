using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Async.Model.AsyncLoaded;
using Async.Model.Context;
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
            loader.Should().BeEmpty();
        }

        [Test]
        public void Replace_WithPredicate_WorksForEmptyLoader()
        {
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased);
            loader.Replace(i => i == 1, 2);  // --- Perform ---
            loader.Should().BeEmpty();
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
        public void Replace_WithPredicate_CanReplaceSingleton()
        {
            IEnumerable<int> loadedInts = new[] { 1 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(i => i == 1, 2); // --- Perform ---

            loader.Should().BeEquivalentTo(new[] { 2 });
        }

        [Test]
        public void ReplaceOnSingletonDoesNothingWhenNotMatched()
        {
            IEnumerable<int> loadedInts = new[] { 1 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(2, 3);  // --- Perform ---

            loader.Should().BeEquivalentTo(new[] { 1 });
        }

        [Test]
        public void Replace_WithPredicate_OnSingleton_DoesNothing_WhenNotMatched()
        {
            IEnumerable<int> loadedInts = new[] { 1 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(i => i == 2, 3);  // --- Perform ---

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
        public void Replace_WithPredicate_CanReplace_WhenLoader_HasMultipleItems()
        {
            IEnumerable<int> loadedInts = new[] { 1, 2, 3 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(i => i == 2, 4);  // --- Perform ---

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
        public void Replace_WithPredicate_CanReplace_MultipleItems()
        {
            IEnumerable<int> loadedInts = new[] { 1, 1, 1 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, tok => Task.FromResult(loadedInts));
            loader.LoadAsync();

            loader.Replace(i => i == 1, 2);  // --- Perform ---

            loader.Should().BeEquivalentTo(new[] { 2, 2, 2 });
        }

        [Test]
        public void ReplaceNotifiesOfChange()
        {
            IEnumerable<int> loadedInts = new[] { 2 };
            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventContext: new RunInlineSynchronizationContext());

            loader.LoadAsync();  // load initial items

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(2, 1);   // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Updated, 1))));
        }

        [Test]
        public void Replace_WithPredicate_Notifies_OfChange()
        {
            IEnumerable<int> loadedInts = new[] { 2 };
            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventContext: new RunInlineSynchronizationContext());

            loader.LoadAsync();  // load initial items

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(i => i == 2, 1);   // --- Perform ---


            listener.Received(1).Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Updated, 1))));
        }

        [Test]
        public void ReplaceNotifiesOfEveryChangeMade()
        {
            IEnumerable<int> loadedInts = new[] { 2, 2, 2, 2 };
            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventContext: new RunInlineSynchronizationContext());

            loader.LoadAsync();  // load initial items

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(2, 1);   // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().BeEquivalentTo(Enumerable.Repeat(new ItemChange<int>(ChangeType.Updated, 1), 4))));
        }

        [Test]
        public void Replace_WithPredicate_Notifies_OfEveryChangeMade()
        {
            IEnumerable<int> loadedInts = new[] { 2, 2, 2, 2 };
            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(loadedInts),
                eventContext: new RunInlineSynchronizationContext());

            loader.LoadAsync();  // load initial items

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(i => i == 2, 1);   // --- Perform ---


            listener.Received(1).Invoke(loader, Fluent.Match<IntChangesAlias>(changes =>
                changes.Should().BeEquivalentTo(Enumerable.Repeat(new ItemChange<int>(ChangeType.Updated, 1), 4))));
        }

        [Test]
        public void ReplaceDoesNotNotifyIfLoaderIsEmpty()
        {
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, eventContext: new RunInlineSynchronizationContext());
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
                eventContext: new RunInlineSynchronizationContext());
            loader.LoadAsync();  // load initial values

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(3, 1);  // --- Perform ---


            listener.DidNotReceive().Invoke(loader, Arg.Any<IntChangesAlias>());
        }

        // CODESTD: Utility classes kept with the few tests that use them instead of the normal location

        /// <summary>
        /// A class that wraps integers but does NOT override <see cref="object.Equals(object)"/>. This means that
        /// variables of this type will only be considered equal, if they point to the same instance.
        /// </summary>
        private class IntWrapper
        {
            public readonly int Value;

            private IntWrapper(int value) { this.Value = value; }
            public override string ToString() { return this.Value.ToString(); }

            public static implicit operator IntWrapper(int value) { return new IntWrapper(value); }
        }

        /// <summary>A comparer of <see cref="IntWrapper"/> instances that compares the underlying int values.</summary>
        private class IntWrapperComparer : IEqualityComparer<IntWrapper>
        {
            public bool Equals(IntWrapper x, IntWrapper y) { return x.Value == y.Value; }
            public int GetHashCode(IntWrapper obj) { return obj.Value.GetHashCode(); }
        }

        [Test]
        public void ReplaceUsesIdentityComparerGivenAtConstruction()
        {
            var collectionChangedHandler = Substitute.For<CollectionChangedHandler<IntWrapper>>();

            IEnumerable<IntWrapper> originalItems = new IntWrapper[] { 1, 2, 3 };
            IntWrapper replacement = 2;

            IEnumerable<ItemChange<IntWrapper>> expectedChanges = new ItemChange<IntWrapper>[]
            {
                new ItemChange<IntWrapper>(ChangeType.Updated, replacement)
            };

            var loader = new ThreadSafeAsyncLoader<IntWrapper>(
                Seq.ListBased,
                loadDataAsync: _ => Task.FromResult(originalItems),
                identityComparer: new IntWrapperComparer(),
                eventContext: new RunInlineSynchronizationContext());
            loader.LoadAsync();  // load original items

            loader.CollectionChanged += collectionChangedHandler;
            loader.CollectionChanged += (s, e) =>
            {
                // Verify that the expected update was made
                e.Should().Equal(expectedChanges);
            };


            loader.Replace(replacement, replacement);  // --- Perform ---


            // Verify that changes were made by checking for collection changed events
            collectionChangedHandler.Received().Invoke(loader, Arg.Any<IEnumerable<ItemChange<IntWrapper>>>());
        }

        private class TimestampedInt : ITimestamped
        {
            public int Value { get; }
            public DateTime LastUpdated { get; }

            public TimestampedInt(int value, DateTime lastUpdated) { this.Value = value; this.LastUpdated = lastUpdated; }

            public override string ToString() { return $"{Value} @ {LastUpdated:s}"; }

            public override bool Equals(object obj)
            {
                TimestampedInt other = obj as TimestampedInt;
                return (other != null && this.Value == other.Value && this.LastUpdated == other.LastUpdated);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return 3 + 5 * Value.GetHashCode() + 7 * LastUpdated.GetHashCode();
                }
            }
        }

        // A comparer that ignores the timestamp part
        private class TimestampedIntValueComparer : IEqualityComparer<TimestampedInt>
        {
            public bool Equals(TimestampedInt x, TimestampedInt y) { return x.Value == y.Value; }

            public int GetHashCode(TimestampedInt obj) { return 3 + 5 * obj.Value.GetHashCode(); }
        }

        [Test]
        public void Replace_OnTimestampedValues_DoesNotUpdate_IfOlder()
        {
            IEnumerable<TimestampedInt> originalItems = new[]
            {
                new TimestampedInt(1, new DateTime(2017, 09, 08, 18, 56, 00, DateTimeKind.Utc)),
                new TimestampedInt(2, new DateTime(2016, 01, 01, 10, 00, 00, DateTimeKind.Utc))
            };

            // An older replacement
            TimestampedInt replacement = new TimestampedInt(3, new DateTime(2017, 01, 01, 01, 08, 00, DateTimeKind.Utc));

            var loader = new ThreadSafeAsyncLoader<TimestampedInt>(
                seqFactory: Seq.ListBased,
                loadDataAsync: _ => Task.FromResult(originalItems),
                identityComparer: new TimestampedIntValueComparer());

            loader.LoadAsync();

            // --- Perform ---
            loader.Replace(originalItems.ElementAt(0), replacement);

            loader.Should().BeEquivalentTo(originalItems);
        }

        [Test]
        public void Replace_OnTimestampedValues_Updates_IfNewer()
        {
            IEnumerable<TimestampedInt> originalItems = new[]
            {
                new TimestampedInt(1, new DateTime(2017, 09, 08, 18, 56, 00, DateTimeKind.Utc)),
                new TimestampedInt(2, new DateTime(2016, 01, 01, 10, 00, 00, DateTimeKind.Utc))
            };

            // A newer replacement
            TimestampedInt replacement = new TimestampedInt(3, new DateTime(2017, 09, 08, 18, 56, 01, DateTimeKind.Utc));

            var loader = new ThreadSafeAsyncLoader<TimestampedInt>(
                seqFactory: Seq.ListBased,
                loadDataAsync: _ => Task.FromResult(originalItems),
                identityComparer: new TimestampedIntValueComparer());

            loader.LoadAsync();

            // --- Perform ---
            loader.Replace(originalItems.ElementAt(0), replacement);

            loader.Should().BeEquivalentTo(new[] { replacement, originalItems.ElementAt(1) });
        }

        [Test]
        public void Replace_WithPredicate_OnTimestampedValues_DoesNotUpdate_IfOlder()
        {
            IEnumerable<TimestampedInt> originalItems = new[]
            {
                new TimestampedInt(1, new DateTime(2017, 09, 08, 18, 56, 00, DateTimeKind.Utc)),
                new TimestampedInt(2, new DateTime(2016, 01, 01, 10, 00, 00, DateTimeKind.Utc))
            };

            // An older replacement
            TimestampedInt replacement = new TimestampedInt(3, new DateTime(2017, 01, 01, 01, 08, 00, DateTimeKind.Utc));

            var loader = new ThreadSafeAsyncLoader<TimestampedInt>(seqFactory: Seq.ListBased, loadDataAsync: _ => Task.FromResult(originalItems));

            loader.LoadAsync();

            // --- Perform ---
            loader.Replace(i => i.Value == 1, replacement);

            loader.Should().BeEquivalentTo(originalItems);
        }

        [Test]
        public void Replace_WithPredicate_OnTimestampedValues_Updates_IfNewer()
        {
            IEnumerable<TimestampedInt> originalItems = new[]
            {
                new TimestampedInt(1, new DateTime(2017, 09, 08, 18, 56, 00, DateTimeKind.Utc)),
                new TimestampedInt(2, new DateTime(2016, 01, 01, 10, 00, 00, DateTimeKind.Utc))
            };

            // A newer replacement
            TimestampedInt replacement = new TimestampedInt(3, new DateTime(2018, 01, 01, 01, 08, 00, DateTimeKind.Utc));

            var loader = new ThreadSafeAsyncLoader<TimestampedInt>(seqFactory: Seq.ListBased, loadDataAsync: _ => Task.FromResult(originalItems));

            loader.LoadAsync();

            // --- Perform ---
            loader.Replace(i => i.Value == 1, replacement);

            loader.Should().BeEquivalentTo(new[] { replacement, originalItems.ElementAt(1) });
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
                eventContext: new RunInlineSynchronizationContext());
            loader.LoadAsync();  // load initial values

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Should().Equal(loadedInts);  // sanity check
            loader.ReplaceAll(replacements);  // --- Perform ---
            loader.Should().Equal(replacements);  // sanity check


            listener.Received().Invoke(loader, Fluent.Match<IntChangesAlias>(
                changes => changes.Should().BeEquivalentTo(expectedChanges)));
        }

        [Test]
        public void ReplaceAllUsesIdentityComparerGivenAtConstruction()
        {
            IEnumerable<IntWrapper> originalItems = new IntWrapper[] { 1, 2, 3 };
            var replacements = new IntWrapper[] { 1, 3 };

            // NOTE: Due to conservate update check, unchanged items will appear as item changes of type update
            // NOTE2: Need to use the actual instances, since IntWrapper uses reference equality
            var expectedChanges = new ItemChange<IntWrapper>[]
            {
                new ItemChange<IntWrapper>(ChangeType.Updated, replacements[0]),
                new ItemChange<IntWrapper>(ChangeType.Updated, replacements[1]),
                new ItemChange<IntWrapper>(ChangeType.Removed, originalItems.ElementAt(1))
            };

            var loader = new ThreadSafeAsyncLoader<IntWrapper>(
                Seq.ListBased,
                loadDataAsync: _ => Task.FromResult(originalItems),
                identityComparer: new IntWrapperComparer(),
                eventContext: new RunInlineSynchronizationContext());
            loader.LoadAsync();  // load initial values
            loader.CollectionChanged += (s, e) =>
            {
                // Verify that the actual changes match the expected changes
                e.Should().BeEquivalentTo(expectedChanges);
            };


            loader.ReplaceAll(replacements);  // --- Perform ---
        }

        private class IntUpdateComparer : IEqualityComparer<int>
        {
            public bool Equals(int x, int y) { return x == y; }
            public int GetHashCode(int obj) { return obj.GetHashCode(); }
        }

        [Test]
        public void ReplaceAllUsesUpdateComparerGivenAtConstruction()
        {
            IEnumerable<int> originalItems = new int[] { 1, 2, 3 };
            var replacements = new int[] { 1, 3 };

            // Since equal int values will now count as unchanged, we do not expect item changes that are updates
            var expectedChanges = new ItemChange<int>[] { new ItemChange<int>(ChangeType.Removed, 2) };

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: _ => Task.FromResult(originalItems),
                updateComparer: new IntUpdateComparer(),
                eventContext: new RunInlineSynchronizationContext());
            loader.LoadAsync();  // load initial values
            loader.CollectionChanged += (s, e) =>
            {
                // Verify that the actual changes match the expected changes
                e.Should().BeEquivalentTo(expectedChanges);
            };


            loader.ReplaceAll(replacements);  // --- Perform ---
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
                eventContext: new RunInlineSynchronizationContext());
            loader.LoadAsync();  // load initial values


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
                eventContext: new RunInlineSynchronizationContext());
            loader.LoadAsync();  // load initial values


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
                eventContext: new RunInlineSynchronizationContext());
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
                eventContext: new RunInlineSynchronizationContext());
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
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, eventContext: new RunInlineSynchronizationContext());
            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Clear();  // --- Perform ---


            listener.DidNotReceive().Invoke(loader, Arg.Any<IntChangesAlias>());
        }
        #endregion Clear

        #region Events
        [Test]
        public void StatusChangesTwiceDuringLoad()
        {
            var statusChanges = new List<AsyncStatusTransition>();

            IEnumerable<int> initialValues = new[] { 1 };

            var loader = new ThreadSafeAsyncLoader<int>(
                Seq.ListBased,
                loadDataAsync: tok => Task.FromResult(initialValues),
                eventContext: new RunInlineSynchronizationContext());

            loader.StatusChanged += (s, e) => statusChanges.Add(e);

            loader.LoadAsync();  // load initial values

            statusChanges.Should().Equal(
                new AsyncStatusTransition(AsyncStatus.Ready, AsyncStatus.Loading),
                new AsyncStatusTransition(AsyncStatus.Loading, AsyncStatus.Ready));
        }
        #endregion Events
    }
}
