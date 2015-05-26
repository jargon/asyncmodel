using System;

namespace Async.Model
{
    public struct EntityTag<TKey, TTag>
    {
        public readonly TKey key;
        public readonly TTag tag;

        public EntityTag(TKey key, TTag tag)
        {
            this.key = key;
            this.tag = tag;
        }
    }
}
