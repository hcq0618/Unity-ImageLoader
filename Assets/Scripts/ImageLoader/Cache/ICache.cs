// hcq 2017/3/23

namespace UnityImageLoader.Cache
{
    public interface ICache<V>
    {
        void Set(string key, V value);

        V Get(string key);
    }
}
