// Created by hcq
using UnityEngine;
using System.Threading;

namespace UnityImageLoader.Utils
{
    public class AsyncTask : MonoBehaviour
    {

        public delegate object DoInBackgroundDelegate();

        public delegate void OnPostExecuteDelegate(object result);

        bool isCompleted;
        object result;

        DoInBackgroundDelegate doInBackground;
        OnPostExecuteDelegate onPostExecute;

        readonly static object locker = new object();
        static GameObject asyncTaskGo;

        AsyncTask() { }

        public AsyncTask SetThreadMaxCount(int count)
        {
            ThreadPool.SetMaxThreads(count, count);
            return this;
        }

        public static AsyncTask GetInstance()
        {
            if (asyncTaskGo == null)
            {
                lock (locker)
                {
                    if (asyncTaskGo == null)
                    {
                        asyncTaskGo = new GameObject("AsyncTask");
                        //设置线程池最大线程数
                        ThreadPool.SetMaxThreads(5, 5);
                    }
                }
            }

            //让update函数生效 需要挂到一个物体上
            return asyncTaskGo.AddComponent<AsyncTask>();
        }

        public AsyncTask SetDoInBackground(DoInBackgroundDelegate doInBackground)
        {
            this.doInBackground = doInBackground;
            return this;
        }

        public AsyncTask SetOnPostExecute(OnPostExecuteDelegate onPostExecute)
        {
            this.onPostExecute = onPostExecute;
            return this;
        }

        public AsyncTask Excute()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                if (doInBackground != null)
                {
                    result = doInBackground();
                }

                isCompleted = true;
            });
            return this;
        }

        void Update()
        {
            if (isCompleted)
            {
                if (onPostExecute != null)
                {
                    onPostExecute(result);
                }

                DestroyImmediate(this);
            }
        }
    }
}


