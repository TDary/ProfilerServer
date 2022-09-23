using Newtonsoft.Json;
using System.IO;
using System.Xml.Serialization;
namespace UAuto
{
    public class ProfilerAnalyzer
    {
        private static ProfilerAnalyzer _ProfilerAnalyzer = null;

        private MTimer mTimer = new MTimer();

        private ServerConfig mServerConfig = null;

        public static string Data_Ip = null;

        public static string Data_Port = null;

        private TaskMng mTaskMng = null;

        public MTimer ServerTimer
        {
            get
            {
                return this.mTimer;
            }
        }

        public ServerConfig Config
        {
            get
            {
                return mServerConfig;
            }

        }

        static ProfilerAnalyzer()
        {
            _ProfilerAnalyzer = new ProfilerAnalyzer();
        }

        public static ProfilerAnalyzer Instance
        {
            get
            {
                return _ProfilerAnalyzer;
            }
        }

        public void Init()
        {
            //初始化配置文件
            FileStream fileStream = new FileStream("ServerConfig.xml", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            //获取类型
            XmlSerializer xml = new XmlSerializer(typeof(ServerConfig));
            //反序列化
            mServerConfig = (ServerConfig)xml.Deserialize(fileStream);
            //释放
            fileStream.Dispose();
            Log.Print.Info("初始化配置文件成功");

            Log.Print.Info(string.Format("启动数据库成功 {0}:{1}", mServerConfig.database.DatabaseIP, mServerConfig.database.DatabasePort));
            MinIO mi = new MinIO();
            mi.Init();
            Data_Ip = MinIO.IP;
            Data_Port = MinIO.Port;

            mTaskMng = new TaskMng();
            mTaskMng.Init(mServerConfig.profilerAnalyze.Max);
            string mes = string.Format("{0}合并服务器启动成功！",ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP);
            //SendText(mes);
        }

        /// <summary>
        /// 主循环
        /// </summary>
        public void Update(float deltaTime)
        {

            if (mTaskMng != null)
            {
                mTaskMng.Update(deltaTime);
            }

        }

        /// <summary>
        /// 发送飞书机器人消息
        /// </summary>
        /// <param name="Messagetext"></param>
        public void SendText(string Messagetext)
        {
            string robotUrl = ProfilerAnalyzer.Instance.Config.RobotMonitor.FeishuRobot;
            var content = new { text = Messagetext };
            var data = new { msg_type = "text", content };
            MHttpSender.SendPostJson(robotUrl, JsonConvert.SerializeObject(data));
        }
    }
}
