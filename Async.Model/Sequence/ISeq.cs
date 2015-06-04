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
        /// <summary>
        /// Takes the "front" item of the sequence. What item that is depends on the seq, but should always be the
        /// "natural" element. For a list-based sequence this would mean the first item in the list, whereas for a
        /// queue-based seq, it should mean a dequeue operation. The result is the taken item along with the seq less
        /// the taken item.
        /// </summary>
        /// <returns>The first element and the rest of the seq.</returns>
        T Take();

        /// <summary>
        /// Conjoins the item onto the seq. Where the item is placed depends on the sequence, but should always be the
        /// "natural" position. For a list-based seq, this would mean at the end of the list, whereas for a queue-based
        /// seq, the item should be enqueued.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        void Conj(T item);

        void ReplaceAll(IEnumerable<T> newItems);
    }
}
