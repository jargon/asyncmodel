using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Schedulers;
using Async.Model.AsyncLoaded;
using Async.Model.Sequence;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
// Because noone wants to type out this every time...
using EnumerableOfIntegerChangesAlias = System.Collections.Generic.IEnumerable<Async.Model.IItemChange<int>>;

namespace Async.Model.UnitTest.AsyncLoaded
{
    [TestFixture]
    public class ThreadSafeAsyncLoaderTest
    {
        [Test]
        public void CollectionChangedHandlerInvokedForReplace()
        {
            IEnumerable<int> loadedInts = new[] { 2 };
            var loader = new ThreadSafeAsyncLoader<int>(Seq.ListBased, loadDataAsync: tok => Task.FromResult(loadedInts),
                eventScheduler: new CurrentThreadTaskScheduler());
            loader.LoadAsync();

            var listener = Substitute.For<CollectionChangedHandler<int>>();
            loader.CollectionChanged += listener;


            loader.Replace(2, 1);   // --- Perform ---


            listener.Received().Invoke(loader, Fluent.Match<EnumerableOfIntegerChangesAlias>(changes =>
                changes.Should().ContainSingle().Which.ShouldBeEquivalentTo(new ItemChange<int>(ChangeType.Updated, 1))));
        }
    }
}
