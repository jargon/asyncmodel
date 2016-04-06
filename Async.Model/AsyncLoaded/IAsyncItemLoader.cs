using System;
using System.Threading.Tasks;

namespace Async.Model.AsyncLoaded
{
    public interface IAsyncItemLoader<TItem, TProgress> : IAsyncItem<TItem>
    {
        Task LoadAsync(IProgress<TProgress> progress);
        Task UpdateAsync(IProgress<TProgress> progress);
    }
}
