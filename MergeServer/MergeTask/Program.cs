using System;
using System.Threading;

namespace UAuto
{
    class Program
    {
        static void Main(string[] args)
        {

            Log.Print.Info("欢迎使用UAutoDistriServer合并服务器");

            ProfilerAnalyzer profilerAnalyzer = ProfilerAnalyzer.Instance;

            profilerAnalyzer.Init();

            /// <summary>
            /// 上一帧运行开始时的时间戳
            /// </summary>
            long mLastTime = profilerAnalyzer.ServerTimer.GetLogicTime();

            while (true)
            {
                try
                {
                    long curTime = profilerAnalyzer.ServerTimer.GetLogicTime();

                    float deltaTime = (float)((curTime - mLastTime) / 1000.0f);

                    Thread.Sleep(1);
                    OneThreadSynchronizationContext.Instance.Update();
                    profilerAnalyzer.Update(deltaTime);

                    mLastTime = curTime;
                }
                catch (Exception e)
                {
                    Log.Print.Error(e.ToString());
                }
            }

            //Console.WriteLine("输入任意键结束程序");
            //Console.ReadLine();
            //return;
        }
    }
}
