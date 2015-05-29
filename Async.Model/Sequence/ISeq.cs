using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Async.Model.Sequence
{
    public interface ISeq<T> : IEnumerable<T>
    {
        // TODO: Is it okay to call First on empty seq?
        T First();
        ISeq<T> Conj(T item);
    }
}
