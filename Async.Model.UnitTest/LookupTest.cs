using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Async.Model.UnitTest
{
    /// <summary>
    /// Someone on Stackoverflow claimed that the ILookup returned from ToLookup does not preserve iteration order of
    /// input sequence. This test aims to prove that it does indeed preserve iteration order. ToLookup uses the Lookup
    /// class defined inside System.Linq.Enumeration.cs, which keeps a linked list of groupings in insertion order and
    /// uses that for iteration.
    /// </summary>
    /// <remarks>
    /// Note that the ILookup interface does not require implementations to respect iteration order of the input
    /// sequence, so one should be careful about relying on it, since Microsoft could theoretically change it in a
    /// later version of .NET. In fact they have made another implementation in the System.Linq.Parallel namespace that
    /// does NOT preserve iteration order, since it is based on a Dictionary.
    /// </remarks>
    /// <see cref="http://stackoverflow.com/a/30289550/567000"/>
    /// <see cref="http://referencesource.microsoft.com/#System.Core/System/Linq/Enumerable.cs,cb695d4a973ef608"/>
    [TestFixture]
    public class LookupTest
    {
        [Test]
        public void ToLookupPreservesIterationOrderWithoutComparerForSortedSequence()
        {
            // First we show that order is preserved for a sorted sequence
            var numbers = new[] { 1, 2, 1, 3, 3, 4, 5 };
            var lookup = numbers.ToLookup(i => i);

            Assert.That(lookup.Select(g => g.Key), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void ToLookupPreservesIterationOrderWithoutComparerForUnsortedSequence()
        {
            // Now show that order is still respected when the input sequence is unsorted
            var numbers = new[] { 4, 2, 2, 5, 1, 4, 3 };
            var lookup = numbers.ToLookup(i => i);

            Assert.That(lookup.Select(g => g.Key), Is.EqualTo(new[] { 4, 2, 5, 1, 3 }));
        }

        class ParityComparer : IEqualityComparer<int>
        {
            public bool Equals(int x, int y)
            {
                // Coded for clarity, not efficiency
                bool xIsEven = x % 2 == 0;
                bool yIsEven = y % 2 == 0;

                return xIsEven == yIsEven;
            }

            public int GetHashCode(int obj)
            {
                // Coded for clarity, not efficiency
                bool even = obj % 2 == 0;
                return even.GetHashCode();
            }
        }

        [Test]
        public void ToLookupPreserversIterationOrderWithComparer()
        {
            // Now to show that it also works when using an equality comparer
            var numbers = new[] { 1, 2 };
            var lookup = numbers.ToLookup(i => i, new ParityComparer());

            Assert.That(lookup.Select(g => g.Key), Is.EqualTo(new[] { 1, 2 }));

            numbers = new[] { 4, 1, 4 };
            lookup = numbers.ToLookup(i => i, new ParityComparer());

            Assert.That(lookup.Select(g => g.Key), Is.EqualTo(new[] { 4, 1 }));
        }

        [Test]
        public void ToLookupPreservesIterationOrderForStrings()
        {
            // And when using strings instead of ints
            var greekLetters = new[] { "alpha", "beta", "gamma" };
            var lookup = greekLetters.ToLookup(l => l.Substring(0, 1));

            Assert.That(lookup.Select(g => g.Key), Is.EqualTo(new[] { "a", "b", "g" }));

            greekLetters = new[] { "beta", "gamma", "alpha" };
            lookup = greekLetters.ToLookup(l => l.Substring(0, 1));

            Assert.That(lookup.Select(g => g.Key), Is.EqualTo(new[] { "b", "g", "a" }));
        }
    }
}
