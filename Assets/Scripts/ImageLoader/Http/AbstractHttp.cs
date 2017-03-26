// hcq 2017/3/26
using System.Collections.Generic;

namespace UnityImageLoader.Http
{
    public abstract class AbstractHttp
    {
        public delegate void OnResponseDelegate(byte[] response, int statusCode, bool isSuccess);

        protected internal Dictionary<string, string> headers = new Dictionary<string, string>();

        protected internal int connectTimeout = 20, requestTimeout = 60;//unit second

        #region set params

        public abstract void SetMaxConnection(byte count);

        public abstract void SetKeepAlive(bool isKeepAlive);

        public abstract void SetDisableCache(bool isDisableCache);

        public abstract void SetMaxConnectionIdleTime(int seconds);

        //默认20秒
        public virtual void SetConnectionTimeOut(int seconds)
        {
            connectTimeout = seconds;
        }

        //默认60秒
        public virtual void SetRequestTimeOut(int seconds)
        {
            requestTimeout = seconds;
        }

        #endregion

        #region request

        public abstract void Get(string url, OnResponseDelegate callback);

        public abstract void Post(string url, Dictionary<string, string> requestParams, OnResponseDelegate callback);

        #endregion

        public void AddHead(string name, string value)
        {
            headers.Add(name, value);
        }

    }
}
