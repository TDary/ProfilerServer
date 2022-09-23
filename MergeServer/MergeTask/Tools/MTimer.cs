using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class MTimer
    {

        /// <summary>
        /// 虚拟启动时间 (毫秒)
        /// </summary>
        private long mServerOpenTime;

        /// <summary>
        /// 与本地时间的时差 (毫秒)
        /// </summary>
        private long mJetLag;

        /// <summary>
        /// 时间倍速
        /// </summary>
        private float mTimeSpeedRatio;

        /// <summary>
        /// 真实的启动时间 (毫秒)
        /// </summary>
        private long mServerStartTime;

        public MTimer()
        {
            mServerOpenTime = new DateTime(2019, 4, 1, 0, 0, 0, DateTimeKind.Local).Ticks / 10000;
            mJetLag = 0;
            mTimeSpeedRatio = 1;
            mServerStartTime = DateTime.Now.Ticks / 10000;
        }

        /// <summary>
        /// 返回当前逻辑上的时间 (毫秒)
        /// </summary>
        /// <returns></returns>
        public long GetLogicTime()
        {
            long nowtime = DateTime.Now.Ticks / 10000;  //真实的系统时间
            return mServerStartTime + (long)((float)(nowtime - mServerStartTime) * mTimeSpeedRatio) + mJetLag;
        }

        /// <summary>
        /// 返回当前逻辑上的时间戳
        /// </summary>
        /// <returns></returns>
        public DateTime GetLogicDateTime()
        {
            return new DateTime(GetLogicTime() * 10000, DateTimeKind.Local);
        }



    }
}
