using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace UAuto
{
    public sealed class MHttpService
    {

        private string mUrl = "";

        private HttpListener listerner = null;

        private bool mStarted = false;

        /// <summary>
        /// 释放标记
        /// </summary>
        private bool disposed = false;

        private Action<HttpListenerContext> taskProcCallback;

        public event Action<HttpListenerContext> TaskProcCallback
        {
            add
            {
                this.taskProcCallback += value;
            }
            remove
            {
                this.taskProcCallback -= value;
            }
        }

        public MHttpService(string listenerUrl)
        {
            this.mUrl = listenerUrl;
            mStarted = false;
            listerner = new HttpListener();
        }

        public bool IsDispose()
        {
            return disposed;
        }

        public void Release()
        {
            if (mStarted)
            {
                this.Stop();
            }

            if (listerner != null)
            {
                listerner = null;
            }

            disposed = true;
        }


        /// <summary>
        /// 启动Http监听服务
        /// </summary>
        public async void Start()
        {
            if (mStarted)
                return;

            mStarted = true;
            await Task.Run(new Action(_AysncStart));
        }

        private void _AysncStart()
        {
            while (mStarted)
            {
                try
                {
                    listerner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;//指定身份验证 Anonymous匿名访问
                    listerner.Prefixes.Add(this.mUrl);
                    listerner.Start();
                }
                catch (Exception e)
                {
                    Log.Print.Error("HTTP服务启动失败..." + e.ToString());
                    break;
                }
                Log.Print.Info("HTTP[" + this.mUrl + "]服务器启动成功.......");

                //线程池
                //int minThreadNum;
                //int portThreadNum;
                //int maxThreadNum;
                //ThreadPool.GetMaxThreads(out maxThreadNum, out portThreadNum);
                //ThreadPool.GetMinThreads(out minThreadNum, out portThreadNum);

                //Log.Debug("最大线程数：" + maxThreadNum);
                //Log.Debug("最小空闲线程数：" + minThreadNum);

                try
                {
                    while (!this.IsDispose() && mStarted)
                    {
                        //Log.Print.Debug("等待客户连接中.......");
                        //没有请求则GetContext处于阻塞状态
                        HttpListenerContext ctx = listerner.GetContext();

                        ThreadPool.QueueUserWorkItem(new WaitCallback(TaskProc), ctx);
                    }
                }
                catch (Exception e)
                {
                    Log.Print.Error("HTTP等待请求连接异常..." + e.ToString());
                    break;
                }
            }
        }

        public void Stop()
        {
            if (mStarted)
                mStarted = false;

            if (listerner != null)
                listerner.Stop();
        }

        private void TaskProc(object o)
        {
            OneThreadSynchronizationContext.Instance.Post(this.OnTaskProc, o);
        }

        private void OnTaskProc(object o)
        {
            HttpListenerContext ctx = (HttpListenerContext)o;

            if (this.taskProcCallback != null)
            {
                try
                {
                    this.taskProcCallback.Invoke(ctx);
                }
                catch (Exception e)
                {
                    Log.Print.Warn(e);
                }
            }

        }

    }

}
