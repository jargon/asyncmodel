using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public interface IAsyncItemLoader<T> : IAsyncItem<T>
    {
        Task LoadAsync();
        Task UpdateAsync();
    }
}
