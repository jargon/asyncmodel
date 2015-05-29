using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Async.Model.Sequence
{
    public static class Seq
    {
        public static ISeq<T> ListBased<T>(IEnumerable<T> items)
        {
            return new ListSeq<T>(new List<T>(items));
        }

        private class ListSeq<T> : ISeq<T>
        {
            private readonly List<T> list;

            public ListSeq(List<T> list)
            {
                this.list = list;
            }

            public ISeq<T> Conj(T item)
            {
                list.Add(item);
                return this;
            }

            public T First()
            {
                // TODO: Should we remove item from list or is it ok for ListSeq.First to be a peek vs QueueSeq.First that is a pop?
                return list[0];
            }

            public IEnumerator<T> GetEnumerator()
            {
                return list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return list.GetEnumerator();
            }
        }

        public static ISeq<T> QueueBased<T>(IEnumerable<T> items)
        {
            return new QueueSeq<T>(new Queue<T>(items));
        }

        private class QueueSeq<T> : ISeq<T>
        {
            private readonly Queue<T> queue;

            public QueueSeq(Queue<T> queue)
            {
                this.queue = queue;
            }

            public ISeq<T> Conj(T item)
            {
                queue.Enqueue(item);
                return this;
            }

            public T First()
            {
                return queue.Dequeue();
            }

            public IEnumerator<T> GetEnumerator()
            {
                return queue.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return queue.GetEnumerator();
            }
        }

        public static Func<IEnumerable<TItem>, ISeq<TItem>> DelegateBased<TItem, TCollection>(
            Func<IEnumerable<TItem>, TCollection> collFactory,
            Func<TCollection, TItem> first,
            Func<TCollection, TItem, TCollection> conj)
            where TCollection : IEnumerable<TItem>
        {
            return items => new DelegateSeq<TItem, TCollection>(collFactory(items), first, conj);
        }

        private class DelegateSeq<TItem, TCollection> : ISeq<TItem> where TCollection : IEnumerable<TItem>
        {
            private readonly Func<TCollection, TItem> first;
            private readonly Func<TCollection, TItem, TCollection> conj;
            private TCollection items;

            public DelegateSeq(TCollection items, Func<TCollection, TItem> first, Func<TCollection, TItem, TCollection> conj)
            {
                this.first = first;
                this.conj = conj;
                this.items = items;
            }

            public ISeq<TItem> Conj(TItem item)
            {
                items = conj(items, item);
                return this;
            }

            public TItem First()
            {
                return first(items);
            }

            public IEnumerator<TItem> GetEnumerator()
            {
                return items.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return items.GetEnumerator();
            }
        }
    }
}
