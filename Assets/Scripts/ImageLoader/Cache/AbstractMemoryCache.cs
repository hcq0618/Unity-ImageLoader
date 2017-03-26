// hcq 2017/3/23
using UnityEngine;

namespace UnityImageLoader.Cache
{
    public abstract class AbstractMemoryCache : ICache<Sprite>
    {
        const long DEFAULT_CAPACITY = 1024 * 1024 * 16;

        protected internal readonly long capacity;

        protected AbstractMemoryCache() : this(DEFAULT_CAPACITY)
        {
        }

        protected AbstractMemoryCache(long capacity)
        {
            this.capacity = capacity > 0 ? capacity : DEFAULT_CAPACITY;
        }

        public abstract Sprite Get(string url);
        public abstract void Set(string url, Sprite value);
    }
}
