using System.Threading.Tasks;
using Async.Model.Sequence;

namespace Async.Model.AsyncLoaded
{
    public interface IAsyncCollectionLoader<T> : IAsyncCollection<T>, IAsyncSeq<T>
    {
        Task LoadAsync();
        Task UpdateAsync();
    }
}
