using System.IO;
using System;
using System.Collections;
using UnityImageLoader.Utils;

namespace UnityImageLoader.Cache
{
    public class LRUDiscCache : AbstractDiscCache
    {

        public LRUDiscCache(string cachePath) : base(cachePath)
        {
        }

        public override void Set(string url, byte[] data)
        {
            if (data == null || data.Length <= 0)
            {
                return;
            }

            string path = GetPath(url);

            if (!File.Exists(path))
            {
                long avaliableBytes = Device.GetSDCardAvaliableBytes();

                if (data.Length > avaliableBytes)
                {
                    RemoveCache();

                    if (data.Length <= avaliableBytes)
                    {
                        File.WriteAllBytes(path, data);
                    }
                }
                else
                {
                    File.WriteAllBytes(path, data);
                }

            }
            else
            {
                File.SetLastAccessTime(path, DateTime.Now);
            }


        }

        void RemoveCache()
        {
            DirectoryInfo folder = new DirectoryInfo(cachePath);
            FileInfo[] files = folder.GetFiles();
            
            if (files.Length > 1)
            {
                Array.Sort(files, new FileDateSort());
                int deleteNum = (int)(files.Length * 0.4) + 1;
            
                for (int i = 0; i < deleteNum; i++)
                {
                    files[i].Delete();
                }
            }
            else if (files.Length == 1)
            {
                files[0].Delete();
            }

        }

        public override byte[] Get(string url)
        {
            string path = GetPath(url);
            if (File.Exists(path))
            {
                byte[] data = File.ReadAllBytes(path);
                File.SetLastAccessTime(path, DateTime.Now);
                return data;
            }
            return null;
        }

        protected internal class FileDateSort : IComparer
        {
            #region IComparer Members

            public int Compare(object x, object y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                if (x == null)
                {
                    return -1;
                }
                if (y == null)
                {
                    return 1;
                }
                FileInfo xInfo = (FileInfo)x;
                FileInfo yInfo = (FileInfo)y;


                return xInfo.LastAccessTime.CompareTo(yInfo.LastAccessTime);

            }

            #endregion

        }

    }
}

