// hcq 2017/3/26
using BestHTTP;
using System;
using System.Collections.Generic;

namespace UnityImageLoader.Http
{
    public class BestHttpImpl : AbstractHttp
    {
        public BestHttpImpl()
        {
            //isKeepAlive default is true, isDisableCache default is false
            HTTPManager.KeepAliveDefaultValue = false;
            HTTPManager.IsCachingDisabled = false;

        }

        #region set params

        //默认最大连接数为4
        public override void SetMaxConnection(byte count)
        {
            HTTPManager.MaxConnectionPerServer = count;
        }

        //默认为true
        public override void SetKeepAlive(bool isKeepAlive)
        {
            HTTPManager.KeepAliveDefaultValue = isKeepAlive;
        }

        public override void SetDisableCache(bool isDisableCache)
        {
            HTTPManager.IsCachingDisabled = isDisableCache;
        }

        //默认连接最大空闲时间2分钟
        public override void SetMaxConnectionIdleTime(int seconds)
        {
            HTTPManager.MaxConnectionIdleTime = TimeSpan.FromSeconds(seconds);
        }

        #endregion

        void HandleResponse(HTTPResponse response, OnResponseDelegate callback)
        {
            //如果请求错误 HTTPResponse对象会返回null
            if (callback != null)
            {
                if (response != null)
                {
                    callback(response.Data, response.StatusCode, response.IsSuccess);
                }
                else
                {
                    callback(null, -1, false);
                }
            }
        }

        void AddHeads(HTTPRequest request)
        {
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> pairs in headers)
                {
                    request.SetHeader(pairs.Key, pairs.Value);
                }
            }
        }

        public override void Get(string url, OnResponseDelegate callback)
        {
            HTTPRequest request = new HTTPRequest(new Uri(url), delegate (HTTPRequest originalRequest, HTTPResponse response)
            {
                HandleResponse(response, callback);
            });

            if (connectTimeout >= 0)
            {
                request.ConnectTimeout = TimeSpan.FromSeconds(connectTimeout);
            }

            if (requestTimeout >= 0)
            {
                request.Timeout = TimeSpan.FromSeconds(requestTimeout);
            }

            AddHeads(request);
            request.Send();
        }

        void AddParams(HTTPRequest request, Dictionary<string, string> requestParams)
        {
            if (requestParams != null)
            {
                foreach (KeyValuePair<string, string> pairs in requestParams)
                {
                    request.AddField(pairs.Key, pairs.Value);
                }
            }
        }

        public override void Post(string url, Dictionary<string, string> requestParams, OnResponseDelegate callback)
        {
            HTTPRequest request = new HTTPRequest(new Uri(url), HTTPMethods.Post, delegate (HTTPRequest originalRequest, HTTPResponse response)
            {
                HandleResponse(response, callback);
            });

            AddParams(request, requestParams);

            AddHeads(request);

            request.Send();
        }

    }
}
