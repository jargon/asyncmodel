namespace Async.Model
{
    public class ItemChange<T> : IItemChange<T>
    {
        private readonly ChangeType type;
        private readonly T item;

        public ChangeType Type { get { return type; } }
        public T Item { get { return item; } }

        public ItemChange(ChangeType type, T item)
        {
            this.type = type;
            this.item = item;
        }
    }
}
