using Async.Model.Sequence;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.UnitTest
{
    [TestFixture]
    public class SeqTest
    {
        [Test]
        public void ListBasedSeqCreatesSeqOfGivenItems()
        {
            var seq = Seq.ListBased(new[] { 1, 2, 3 });
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ConjOnListBasedSeqAddsToEnd()
        {
            var seq = Seq.ListBased(new[] { 1, 2, 3 });
            seq = seq.Conj(4);
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void CanConjOnEmptyListBasedSeq()
        {
            var seq = Seq.ListBased(new int[0]);
            seq = seq.Conj(1);
            Assert.That(seq, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TakeOnListBasedSeqRemovesFromStart()
        {
            var seq = Seq.ListBased(new[] { 1, 2, 3 });
            var take = seq.Take();
            Assert.That(take.First, Is.EqualTo(1));
            Assert.That(take.Rest, Is.EqualTo(new[] { 2, 3 }));
        }

        // TODO: Is this the preferred behaviour?
        [Test]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void TakeOnEmptyListBasedSeqFails()
        {
            var seq = Seq.ListBased(new int[0]);
            seq.Take();
        }

        [Test]
        public void QueueBasedSeqCreatesSeqOfGivenItems()
        {
            var seq = Seq.QueueBased(new[] { 1, 2, 3 });
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ConjOnQueueBasedSeqEnqueuesToEnd()
        {
            var seq = Seq.QueueBased(new[] { 1, 2, 3 });
            seq = seq.Conj(4);
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void CanConjOnEmptyQueueBasedSeq()
        {
            var seq = Seq.QueueBased(new int[0]);
            seq = seq.Conj(1);
            Assert.That(seq, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TakeOnQueueBasedSeqDequeuesFromStart()
        {
            var seq = Seq.QueueBased(new[] { 1, 2, 3 });
            var take = seq.Take();
            Assert.That(take.First, Is.EqualTo(1));
            Assert.That(take.Rest, Is.EqualTo(new[] { 2, 3 }));
        }

        // TODO: Is this the preferred behaviour?
        [Test]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TakeOnEmptyQueueBasedSeqFails()
        {
            var seq = Seq.QueueBased(new int[0]);
            seq.Take();
        }

        [Test]
        public void AsAsyncYieldsSeqWithSameItems()
        {
            var innerSeq = Seq.ListBased(new[] { 1, 2, 3 });
            var asyncSeq = innerSeq.AsAsync();
            Assert.That(asyncSeq, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void AsAsyncConjDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();
            innerSeq.Conj(1).Returns(innerSeq);

            var asyncSeq = innerSeq.AsAsync();
            var newAsyncSeq = asyncSeq.Conj(1) as IAsyncSeq<int>;

            Assert.That(newAsyncSeq, Is.SameAs(asyncSeq));

            innerSeq.Received(1).Conj(1);
        }

        [Test]
        public void AsAsyncTakeDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();
            innerSeq.Take().Returns(new TakeResult<int>(1, innerSeq));

            var asyncSeq = innerSeq.AsAsync();
            var take = asyncSeq.Take();

            Assert.That(take.First, Is.EqualTo(1));
            Assert.That(take.Rest, Is.SameAs(asyncSeq));

            innerSeq.Received(1).Take();
        }

        [Test]
        public void AsAsyncConjAsyncDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();
            innerSeq.Conj(1).Returns(innerSeq);

            var asyncSeq = innerSeq.AsAsync();
            var task = asyncSeq.ConjAsync(1, CancellationToken.None);

            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(task.Result, Is.SameAs(asyncSeq));

            innerSeq.Received(1).Conj(1);
        }

        [Test]
        public void AsAsyncTakeAsyncDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();
            innerSeq.Take().Returns(new TakeResult<int>(1, innerSeq));

            var asyncSeq = innerSeq.AsAsync();
            var task = asyncSeq.TakeAsync(CancellationToken.None);

            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(task.Result.First, Is.EqualTo(1));
            Assert.That(task.Result.Rest, Is.SameAs(asyncSeq));

            innerSeq.Received(1).Take();
        }

        [Test]
        public void CanCreateImmutableISeq()
        {
            var seq = new ImmutableListSeq<int>(ImmutableList.Create(1, 2, 3));
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3 }));

            var newSeq = seq.Conj(4);
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(newSeq, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            var take = newSeq.Take();
            Assert.That(newSeq, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(take.First, Is.EqualTo(1));
            Assert.That(take.Rest, Is.EqualTo(new[] { 2, 3, 4 }));
        }

        [Test]
        public void AsAsyncWorksOnImmutableISeq()
        {
            var innerSeq = new ImmutableListSeq<int>(ImmutableList.Create(1, 2, 3));
            var asyncSeq = innerSeq.AsAsync();

            var newAsyncSeq = asyncSeq.Conj(4);
            Assert.That(asyncSeq, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(newAsyncSeq, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            var take = newAsyncSeq.Take();
            Assert.That(newAsyncSeq, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(take.First, Is.EqualTo(1));
            Assert.That(take.Rest, Is.EqualTo(new[] { 2, 3, 4 }));

            var conjTask = asyncSeq.ConjAsync(4, CancellationToken.None);
            Assert.That(conjTask.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(asyncSeq, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(conjTask.Result, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            var takeTask = conjTask.Result.TakeAsync(CancellationToken.None);
            Assert.That(takeTask.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(conjTask.Result, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(takeTask.Result.First, Is.EqualTo(1));
            Assert.That(takeTask.Result.Rest, Is.EqualTo(new[] { 2, 3, 4 }));
        }

        private class ImmutableListSeq<T> : ISeq<T>
        {
            private readonly ImmutableList<T> innerList;

            public ImmutableListSeq(ImmutableList<T> list)
            {
                this.innerList = list;
            }

            public TakeResult<T> Take()
            {
                var item = innerList[0];
                var newList = innerList.RemoveAt(0);
                return new TakeResult<T>(item, new ImmutableListSeq<T>(newList));
            }

            public ISeq<T> Conj(T item)
            {
                var newList = innerList.Add(item);
                return new ImmutableListSeq<T>(newList);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return innerList.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return innerList.GetEnumerator();
            }
        }
    }
}
