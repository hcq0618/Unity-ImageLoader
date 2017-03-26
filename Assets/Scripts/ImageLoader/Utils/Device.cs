// hcq 2017/3/26
using System;
using UnityEngine;

namespace UnityImageLoader.Utils
{
    public static class Device
    {
        static AndroidJavaClass GetEnvironmentClass()
        {
            return new AndroidJavaClass("android.os.Environment");
        }

        public static long GetSDCardAvaliableBytes()
        {
#if UNITY_ANDROID
            string path = GetSDCardPath();

            using (AndroidJavaObject statFs = new AndroidJavaObject("android.os.StatFs", path))
            {
                long size;
                if (GetBuildVersionSDKInt() >= 18)
                {
                    size = statFs.Call<long>("getAvailableBlocksLong") * statFs.Call<long>("getBlockSizeLong");
                }
                else
                {
                    size = statFs.Call<long>("getAvailableBlocks") * statFs.Call<long>("getBlockSize");
                }

                return size;
            }
#else
            throw new NotImplementedException();
#endif
        }

        public static string GetSDCardPath()
        {
#if UNITY_ANDROID
            using (AndroidJavaClass environment = GetEnvironmentClass())
            {
                using (AndroidJavaObject directory = environment.CallStatic<AndroidJavaObject>("getExternalStorageDirectory"))
                {
                    return directory.Call<string>("getPath");
                }
            }
#else
            throw new NotImplementedException();
#endif
        }

        public static int GetBuildVersionSDKInt()
        {
#if UNITY_ANDROID
            using (AndroidJavaClass buildVersionClass = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return buildVersionClass.GetStatic<int>("SDK_INT");
            }
#else
            throw new NotImplementedException();
#endif
        }

        public static long GetMaxMemory()
        {
#if UNITY_ANDROID
            using (AndroidJavaClass runtime = new AndroidJavaClass("java.lang.Runtime"))
            {
                using (AndroidJavaObject run = runtime.CallStatic<AndroidJavaObject>("getRuntime"))
                {
                    long maxMemory = run.Call<long>("maxMemory");
                    return maxMemory;
                }
            }
#else
            throw new NotImplementedException();
#endif
        }

        public static bool IsExistSDCard()
        {
#if UNITY_ANDROID
            using (AndroidJavaClass environment = GetEnvironmentClass())
            {
                string state = environment.CallStatic<string>("getExternalStorageState");
                return "mounted".Equals(state);
            }
#else
            throw new NotImplementedException();
#endif

        }

        public static string GetExternalCacheDir()
        {
#if UNITY_ANDROID
            using (AndroidJavaObject activity = GetActivity())
            {
                using (AndroidJavaObject cacheDir = activity.Call<AndroidJavaObject>("getExternalCacheDir"))
                {
                    string path = cacheDir.Call<string>("getPath");
                    return path;
                }
            }
#else
            throw new NotImplementedException();
#endif
        }

        static AndroidJavaObject GetActivity()
        {
            using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
    }
}
