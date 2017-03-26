using UnityEngine;
using System;
using System.IO;
using UnityEngine.UI;
using UnityImageLoader.Cache;
using UnityImageLoader.Utils;
using UnityImageLoader.Http;

namespace UnityImageLoader
{
    public class ImageLoader
    {
        static ImageLoader instance;
        readonly static object locker = new object();

        AbstractHttp httpImpl;

        AbstractMemoryCache _memoryCache;
        AbstractMemoryCache memoryCache
        {
            get
            {
                if (_memoryCache == null)
                {
                    long maxMemory = Device.GetMaxMemory();
                    _memoryCache = new LRUMemoryCache(maxMemory / 8);
                }

                return _memoryCache;
            }

            set
            {
                _memoryCache = value;
            }
        }

        AbstractDiscCache _discCache;
        AbstractDiscCache discCache
        {
            get
            {
                if (_discCache == null)
                {
                    if (Device.IsExistSDCard())
                    {
                        string cachePath = Device.GetExternalCacheDir() + "/image_cache/";

                        if (!Directory.Exists(cachePath))
                        {
                            Directory.CreateDirectory(cachePath);
                        }

                        _discCache = new LRUDiscCache(cachePath);
                    }
                }

                return _discCache;
            }

            set
            {
                _discCache = value;
            }
        }

        ImageLoader() { }

        #region public method

        public static ImageLoader GetInstance()
        {
            if (instance == null)
            {
                lock (locker)
                {
                    if (instance == null)
                    {
                        instance = new ImageLoader();
                    }
                }
            }

            return instance;
        }

        public ImageLoader SetHttpImpl(AbstractHttp impl)
        {
            httpImpl = impl;
            return this;
        }

        public ImageLoader SetMemoryCache(AbstractMemoryCache impl)
        {
            memoryCache = impl;
            return this;
        }

        public AbstractMemoryCache GetMemoryCache()
        {
            return memoryCache;
        }

        public ImageLoader SetDiscCache(AbstractDiscCache impl)
        {
            discCache = impl;
            return this;
        }

        public AbstractDiscCache GetDiscCache()
        {
            return discCache;
        }

        public bool DisplayFromMemory(Image image, string uri, params DisplayOption[] option)
        {
            DisplayOption opt = GetOptionFromParams(option);

            Sprite sprite = memoryCache.Get(uri);
            if (opt.isMemoryCache && sprite != null)
            {
                //LogUtils.Log("image load from memory cache");
                Display(image, sprite);
                return true;
            }

            return false;
        }

        public bool Display(byte[] data, Image image, string uri, params DisplayOption[] option)
        {
            DisplayOption opt = GetOptionFromParams(option);

            if (DisplayFromMemory(image, uri, option))
            {
                return true;
            }

            return DisplayFromBytes(data, image, uri, opt);
        }

        public void Display(Image image, string uri)
        {
            Display(image, uri, null);
        }

        public void Display(Image image, string uri, params DisplayOption[] option)
        {
            DisplayOption opt = GetOptionFromParams(option);

            if (string.IsNullOrEmpty(uri))
            {
                DisplayErrorImage(image, opt);

                return;
            }

            if (DisplayFromMemory(image, uri, option))
            {
                return;
            }

            Uri _uri = new Uri(uri);

            if (_uri.IsFile)
            {
                DisplayFromFile(image, uri, option);
            }
            else
            {
                DisplayLoadingImage(image, opt);

                if (opt.isDiscCache)
                {
                    discCache.Access(uri);

                    DisplayFromDiscCache(image, uri, opt);
                }
                else
                {
                    DisplayFromHttp(image, uri, opt);
                }
            }
        }

        public void Display(Image image, Sprite sprite)
        {
            Color color = image.color;
            color.a = 1;
            image.color = color;
            image.overrideSprite = sprite;
        }

        public Sprite Display(Image image, Texture2D texture)
        {
            if (texture == null)
            {
                DisplayTransparent(image);

                return null;
            }

            Sprite sprite = Convert2Sprite(texture);
            Display(image, sprite);

            return sprite;
        }

        //Not consider to memory cache,just display from file immediately
        public void DisplayFromFile(Image image, string path, params DisplayOption[] option)
        {
            DisplayOption opt = GetOptionFromParams(option);

            DisplayLoadingImage(image, opt);

            if (File.Exists(path))
            {
                AsyncTask.GetInstance()
                         .SetDoInBackground(delegate
                {

                    byte[] data = File.ReadAllBytes(path);
                    return data;

                }).SetOnPostExecute(result => DisplayFromBytes((byte[])result, image, path, opt))
                         .Excute();
            }
            else
            {
                DisplayErrorImage(image, opt);
            }
        }

        public Sprite Convert2Sprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        }

        public Texture2D Convert2Texture(int width, int height, byte[] data)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.LoadImage(data);

            return texture;
        }

        #endregion

        DisplayOption GetOptionFromParams(params DisplayOption[] option)
        {
            if (option == null || option.Length <= 0)
            {
                return DisplayOption.GetDefaultDisplayOption();
            }
            else
            {
                return option[0];
            }
        }

        void DisplayFromDiscCache(Image image, string uri, DisplayOption option)
        {
            if (discCache != null)
            {
                AsyncTask.GetInstance()
                         .SetDoInBackground(delegate
                {
                    return discCache.Get(uri);
                }).SetOnPostExecute(delegate (object result)
                {
                    byte[] data = (byte[])result;
                    if (DisplayFromBytes(data, image, uri, option))
                    {
                        //LogUtils.Log("image load from disc cache");
                    }
                    else
                    {
                        DisplayFromHttp(image, uri, option);
                    }

                }).Excute();
            }
        }

        void DisplayFromHttp(Image image, string uri, DisplayOption option)
        {
            if (httpImpl == null)
            {
                httpImpl = new BestHttpImpl();
            }

            httpImpl.Get(uri, delegate (byte[] response, int statusCode, bool isSuccess)

             {
                 //LogUtils.Log("image load from net");
                 if (isSuccess)
                 {
                     if (discCache != null && option.isDiscCache)
                     {
                         discCache.Set(uri, response);
                     }

                     Sprite sprite = Display(image, response);
                     if (sprite != null && option.isMemoryCache)
                     {
                         memoryCache.Set(uri, sprite);
                     }

                 }
                 else
                 {
                     DisplayErrorImage(image, option);
                 }

             });
        }

        bool DisplayLoadingImage(Image image, DisplayOption option)
        {
            if (!DisplayFromResource(image, option.loadingImagePath, option))
            {
                DisplayTransparent(image);
            }

            return true;
        }

        bool DisplayErrorImage(Image image, DisplayOption option)
        {
            return DisplayFromResource(image, option.loadErrorImagePath, option);
        }

        Sprite Display(Image image, byte[] data)
        {
            if (data != null && data.Length > 0)
            {
                RectTransform tranform = (RectTransform)image.transform;

                Texture2D texture = Convert2Texture((int)tranform.sizeDelta.x, (int)tranform.sizeDelta.y, data);

                return Display(image, texture);
            }

            return null;
        }

        bool DisplayFromResource(Image image, string resourcePath, DisplayOption option)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return false;
            }

            if (DisplayFromMemory(image, resourcePath, option))
            {
                return true;
            }

            Sprite sprite = Display(image, Resources.Load<Texture2D>(resourcePath));

            if (sprite != null && option.isMemoryCache)
            {
                memoryCache.Set(resourcePath, sprite);
            }

            return true;
        }

        void DisplayTransparent(Image image)
        {
            Color imageColor = image.color;
            imageColor.a = 0;
            image.color = imageColor;
            image.overrideSprite = null;
        }

        bool DisplayFromBytes(byte[] data, Image image, string uri, DisplayOption option)
        {
            Sprite sprite = Display(image, data);

            if (sprite == null)
            {
                DisplayErrorImage(image, option);

                return false;
            }
            else
            {
                if (option.isMemoryCache)
                {
                    memoryCache.Set(uri, sprite);
                }

                return true;
            }
        }

    }
}

