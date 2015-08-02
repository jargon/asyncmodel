using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace Async.Model.UnitTest
{
    [TestFixture]
    public class LinqExtensionsTest
    {
        [Test]
        public void ChangesFromReturnsNoChangesForEmptySequences()
        {
            var changes = Enumerable.Empty<int>()
                .ChangesFrom(Enumerable.Empty<int>());  // --- Perform ---

            changes.Should().BeEmpty("because there can be no changes between two empty sequences");
        }

        [Test]
        public void ChangesFromReturnsNoChangesForSingletonSequenceComparedToItself()
        {
            var sequence = new int[] { 1 };
            var changes = sequence.ChangesFrom(sequence);  // --- Perform ---

            changes.Should().BeEmpty("because there can be no changes between a sequence and itself");
        }

        [Test]
        public void ChangesFromReturnsNoChangesForSequenceComparedToItself()
        {
            var sequence = new int[] { 1, 2, 3, 4 };
            var changes = sequence.ChangesFrom(sequence);  // --- Perform ---

            changes.Should().BeEmpty("because there can be no changes between a sequence and itself");
        }

        [Test]
        public void ChangesFromReturnsCorrectChangesForDifferentSingletonSequences()
        {
            // Precondition: ItemChange implements Equals in natural way
            var change1 = new ItemChange<int>(ChangeType.Added, 1);
            var change2 = new ItemChange<int>(ChangeType.Added, 1);
            change1.Should().Be(change2);

            var oldSeq = new int[] { 1 };
            var newSeq = new int[] { 2 };
            IEnumerable<IItemChange<int>> expectedChanges = new[]
            {
                new ItemChange<int>(ChangeType.Removed, 1),
                new ItemChange<int>(ChangeType.Added, 2)
            };

            var changes = newSeq.ChangesFrom(oldSeq);  // --- Perform ---

            changes.Should().BeEquivalentTo(expectedChanges);
        }

        [Test]
        public void ChangesFromReturnsExpectedChangesForDifferentSequences()
        {
            var oldSeq = new int[] { 1, 2, 3, 4 };
            var newSeq = new int[] { 3, 4, 5, 6 };
            IEnumerable<IItemChange<int>> expectedChanges = new[]
            {
                new ItemChange<int>(ChangeType.Removed, 1),
                new ItemChange<int>(ChangeType.Removed, 2),
                new ItemChange<int>(ChangeType.Added, 5),
                new ItemChange<int>(ChangeType.Added, 6)
            };

            var actualChanges = newSeq.ChangesFrom(oldSeq);  // --- Perform ---
            actualChanges.Should().BeEquivalentTo(expectedChanges);
        }

        [Test]
        public void ChangesFromRejectsNullOldItems()
        {
            var newSeq = new int[] { 1 };

            // --- Perform ---
            Action callingChangesFrom = () => newSeq.ChangesFrom(null);
            callingChangesFrom.ShouldThrow<ArgumentNullException>().WithMessage("*oldItems*");
        }

        [Test]
        public void ChangesFromRejectsNullNewItems()
        {
            var oldSeq = new int[] { 1 };
            IEnumerable<int> newSeq = null;

            // --- Perform ---
            Action callingChangesFrom = () => newSeq.ChangesFrom(oldSeq);
            callingChangesFrom.ShouldThrow<ArgumentNullException>().WithMessage("*newItems*");
        }

        [Test]
        [Category("Limitations")]
        public void ChangesFromDoesNotAllowDuplicatesInOldItems()
        {
            var oldSeq = new int[] { 1, 2, 1 };
            var newSeq = new int[] { 1, 2, 3 };

            // --- Perform ---
            Func<IEnumerable> changesIterator = () => newSeq.ChangesFrom(oldSeq);
            // We need to force enumeration, since ChangesFrom uses deferred execution and duplicates are discovered lazily
            changesIterator.Enumerating()
                .ShouldThrow<ArgumentException>("because duplicates are forbidden")
                .WithMessage("*oldItems*", "because error message should name faulty parameter");
        }

        [Test]
        [Category("Limitations")]
        public void ChangesFromDoesNotAllowDuplicatesInNewItems()
        {
            var oldSeq = new int[] { 1, 2, 3 };
            var newSeq = new int[] { 1, 1 };

            // --- Perform ---
            Func<IEnumerable> changesIterator = () => newSeq.ChangesFrom(oldSeq);
            changesIterator.Enumerating()
                .ShouldThrow<ArgumentException>("because duplicates are forbidden")
                .WithMessage("*newItems*", "because error message should name faulty parameter");
        }
    }
}
