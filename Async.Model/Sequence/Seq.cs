using Nito.AsyncEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
                if (list == null)
                    throw new ArgumentNullException("list");

                this.list = list;
            }

            public void Conj(T item)
            {
                list.Add(item);
            }

            public T Take()
            {
                var item = list[0];
                list.RemoveAt(0);
                return item;
            }

            public void ReplaceAll(IEnumerable<T> newItems)
            {
                list.Clear();
                list.AddRange(newItems);
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
            private Queue<T> queue;

            public QueueSeq(Queue<T> queue)
            {
                if (queue == null)
                    throw new ArgumentNullException("queue");

                this.queue = queue;
            }

            public void Conj(T item)
            {
                queue.Enqueue(item);
            }

            public T Take()
            {
                return queue.Dequeue();
            }

            public void ReplaceAll(IEnumerable<T> newItems)
            {
                this.queue = new Queue<T>(newItems);
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

        public static IAsyncSeq<T> AsAsync<T>(this ISeq<T> seq)
        {
            return new AsyncWrapper<T>(seq);
        }

        private class AsyncWrapper<T> : IAsyncSeq<T>
        {
            private readonly ISeq<T> innerSeq;

            public AsyncWrapper(ISeq<T> seq)
            {
                if (seq == null)
                    throw new ArgumentNullException("seq");

                this.innerSeq = seq;
            }

            public Task<T> TakeAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Take());
            }

            public Task ConjAsync(T item, CancellationToken cancellationToken)
            {
                Conj(item);
                return TaskConstants.Completed;
            }

            public T Take()
            {
                return innerSeq.Take();
            }

            public void Conj(T item)
            {
                innerSeq.Conj(item);
            }

            public void ReplaceAll(IEnumerable<T> newItems)
            {
                innerSeq.ReplaceAll(newItems);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return innerSeq.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return innerSeq.GetEnumerator();
            }
        }
    }
}
