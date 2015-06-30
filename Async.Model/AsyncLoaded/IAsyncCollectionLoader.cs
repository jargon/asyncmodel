using Async.Model.Sequence;

namespace Async.Model.AsyncLoaded
{
    public interface IAsyncCollectionLoader<T> : IAsyncCollection<T>, IAsyncSeq<T>
    {
        void LoadAsync();
        void UpdateAsync();
    }
}
