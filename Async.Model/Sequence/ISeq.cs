using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Async.Model.Sequence
{
    public interface ISeq<T> : IEnumerable<T>
    {
        // TODO: Is it okay to call Take on empty seq?
        /// <summary>
        /// Takes the "front" item of the sequence. What item that is depends on the seq, but should always be the
        /// "natural" element. For a list-based sequence this would mean the first item in the list, whereas for a
        /// queue-based seq, it should mean a dequeue operation. After this operation, the seq will no longer contain
        /// the item.
        /// </summary>
        /// <returns>The first item of the seq.</returns>
        T Take();

        /// <summary>
        /// Conjoins the item onto the seq. Where the item is placed depends on the sequence, but should always be the
        /// "natural" position. For a list-based seq, this would mean at the end of the list, whereas for a queue-based
        /// seq, the item should be enqueued.
        /// </summary>
        /// <param name="item">Item to add to the seq.</param>
        void Conj(T item);

        /// <summary>
        /// Replaces all instances of <paramref name="oldItem"/> in the sequence with <paramref name="newItem"/>. Items
        /// are compared by whatever definition of equality the seq uses.
        /// </summary>
        /// <param name="oldItem">The item to be replaced.</param>
        /// <param name="newItem">The replacement item to use.</param>
        void Replace(T oldItem, T newItem);

        /// <summary>
        /// Replaces all items in the sequence with the given new items. This has the same effect as iterating through
        /// newItems and calling Conj for each item, except possibly more efficient. Also, if the seq is thread-safe,
        /// this operation is required to be atomic.
        /// </summary>
        /// <param name="newItems">New items to replace the existing items in the sequence.</param>
        void ReplaceAll(IEnumerable<T> newItems);

        /// <summary>
        /// Clears the sequence, leaving it empty. If the seq is thread-safe, this operation is required to be atomic.
        /// </summary>
        void Clear();
    }
}
