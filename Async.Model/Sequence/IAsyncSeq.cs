using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Async.Model.Sequence
{
    public interface IAsyncSeq<T> : ISeq<T>
    {
        // TODO: Is it okay to call First on empty seq?
        Task<TakeResult<T>> TakeAsync(CancellationToken cancellationToken);
        Task<IAsyncSeq<T>> ConjAsync(T item, CancellationToken cancellationToken);
    }
}
