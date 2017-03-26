using System.Threading;
using UnityEngine;

namespace UnityImageLoader.Cache
{
    public class LRUMemoryCache : AbstractMemoryCache
    {
        readonly ReaderWriterLockSlim lockslim;
        long size;

        readonly LinkedDictionary<string, Sprite> linkedDictionary;

        public LRUMemoryCache(long capacity) : base(capacity)
        {
            linkedDictionary = new LinkedDictionary<string, Sprite>();
            lockslim = new ReaderWriterLockSlim();
        }

        public override void Set(string url, Sprite value)
        {
            if (value == null)
            {
                return;
            }

            Sprite previous;
            if (linkedDictionary.TryGet(url, out previous))
            {
                size -= ToSize(previous);
            }

            size += ToSize(value);
            linkedDictionary.Set(url, value);

            TrimToSize();
        }

        public override Sprite Get(string url)
        {
            Sprite result;
            linkedDictionary.TryGet(url, out result);
            return result;
        }

        public int ToSize(Sprite value)
        {
            Texture2D texture = value.texture;
            return texture.width * texture.height * 4;
        }

        void TrimToSize()
        {
            while (true)
            {
                lockslim.EnterWriteLock();
                try
                {
                    if (linkedDictionary.Count == 0)
                    {
                        break;
                    }

                    if (size <= capacity)
                    {
                        break;
                    }

                    string tailKey = linkedDictionary.GetTailKey();
                    Sprite tailValue;
                    if (linkedDictionary.TryGet(tailKey, out tailValue))
                    {
                        size -= ToSize(tailValue);
                        linkedDictionary.RemoveLast();
                    }
                }
                finally
                {
                    lockslim.ExitWriteLock();
                }
            }
        }
    }
}

