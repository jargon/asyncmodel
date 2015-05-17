using NUnit.Framework;
using Async.Model;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Async.Model.UnitTest
{
    [TestFixture]
    public class FullOuterJoinTest
    {
        [Test]
        public void CanJoinEmptySequences()
        {
            var left = Enumerable.Empty<string>();
            var right = Enumerable.Empty<string>();

            var join = left.FullOuterJoin(right, l => l, r => r, (l, r, k) => String.Concat(l, r));
            var result = join.ToArray();

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void CanJoinLeftToEmptySequence()
        {
            var left = new[] { 1 };
            var right = Enumerable.Empty<int>();

            var result = left
                .FullOuterJoin(right, l => l, r => r, (l, r, k) => l + r, EqualityComparer<int>.Default, 0, 1)
                .ToArray();

            Assert.That(result, Is.Not.Empty);
            Assert.That(result.Single(), Is.EqualTo(2));
        }

        [Test]
        public void CanJoinRightToEmptySequence()
        {
            var left = Enumerable.Empty<int>();
            var right = new[] { 1, 2, 3 };

            var result = left
                .FullOuterJoin(right, l => l, r => r, (l, r, k) => l + r)
                .ToArray();

            Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void CanJoinSequencesWithAllMatchingKeys()
        {
            var left = new[] { 1, 2, 3, 4, 5 };
            var right = left.Reverse();

            var result = left
                .FullOuterJoin(right, l => l, r => r, (l, r, k) => l + r)
                .ToArray();

            Assert.That(result, Is.EqualTo(new[] { 2, 4, 6, 8, 10 }));
        }

        [Test]
        public void CanJoinSequencesWithNoMatchingKeys()
        {
            var left = new int?[] { 1, 2, 3, 4, 5 };
            var right = left.Select(i => i + 5);

            var result = left
                .FullOuterJoin(right, l => l, r => r, (l, r, k) => l ?? r ?? -1)
                .ToArray();

            Assert.That(result, Is.EqualTo(new int?[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        }

        [Test]
        public void CanJoinSequencesWithSomeMatchingKeys()
        {
            var left = new int?[] { 1, 2, -3, 4, 5 };
            var right = new int?[] { 1, 2, 30, -40, 50 };

            var result = left
                .FullOuterJoin(right, l => l, r => r, (l, r, k) => k)
                .ToArray();

            Assert.That(result, Is.EqualTo(new int?[] { 1, 2, -3, 4, 5, 30, -40, 50 }));
        }
    }
}
