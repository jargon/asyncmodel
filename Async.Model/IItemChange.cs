using System;

namespace Async.Model
{
    public enum ChangeType
    {
        Unchanged, Added, Removed, Updated
    }

    public interface IItemChange<out T>
    {
        T Item { get; }
        ChangeType Type { get; }
    }
}
