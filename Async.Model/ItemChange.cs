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

        public override bool Equals(object obj)
        {
            var other = obj as ItemChange<T>;
            return other != null &&
                this.type == other.type &&
                this.item.Equals(other.item);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return type.GetHashCode() + 7 * item.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format("ItemChange({0:G}, {1})", type, item);
        }
    }
}
