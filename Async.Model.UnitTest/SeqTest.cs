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
            seq.Conj(4);
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void CanConjOnEmptyListBasedSeq()
        {
            var seq = Seq.ListBased(new int[0]);
            seq.Conj(1);
            Assert.That(seq, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TakeOnListBasedSeqRemovesFromStart()
        {
            var seq = Seq.ListBased(new[] { 1, 2, 3 });
            var item = seq.Take();
            Assert.That(item, Is.EqualTo(1));
            Assert.That(seq, Is.EqualTo(new[] { 2, 3 }));
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
            seq.Conj(4);
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void CanConjOnEmptyQueueBasedSeq()
        {
            var seq = Seq.QueueBased(new int[0]);
            seq.Conj(1);
            Assert.That(seq, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TakeOnQueueBasedSeqDequeuesFromStart()
        {
            var seq = Seq.QueueBased(new[] { 1, 2, 3 });
            var item = seq.Take();
            Assert.That(item, Is.EqualTo(1));
            Assert.That(seq, Is.EqualTo(new[] { 2, 3 }));
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

            var asyncSeq = innerSeq.AsAsync();
            asyncSeq.Conj(1);

            innerSeq.Received(1).Conj(1);
        }

        [Test]
        public void AsAsyncTakeDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();
            innerSeq.Take().Returns(1);

            var asyncSeq = innerSeq.AsAsync();
            var item = asyncSeq.Take();

            Assert.That(item, Is.EqualTo(1));

            innerSeq.Received(1).Take();
        }

        [Test]
        public void AsAsyncConjAsyncDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();

            var asyncSeq = innerSeq.AsAsync();
            var task = asyncSeq.ConjAsync(1, CancellationToken.None);

            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));

            innerSeq.Received(1).Conj(1);
        }

        [Test]
        public void AsAsyncTakeAsyncDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();
            innerSeq.Take().Returns(1);

            var asyncSeq = innerSeq.AsAsync();
            var task = asyncSeq.TakeAsync(CancellationToken.None);

            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(task.Result, Is.EqualTo(1));

            innerSeq.Received(1).Take();
        }

        // TODO: Should we delete the following tests and supporting class, since seqs based on immutable collections
        // are now a lot less useful, given that you can no longer create an immutable seq?
        [Test]
        public void CanImplementISeqWithImmutableCollection()
        {
            var seq = new ImmutableListSeq<int>(ImmutableList.Create(1, 2, 3));
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3 }));

            seq.Conj(4);
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            var item = seq.Take();
            Assert.That(seq, Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(item, Is.EqualTo(1));
        }

        [Test]
        public void AsAsyncWorksOnImmutableCollectionBasedISeq()
        {
            var innerSeq = new ImmutableListSeq<int>(ImmutableList.Create(1, 2, 3));
            var asyncSeq = innerSeq.AsAsync();

            asyncSeq.Conj(4);
            Assert.That(asyncSeq, Is.EqualTo(new[] { 1, 2, 3, 4 }));

            var item = asyncSeq.Take();
            Assert.That(item, Is.EqualTo(1));
            Assert.That(asyncSeq, Is.EqualTo(new[] { 2, 3, 4 }));

            var conjTask = asyncSeq.ConjAsync(5, CancellationToken.None);
            Assert.That(conjTask.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(asyncSeq, Is.EqualTo(new[] { 2, 3, 4, 5 }));

            var takeTask = asyncSeq.TakeAsync(CancellationToken.None);
            Assert.That(takeTask.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(takeTask.Result, Is.EqualTo(2));
            Assert.That(asyncSeq, Is.EqualTo(new[] { 3, 4, 5 }));
        }

        private class ImmutableListSeq<T> : ISeq<T>
        {
            private ImmutableList<T> innerList;

            public ImmutableListSeq(ImmutableList<T> list)
            {
                this.innerList = list;
            }

            public T Take()
            {
                var item = innerList[0];
                innerList = innerList.RemoveAt(0);
                return item;
            }

            public void Conj(T item)
            {
                innerList = innerList.Add(item);
            }

            public void Replace(T item)
            {
                innerList = innerList.Replace(item, item);
            }

            public void ReplaceAll(IEnumerable<T> newItems)
            {
                innerList = newItems.ToImmutableList();
            }

            public void Clear()
            {
                innerList = innerList.Clear();
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
