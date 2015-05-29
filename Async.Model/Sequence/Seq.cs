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
                this.list = list;
            }

            public ISeq<T> Conj(T item)
            {
                list.Add(item);
                return this;
            }

            public TakeResult<T> Take()
            {
                var item = list[0];
                list.RemoveAt(0);
                return new TakeResult<T>(item, this);
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

            public TakeResult<T> Take()
            {
                return new TakeResult<T>(queue.Dequeue(), this);
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
                this.innerSeq = seq;
            }

            public Task<TakeResult<T>> TakeAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Take());
            }

            public Task<IAsyncSeq<T>> ConjAsync(T item, CancellationToken cancellationToken)
            {
                var resultSeq = Conj(item) as IAsyncSeq<T>;
                return Task.FromResult(resultSeq);
            }

            public TakeResult<T> Take()
            {
                var result = innerSeq.Take();

                // If the inner seq is immutable, we need to return a new wrapper
                var rest = (result.Rest == innerSeq) ? this : new AsyncWrapper<T>(result.Rest);

                return new TakeResult<T>(result.First, rest);
            }

            public ISeq<T> Conj(T item)
            {
                var result = innerSeq.Conj(item);

                // If the inner seq is immutable, we need to return a new wrapper
                return (result == innerSeq) ? this : new AsyncWrapper<T>(result);
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
