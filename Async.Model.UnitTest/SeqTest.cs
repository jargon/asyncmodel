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
        #region ListBasedSeq
        [Test]
        public void ListBasedSeqCreatesSeqOfGivenItems()
        {
            var seq = Seq.ListBased(new[] { 1, 2, 3 });
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        #region Conj
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
        #endregion Conj

        #region Take
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
        #endregion Take

        #region Replace
        [Test]
        public void ReplaceOnEmptyListBasedSeqDoesNothing()
        {
            var emptySeq = Seq.ListBased(new int[0]);
            emptySeq.Replace(1, 2);

            Assert.That(emptySeq, Is.Empty);
        }

        [Test]
        public void ReplaceOnListBasedSeqWithSingleItemReplacesItemWhenMatched()
        {
            var singleton = Seq.ListBased(new[] { 1 });
            singleton.Replace(1, 2);

            Assert.That(singleton, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void ReplaceOnListBasedSeqWithSingleDoesNothingWhenNotMatched()
        {
            var singleton = Seq.ListBased(new[] { 1 });
            singleton.Replace(2, 1);

            Assert.That(singleton, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ReplaceOnListBasedSeqReplacesAllOccurrences()
        {
            var duplicates = Seq.ListBased(new[] { 1, 2, 3, 2, 4, 2 });
            duplicates.Replace(2, 3);

            Assert.That(duplicates, Is.EqualTo(new[] { 1, 3, 3, 3, 4, 3 }));
        }

        [Test]
        public void ReplaceOnListBasedSeqWithSameValueDoesNothing()
        {
            var repeated = Seq.ListBased(new[] { 1, 1, 1 });
            repeated.Replace(1, 1);

            Assert.That(repeated, Is.EqualTo(new[] { 1, 1, 1 }));
        }
        #endregion Replace
        #endregion ListBasedSeq

        #region QueueBasedSeq
        [Test]
        public void QueueBasedSeqCreatesSeqOfGivenItems()
        {
            var seq = Seq.QueueBased(new[] { 1, 2, 3 });
            Assert.That(seq, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        #region Conj
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
        #endregion Conj

        #region Take
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
        #endregion Take

        #region Replace
        [Test]
        public void ReplaceOnEmptyQueueBasedSeqDoesNothing()
        {
            var emptySeq = Seq.QueueBased(new int[0]);
            emptySeq.Replace(1, 2);

            Assert.That(emptySeq, Is.Empty);
        }

        [Test]
        public void ReplaceOnQueueBasedSeqWithSingleItemReplacesItemWhenMatched()
        {
            var singleton = Seq.QueueBased(new[] { 1 });
            singleton.Replace(1, 2);

            Assert.That(singleton, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void ReplaceOnQueueBasedSeqWithSingleDoesNothingWhenNotMatched()
        {
            var singleton = Seq.QueueBased(new[] { 1 });
            singleton.Replace(2, 1);

            Assert.That(singleton, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void ReplaceOnQueueBasedSeqReplacesAllOccurrences()
        {
            var duplicates = Seq.QueueBased(new[] { 1, 2, 3, 2, 4, 2 });
            duplicates.Replace(2, 3);

            Assert.That(duplicates, Is.EqualTo(new[] { 1, 3, 3, 3, 4, 3 }));
        }

        [Test]
        public void ReplaceOnQueueBasedSeqWithSameValueDoesNothing()
        {
            var repeated = Seq.QueueBased(new[] { 1, 1, 1 });
            repeated.Replace(1, 1);

            Assert.That(repeated, Is.EqualTo(new[] { 1, 1, 1 }));
        }
        #endregion Replace
        #endregion QueueBasedSeq

        #region AsAsync
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
        public void AsAsyncReplaceDelegatesToInnerSeq()
        {
            var innerSeq = Substitute.For<ISeq<int>>();

            var asyncSeq = innerSeq.AsAsync();
            asyncSeq.Replace(1, 2);

            innerSeq.Received(1).Replace(1, 2);
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
        #endregion AsAsync

        #region SeqOverImmutableCollection
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

            public void Replace(T oldItem, T newItem)
            {
                innerList = innerList.Replace(oldItem, newItem);
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
        #endregion SeqOverImmutableCollection
    }
}
