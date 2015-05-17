namespace Async.Model
{
    public struct ItemChange<T>
    {
        public enum ChangeType
        {
            Unchanged, Added, Removed, Updated
        }

        public readonly ChangeType Type;
        public readonly T Item;

        public ItemChange(ChangeType type, T item)
        {
            this.Type = type;
            this.Item = item;
        }
    }
}
