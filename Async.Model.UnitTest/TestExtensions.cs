using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Async.Model.UnitTest
{
    public static class TestExtensions
    {
        public static bool WasAdded<T>(this IItemChange<T> change, T item)
        {
            return change.Type == ChangeType.Added && change.Item.Equals(item);
        }

        public static bool WasAdded<T>(this IEnumerable<IItemChange<T>> changes, IEnumerable<T> items)
        {
            var itemMatches = changes.FullOuterJoin(items, l => l.Item, r => r, (l, r, k) => l != null && r != null);
            return changes.All(c => c.Type == ChangeType.Added)
                && itemMatches.Aggregate(true, (acc, val) => acc && val);
        }
    }
}
