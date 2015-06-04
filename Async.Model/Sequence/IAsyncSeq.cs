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
        Task<T> TakeAsync(CancellationToken cancellationToken);
        Task ConjAsync(T item, CancellationToken cancellationToken);
    }
}
