using System;
using System.Threading.Tasks;
using Async.Model.Sequence;

namespace Async.Model.AsyncLoaded
{
    public interface IAsyncCollectionLoader<T> : IAsyncCollection<T>, IAsyncSeq<T>
    {
        Task LoadAsync();
        Task UpdateAsync();

        // TODO: This does not belong here, but I don't want to implement it for all seqs
        // Will correct placement once we drop the seq abstraction
        void Replace(Func<T, bool> predicate, T replacement);
    }
}
