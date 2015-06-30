using System;
using System.Collections.Generic;
using System.Linq;

namespace Async.Model
{
    public static class LinqExtensions
    {
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
            where TNew : class
            where TOld : class
            where TKey : class
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

                if (newKey != oldKey && !updateComparer.Equals(newKey, oldKey))
                    return new ItemChange<TKey>(ChangeType.Updated, newItemKeySelector(n));

                return new ItemChange<TKey>(ChangeType.Unchanged, newKey);

            }, identityComparer);
        }
    }
}
