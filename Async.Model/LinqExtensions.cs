using System;
using System.Collections.Generic;
using System.Linq;

namespace Async.Model
{
    public static class LinqExtensions
    {
        /// <summary>
        /// Returns a new sequence in which all instances of oldItem are replaced with newItem. Instances are compared
        /// for equality using <c>EqualityComparer&lt;TSource&gt;.Default</c>.
        /// </summary>
        /// <remarks>This method is implemented using deferred execution.</remarks>
        /// <typeparam name="TSource">The type of elements of source.</typeparam>
        /// <param name="source">A sequence of values in which to replace some items.</param>
        /// <param name="oldItem">Item to be replaced.</param>
        /// <param name="newItem">Item to substitute for the old one.</param>
        /// <returns>A new sequence with all instances of oldItem replaced with newItem.</returns>
        public static IEnumerable<TSource> Replace<TSource>(this IEnumerable<TSource> source, TSource oldItem, TSource newItem)
        {
            return Replace(source, oldItem, newItem, EqualityComparer<TSource>.Default);
        }

        /// <summary>
        /// Returns a new sequence in which all instances of oldItem are replaced with newItem. Instances are compared
        /// for equality using the given comparer.
        /// </summary>
        /// <remarks>This method is implemented using deferred execution.</remarks>
        /// <typeparam name="TSource">The type of elements of source.</typeparam>
        /// <param name="source">A sequence of values in which to replace some items.</param>
        /// <param name="oldItem">Item to be replaced.</param>
        /// <param name="newItem">Item to substitute for the old one.</param>
        /// <param name="comparer">Comparer to use when matching oldItem against elements of the sequence.</param>
        /// <returns>A new sequence with all instances of oldItem replaced with newItem.</returns>
        public static IEnumerable<TSource> Replace<TSource>(
            this IEnumerable<TSource> source, TSource oldItem, TSource newItem, IEqualityComparer<TSource> comparer)
        {
            // TODO: Use nameof operator once we upgrade to C# 6.0
            if (source == null) throw new ArgumentNullException("source");
            if (comparer == null) throw new ArgumentNullException("comparer");
            
            return source.Select(item =>
            {
                if (comparer.Equals(item, oldItem))
                    return newItem;
                else
                    return item;
            });
        }

        public static IEnumerable<TResult> FullOuterJoin<TLeft, TRight, TKey, TResult>(
            this IEnumerable<TLeft> left,
            IEnumerable<TRight> right,
            Func<TLeft, TKey> leftKeySelector,
            Func<TRight, TKey> rightKeySelector,
            Func<TLeft, TRight, TKey, TResult> resultSelector,
            IEqualityComparer<TKey> comparator = null,
            TLeft defaultLeft = default(TLeft),
            TRight defaultRight = default(TRight))
        {
            if (left == null) throw new ArgumentNullException("left");
            if (right == null) throw new ArgumentNullException("right");
            if (leftKeySelector == null) throw new ArgumentNullException("leftKeySelector");
            if (rightKeySelector == null) throw new ArgumentNullException("rightKeySelector");
            if (resultSelector == null) throw new ArgumentNullException("resultSelector");

            comparator = comparator ?? EqualityComparer<TKey>.Default;
            return FullOuterJoinIterator(left, right, leftKeySelector, rightKeySelector, resultSelector, comparator, defaultLeft, defaultRight);
        }

        internal static IEnumerable<TResult> FullOuterJoinIterator<TLeft, TRight, TKey, TResult>(
            IEnumerable<TLeft> left,
            IEnumerable<TRight> right,
            Func<TLeft, TKey> leftKeySelector,
            Func<TRight, TKey> rightKeySelector,
            Func<TLeft, TRight, TKey, TResult> resultSelector,
            IEqualityComparer<TKey> comparator,
            TLeft defaultLeft,
            TRight defaultRight)
        {
            var leftLookup = left.ToLookup(leftKeySelector, comparator);
            var rightLookup = right.ToLookup(rightKeySelector, comparator);
            var keys = leftLookup.Select(g => g.Key).Union(rightLookup.Select(g => g.Key), comparator);

            foreach (var key in keys)
                foreach (var leftValue in leftLookup[key].DefaultIfEmpty(defaultLeft))
                    foreach (var rightValue in rightLookup[key].DefaultIfEmpty(defaultRight))
                        yield return resultSelector(leftValue, rightValue, key);
        }

        /// <summary>
        /// Calculates the changes between the new sequence and the old sequence and returns the
        /// result as a sequence of <see cref="ItemChange{T}"/>. Item ordering is ignored, so the
        /// two sequences are effectively treated as mathematical sets.
        /// </summary>
        /// <typeparam name="TSource">The type of items in <paramref name="newItems"/> and <paramref name="oldItems"/>.</typeparam>
        /// <param name="newItems">The input sequence of new items.</param>
        /// <param name="oldItems">The input sequence of old items to calculate changes against.</param>
        /// <param name="identityComparer">The <see cref="IEqualityComparer{T}"/> to use when determining if two items are versions of the same logical object.</param>
        /// <param name="updateComparer">The <see cref="IEqualityComparer{T}"/> to use when determining if two items are the same version of the same logical object.</param>
        /// <returns>A sequence of <see cref="ItemChange{T}"/> that describes all changes from the old sequence to the new.</returns>
        public static IEnumerable<ItemChange<TSource>> ChangesFrom<TSource>(
            this IEnumerable<TSource> newItems,
            IEnumerable<TSource> oldItems,
            IEqualityComparer<TSource> identityComparer = null,
            IEqualityComparer<TSource> updateComparer = null)
        {
            if (newItems == null) throw new ArgumentNullException("newItems");
            if (oldItems == null) throw new ArgumentNullException("oldItems");

            identityComparer = identityComparer ?? EqualityComparer<TSource>.Default;
            updateComparer = updateComparer ?? EqualityComparer<TSource>.Default;

            return ChangesFromIterator(newItems, oldItems, identityComparer, updateComparer);
        }

        internal static IEnumerable<ItemChange<T>> ChangesFromIterator<T>(
            IEnumerable<T> newItems,
            IEnumerable<T> oldItems,
            IEqualityComparer<T> identityComparer,
            IEqualityComparer<T> updateComparer)
        {
            // Ensure fast lookup of items
            Dictionary<T, T> newDict = null;
            Dictionary<T, T> oldDict = null;

            try
            {
                newDict = newItems.ToDictionary(item => item, identityComparer);
                oldDict = oldItems.ToDictionary(item => item, identityComparer);
            }
            catch (ArgumentException)
            {
                // We will get an ArgumentException in case of duplicates in newItems or oldItems

                // If newDict has not been set, then newItems caused the error, otherwise it must be oldItems
                // TODO: Use nameof operator when we upgrade to C# 6.0
                var argumentName = (newDict == null) ? "newItems" : "oldItems";
                throw new ArgumentException("Duplicates not allowed", argumentName);
            }

            // Make a pass through the old items to find updates and removals
            foreach (var oldItem in oldDict.Keys)
            {
                if (newDict.ContainsKey(oldItem))
                {
                    var newItem = newDict[oldItem];
                    if (!updateComparer.Equals(newItem, oldItem))
                        yield return new ItemChange<T>(ChangeType.Updated, newItem);
                }
                else
                {
                    yield return new ItemChange<T>(ChangeType.Removed, oldItem);
                }
            }

            // Make a pass through the new items to find additions
            foreach (var newItem in newDict.Keys)
            {
                if (!oldDict.ContainsKey(newItem))
                    yield return new ItemChange<T>(ChangeType.Added, newItem);
            }
        }
    }
}
