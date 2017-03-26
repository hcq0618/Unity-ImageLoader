// hcq 2017/3/23
using System.IO;
using System;
using UnityEngine;

namespace UnityImageLoader.Cache
{
    public abstract class AbstractDiscCache : ICache<byte[]>
    {
        protected internal readonly string cachePath;

        protected AbstractDiscCache(string cachePath)
        {
            this.cachePath = cachePath;
        }

        public abstract void Set(string url, byte[] data);
        public abstract byte[] Get(string url);

        public virtual void Access(string url)
        {
            string path = GetPath(url);
            if (File.Exists(path))
            {
                File.SetLastAccessTime(path, DateTime.Now);
            }
        }

        public virtual string GetPath(string url)
        {
            return cachePath + Animator.StringToHash(url);
        }
    }
}
