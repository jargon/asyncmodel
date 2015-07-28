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

        public static IEnumerable<ItemChange<TKey>> ChangesFrom<TNew, TOld, TKey>(
            this IEnumerable<TNew> newItems,
            IEnumerable<TOld> oldItems,
            Func<TNew, TKey> newItemKeySelector,
            Func<TOld, TKey> oldItemKeySelector,
            IEqualityComparer<TKey> identityComparer = null,
            IEqualityComparer<TKey> updateComparer = null)
        {
            if (newItems == null) throw new ArgumentNullException("newItems");
            if (oldItems == null) throw new ArgumentNullException("oldItems");
            if (newItemKeySelector == null) throw new ArgumentNullException("newItemKeySelector");
            if (oldItemKeySelector == null) throw new ArgumentNullException("oldItemKeySelector");

            identityComparer = identityComparer ?? EqualityComparer<TKey>.Default;
            updateComparer = updateComparer ?? EqualityComparer<TKey>.Default;

            return newItems.FullOuterJoin(oldItems, newItemKeySelector, oldItemKeySelector, (n, o, k) =>
            {
                if (n == null)
                    return new ItemChange<TKey>(ChangeType.Removed, k);
                else if (o == null)
                    return new ItemChange<TKey>(ChangeType.Added, k);

                var newKey = newItemKeySelector(n);
                var oldKey = oldItemKeySelector(o);

                if (!updateComparer.Equals(newKey, oldKey))
                    return new ItemChange<TKey>(ChangeType.Updated, newKey);

                return new ItemChange<TKey>(ChangeType.Unchanged, newKey);

            }, identityComparer);
        }
    }
}
