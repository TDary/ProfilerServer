using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;

namespace UAuto
{

    enum ETM_ProcessState
    {
        EMPS_None = 0,  // 未开始
        EMPS_WaitFile = 1,     //等获取数据
        EMPS_WaitZipFile = 2,     //等获取zip数据
        EMPS_WaitMerge = 3,   //等待合并完毕
        EMPS_End = 4,   //已经结束
        EMPS_MDown = 5, //合并完成
    }

    enum Get_ScreenState
    {
        Get_Default = 3,  //截图初始状态
        Get_Down = 1, //已获取截图(截图已生成)
        Get_Doing = 2,  //正在处理截图
        Get_None = 0,   //不存在截图
    }

    public class FileInfo
    {
        public string URL = string.Empty;

        public string Bucket = string.Empty;

        public string File = string.Empty;

        public string FilePath = string.Empty;

        public bool IsReady = false;
    }

    public class Region
    {
        //存储数为主键
        public int _id;
        //函数哈希值
        public ulong fun_id { get; set; }
        public List<RegionInfo> frames { get; set; }
    }
    public class AllFunRow
    {
        //函数hash为主键
        public long _id;
        public List<CaseFunRowInfo> frames { get; set; }

    }
    public class SFunRow
    {
        public List<AllFunRow> allfunrow { get; set; }
    }

    public class RegionInfo
    {
        public long frame { get; set; }
        public long validframe { get; set; }
        public long calls { get; set; }
        //*100
        public long gcalloc { get; set; }
        //*100
        public long timems { get; set; }
        //*100
        public long selfms { get; set; }
    }
    public class CaseFunRowInfo
    {

        public int frame { get; set; }

        //*100
        public int total { get; set; }

        //*100
        public int self { get; set; }

        public int calls { get; set; }

        //*100
        public int gcalloc { get; set; }

        //*100
        public int timems { get; set; }

        //*100
        public int selfms { get; set; }
    }

    /// <summary>
    /// 任务管理器所管理的合并任务
    /// </summary>
    public class TaskMerge : TaskBase
    {
        private bool mIsEnd = false;

        private AnalyzeTask mTaskAO = null;

        private string mAnalyzeStartTime = string.Empty;

        private int mStartTimeStamp = 0;

        private List<FileInfo> mCsvInfoList = new List<FileInfo>();

        private List<FileInfo> mFunrowjsonInfoList = new List<FileInfo>();

        private List<FileInfo> mRenderFunRowjsonInfoList = new List<FileInfo>();

        private List<FileInfo> mFunHashjsonInfoList = new List<FileInfo>();

        private List<FileInfo> mFunjsonInfoList = new List<FileInfo>();

        private string mZipFilePath = "";

        private bool mIsUnZipSuccess = false;

        private const string ZipFileName = "screen.zip";

        private Action<string> ActionDataFileComplete;

        private Action<string> ActionFunRowjsonFileComplete;

        private Action<string> ActionRenderFunRowjsonFileComplete;

        private Action<string> ActionFunHashjsonFileComplete;

        private Action<string> ActionFunjsonFileComplete;

        private Action<string> ActionZipFileComplete;

        private ETM_ProcessState mProcessState = ETM_ProcessState.EMPS_None;

        private Get_ScreenState mScreenState = Get_ScreenState.Get_Default;

        private float mGetFilesTimer = 0.0f;

        private Thread threadStart1 = null; //合并函数统计线程

        private Thread threadStart2 = null; //合并csv线程

        private Thread threadStart3 = null; //合并渲染线程数据

        private Thread threadStart4 = null; //入库哈希函数

        private string filename = ""; //csv名

        private string[] screenFiles = null; //存储当前案例截图名

        private int sampleStartTimestamp = 0;  //csv文件名开头

        private int sampleMaxTimestamp = 0; //csv文件名末尾

        private float maxTotalTime = 0; //csv文件总时间

        private int Totalframe;  //案例总帧数

        private bool isCsvSuccess;

        private bool isFunjsonSuccess;

        private bool isFunRowSuccess;

        private bool isRenderRowSuccess;

        private bool isFunHashSuccess;

        public TaskMerge(string id) : base(id)
        {
            ActionDataFileComplete = this.OnDataFileComplete;
            ActionFunRowjsonFileComplete = this.OnFunRowjsonFileComplete;
            ActionRenderFunRowjsonFileComplete = this.OnRenderRowjsonFileComplete;
            ActionFunHashjsonFileComplete = this.OnFunHashFileComplete;
            ActionFunjsonFileComplete = this.OnFunjsonFileComplete;
            ActionZipFileComplete = this.OnZipFileComplete;
        }

        public override void Begin()
        {

            mTaskAO = DB.Instance.FindOne<AnalyzeTask>(ID);

            if (mTaskAO == null)
            {
                Log.Print.Error("找不到对应的任务ID:" + ID);
                SetEnd();
                return;
            }

            if (mTaskAO.TaskState == (int)EAnalyzeTaskState.EAT_GetFilesTimeOut)
            {
                Log.Print.Error("当前案例获取分析文件失败----");
                SetEnd();
                return;
            }

            mStartTimeStamp = (int)CommonTools.GetLocalTimeSeconds();

            mProcessState = ETM_ProcessState.EMPS_WaitFile;

            mGetFilesTimer = 0.0f;

            // 列出所有要获取的文件
            for (int i = 0; i < mTaskAO.RawFiles.Length; i++)
            {
                string nowRawfile = mTaskAO.RawFiles[i];
                string nowMinioFile = string.Format("http://10.11.146.82:9000/analysisdata/{0}/{1}", GetBucket(), nowRawfile);
                // 最先看一下有没有已经创建的子数据
                FilterDefinitionBuilder<AnalyzeSubTask> filDen = new FilterDefinitionBuilder<AnalyzeSubTask>();
                FilterDefinition<AnalyzeSubTask> stquery = filDen.And(
                        filDen.Eq("Minio_Path", nowMinioFile),
                        filDen.Eq("UUID", mTaskAO.UUID));
                AnalyzeSubTask ast = DB.Instance.FindOne<AnalyzeSubTask>(stquery, DB.database);

                if (ast == null)
                {
                    // 写分析结果数据库
                    FilterDefinition<AnalyzeTask> qu1 = Builders<AnalyzeTask>.Filter.Eq("_id", mTaskAO._id);
                    AnalyzeTask nowState = DB.Instance.FindOne<AnalyzeTask>(qu1, DB.database);
                    if (nowState.TaskState != 1)
                    {
                        mTaskAO.TaskState = (int)EAnalyzeTaskState.EAT_CantSubtask;
                        mTaskAO.Desc = "分析" + mTaskAO.RawFiles[i] + "文件的子任务缺失";
                        DB.Instance.Update<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
                        SendText(mTaskAO.GameID, mTaskAO.UUID, mTaskAO.Desc);
                        Log.Print.Warn("分析" + mTaskAO.RawFiles[i] + "文件的子任务缺失");
                    }
                    SetEnd();
                    return;
                }

                FileInfo csvInfo = new FileInfo();
                csvInfo.URL = ast.Worker;
                csvInfo.Bucket = mTaskAO.AnalyzeBucket;
                csvInfo.File = string.Format("{0}.csv", nowRawfile);
                string path = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, GetBucket());
                csvInfo.FilePath = Path.Combine(path, csvInfo.File);
                csvInfo.IsReady = false;
                mCsvInfoList.Add(csvInfo);

                TaskMng.GetAnalyzeFile(ast.Worker, mTaskAO.UUID, mTaskAO.AnalyzeBucket, csvInfo.File, ActionDataFileComplete);

                FileInfo funrowjsonInfo = new FileInfo();
                funrowjsonInfo.URL = ast.Worker;
                funrowjsonInfo.Bucket = mTaskAO.AnalyzeBucket;
                funrowjsonInfo.File = string.Format("{0}_funrow.json", nowRawfile);
                string path2 = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, GetBucket());
                funrowjsonInfo.FilePath = Path.Combine(path2, funrowjsonInfo.File);
                funrowjsonInfo.IsReady = false;
                mFunrowjsonInfoList.Add(funrowjsonInfo);

                TaskMng.GetAnalyzeFile(ast.Worker, mTaskAO.UUID, mTaskAO.AnalyzeBucket, funrowjsonInfo.File, ActionFunRowjsonFileComplete);

                FileInfo rowjsonInfo = new FileInfo();
                rowjsonInfo.URL = ast.Worker;
                rowjsonInfo.Bucket = mTaskAO.AnalyzeBucket;
                rowjsonInfo.File = string.Format("{0}_renderrow.json", nowRawfile);
                string path4 = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, GetBucket());
                rowjsonInfo.FilePath = Path.Combine(path4, rowjsonInfo.File);
                rowjsonInfo.IsReady = false;
                mRenderFunRowjsonInfoList.Add(rowjsonInfo);

                TaskMng.GetAnalyzeFile(ast.Worker, mTaskAO.UUID, mTaskAO.AnalyzeBucket, rowjsonInfo.File, ActionRenderFunRowjsonFileComplete);

                FileInfo funhashjsonInfo = new FileInfo();
                funhashjsonInfo.URL = ast.Worker;
                funhashjsonInfo.Bucket = mTaskAO.AnalyzeBucket;
                funhashjsonInfo.File = string.Format("{0}_funhash.json", nowRawfile);
                string path5 = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, GetBucket());
                funhashjsonInfo.FilePath = Path.Combine(path5, funhashjsonInfo.File);
                funhashjsonInfo.IsReady = false;
                mFunHashjsonInfoList.Add(funhashjsonInfo);

                TaskMng.GetAnalyzeFile(ast.Worker, mTaskAO.UUID, mTaskAO.AnalyzeBucket, funhashjsonInfo.File, ActionFunHashjsonFileComplete);

                FileInfo funjson = new FileInfo();
                funjson.URL = ast.Worker;
                funjson.Bucket = mTaskAO.AnalyzeBucket;
                funjson.File = string.Format("{0}_fun.json", nowRawfile);
                string path6 = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, GetBucket());
                funjson.FilePath = Path.Combine(path6, funjson.File);
                funjson.IsReady = false;
                mFunjsonInfoList.Add(funjson);

                TaskMng.GetAnalyzeFile(ast.Worker, mTaskAO.UUID, mTaskAO.AnalyzeBucket, funjson.File, ActionFunjsonFileComplete);
            }
        }

        public override void Update(float deltaTime)
        {
            if (mProcessState == ETM_ProcessState.EMPS_WaitFile)
            {
                // 获取文件超时
                mGetFilesTimer += deltaTime;
                if (mGetFilesTimer > 600.0f)
                {
                    mGetFilesTimer = 0.0f;

                    FilterDefinition<AnalyzeTask> qu1 = Builders<AnalyzeTask>.Filter.Eq("_id", mTaskAO._id);
                    AnalyzeTask nowState = DB.Instance.FindOne<AnalyzeTask>(qu1, DB.database);

                    if (nowState.TaskState != 1)
                    {
                        string mees = "获取文件超时";
                        FilterDefinition<AnalyzeTask> query = Builders<AnalyzeTask>.Filter.Eq("_id", mTaskAO._id);
                        UpdateDefinition<AnalyzeTask> modify = Builders<AnalyzeTask>.Update.Set("Desc", mees);
                        DB.Instance.FindAndModify(query, modify, DB.database);
                        Log.Print.Warn("任务ID=" + ID + "获取文件超时");
                    }
                    else if (nowState.TaskState == 1)
                    {
                        Log.Print.Info("该案例ID:" + nowState._id + "已合并完毕！！！无需再次进行合并！！！");
                    }

                    mProcessState = ETM_ProcessState.EMPS_None;
                    SetEnd();
                }

                if (IsAllCsvReady() && IsAllFunRowjsonReady() && IsAllRenderRowjsonReady() && IsAllFunHashjsonReady() && IsAllFunjsonReady())
                {
                    mGetFilesTimer = 0.0f;
                    // 获取截图zip
                    mProcessState = ETM_ProcessState.EMPS_WaitZipFile;
                    GetZip();
                }
            }
            else if (mProcessState == ETM_ProcessState.EMPS_WaitZipFile)
            {
                string message = "开始合并!!!";
                SendText(mTaskAO.GameID, mTaskAO.UUID, message);
                FilterDefinition<AnalyzeTask> queryMerge = Builders<AnalyzeTask>.Filter.Eq("_id", mTaskAO._id);
                UpdateDefinition<AnalyzeTask> updateMerge = Builders<AnalyzeTask>.Update
                    .Set("WorkerName", ProfilerAnalyzer.Instance.Config.serverUrl.WorkerName)
                    .Set("AnalyzeURL", ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP)
                    .Set("TaskState", (int)EAnalyzeTaskState.EAT_Execution);
                DB.Instance.UpdateState<AnalyzeTask>(queryMerge, updateMerge, DB.database);

                threadStart1 = new Thread(() =>
                {
                    isFunRowSuccess = CombineFunRowjson();
                });
                threadStart2 = new Thread(() =>
                {
                    isCsvSuccess = CombineCsv();
                });
                threadStart3 = new Thread(() =>
                {
                    isRenderRowSuccess = CombineRenderRowjson();
                });
                threadStart4 = new Thread(() =>
                {
                    isFunHashSuccess = SaveFunHash();
                    //合并函数堆栈
                    isFunjsonSuccess = SaveFunjson();
                });
                //合并函数统计线程
                threadStart1.Start();
                //合并csv线程
                threadStart2.Start();
                //合并渲染线程统计数据
                threadStart3.Start();
                //入库哈希函数表数据
                threadStart4.Start();

                mProcessState = ETM_ProcessState.EMPS_WaitMerge;
            }
            else if (mProcessState == ETM_ProcessState.EMPS_WaitMerge)
            {
                if (threadStart1.ThreadState == ThreadState.Stopped && threadStart2.ThreadState == ThreadState.Stopped &&
                    threadStart3.ThreadState == ThreadState.Stopped && threadStart4.ThreadState == ThreadState.Stopped)
                {
                    if (isFunRowSuccess && isCsvSuccess && isRenderRowSuccess && isFunHashSuccess && isFunjsonSuccess)
                    {
                        mProcessState = ETM_ProcessState.EMPS_MDown;
                    }
                    else
                    {
                        mTaskAO.TaskState = (int)EAnalyzeTaskState.EAT_MergeError;
                        DB.Instance.UpdateTong<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
                        string message = "合并失败！！！请检查一下~~~";
                        SendText(mTaskAO.GameID, mTaskAO.UUID, message);
                        SetEnd();
                    }
                }
            }
            else if (mProcessState == ETM_ProcessState.EMPS_MDown)
            {
                SuccessCase(filename, screenFiles, sampleStartTimestamp, sampleMaxTimestamp, maxTotalTime);
                mProcessState = ETM_ProcessState.EMPS_WaitMerge;
            }
        }

        public void SendText(string gameid, string _uuid, string textmessage)
        {
            string robotUrl = ProfilerAnalyzer.Instance.Config.RobotMonitor.FeishuRobot;
            var content = new { text = "GameID:" + gameid + "，UUID:" + _uuid + "，案例" + textmessage };
            var data = new { msg_type = "text", content };
            MHttpSender.SendPostJson(robotUrl, JsonConvert.SerializeObject(data));
        }

        public void SendText(string gameid, string _uuid, string textmessage, string extra)
        {
            string robotUrl = ProfilerAnalyzer.Instance.Config.RobotMonitor.FeishuRobot;
            var content = new { text = "GameID:" + gameid + "，UUID:" + _uuid + "，" + textmessage + "，额外功能：" + extra };
            var data = new { msg_type = "text", content };
            MHttpSender.SendPostJson(robotUrl, JsonConvert.SerializeObject(data));
        }

        public override bool IsEnd()
        {
            return mIsEnd;
        }

        public override void Release()
        {
            mTaskAO = null;

            if (mCsvInfoList != null)
            {
                mCsvInfoList.Clear();
                mCsvInfoList = null;
            }
        }

        public void SetEnd()
        {
            mIsEnd = true;
        }

        private bool IsOccupied(string filepath)
        {
            FileStream stream = null;
            try
            {
                stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch
            {
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }
        }

        private bool IsAllCsvReady()
        {
            for (int i = 0; i < mCsvInfoList.Count; i++)
            {
                if (!mCsvInfoList[i].IsReady)
                {
                    return false;
                }
                bool occupied = IsOccupied(mCsvInfoList[i].FilePath);
                if (occupied)
                {
                    return false;
                }
            }

            return true;
        }


        private bool IsAllFunRowjsonReady()
        {
            for (int i = 0; i < mFunrowjsonInfoList.Count; i++)
            {
                if (!mFunrowjsonInfoList[i].IsReady)
                {
                    return false;
                }
                bool occupied = IsOccupied(mFunrowjsonInfoList[i].FilePath);
                if (occupied)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetFunRowjsonReady(string filepath)
        {
            for (int i = 0; i < mFunrowjsonInfoList.Count; i++)
            {
                if (mFunrowjsonInfoList[i].FilePath == filepath)
                {
                    //Log.Print.Info("文件" + filepath + "准备完毕");
                    mFunrowjsonInfoList[i].IsReady = true;
                    break;
                }
            }
        }

        private bool IsAllRenderRowjsonReady()
        {
            for (int i = 0; i < mRenderFunRowjsonInfoList.Count; i++)
            {
                if (!mRenderFunRowjsonInfoList[i].IsReady)
                {
                    return false;
                }
                bool occupied = IsOccupied(mRenderFunRowjsonInfoList[i].FilePath);
                if (occupied)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetRenderRowjsonReady(string filepath)
        {
            for (int i = 0; i < mRenderFunRowjsonInfoList.Count; i++)
            {
                if (mRenderFunRowjsonInfoList[i].FilePath == filepath)
                {
                    //Log.Print.Info("文件" + filepath + "准备完毕");
                    mRenderFunRowjsonInfoList[i].IsReady = true;
                    break;
                }
            }
        }

        private bool IsAllFunHashjsonReady()
        {
            for (int i = 0; i < mFunHashjsonInfoList.Count; i++)
            {
                if (!mFunHashjsonInfoList[i].IsReady)
                {
                    return false;
                }
                bool occupied = IsOccupied(mFunHashjsonInfoList[i].FilePath);
                if (occupied)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsAllFunjsonReady()
        {
            for (int i = 0; i < mFunjsonInfoList.Count; i++)
            {
                if (!mFunjsonInfoList[i].IsReady)
                {
                    return false;
                }
                bool occupied = IsOccupied(mFunjsonInfoList[i].FilePath);
                if (occupied)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetFunHashjsonReady(string filepath)
        {
            for (int i = 0; i < mFunHashjsonInfoList.Count; i++)
            {
                if (mFunHashjsonInfoList[i].FilePath == filepath)
                {
                    //Log.Print.Info("文件" + filepath + "准备完毕");
                    mFunHashjsonInfoList[i].IsReady = true;
                    break;
                }
            }
        }

        private void SetFunjsonReady(string filepath)
        {
            for (int i = 0; i < mFunjsonInfoList.Count; i++)
            {
                if (mFunjsonInfoList[i].FilePath == filepath)
                {
                    //Log.Print.Info("文件" + filepath + "准备完毕");
                    mFunjsonInfoList[i].IsReady = true;
                    break;
                }
            }
        }

        private void SetCsvReady(string filepath)
        {
            for (int i = 0; i < mCsvInfoList.Count; i++)
            {
                if (mCsvInfoList[i].FilePath == filepath)
                {
                    //Log.Print.Info("文件" + filepath + "准备完毕");
                    mCsvInfoList[i].IsReady = true;
                    break;
                }
            }
        }

        private void GetZip()
        {
            string filename = "screen.zip";
            TaskMng.GetFile(string.Format("{0}:{1}", mTaskAO.UploadIp, 9000), mTaskAO.AnalyzeBucket, filename, ActionZipFileComplete);
        }

        private bool UnZipScreen(out string[] fileList)
        {
            string savepath = Path.Combine(GetSavePath(), "screen");
            if (!Directory.Exists(savepath))
            {
                //目标目录不存在则创建
                try
                {
                    Directory.CreateDirectory(savepath);
                }
                catch (Exception ex)
                {
                    throw new Exception("创建目录" + savepath + "失败：" + ex.Message);
                }
            }

            bool unzipSuccess = ZipHelper.UnZip(mZipFilePath, savepath);

            if (unzipSuccess)
            {
                string[] filearr = Directory.GetFileSystemEntries(savepath);

                for (int i = 0, c = filearr.Length; i < c; i++)
                {
                    filearr[i] = Path.GetFileName(filearr[i]);
                }

                fileList = filearr;
                File.Delete(mZipFilePath);
            }
            else
            {
                fileList = null;
            }

            return unzipSuccess;
        }


        private void OnFunRowjsonFileComplete(string filePath)
        {
            if (IsEnd())
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Log.Print.Warn("合并任务" + ID + "收到获取funrowjson文件为空");
            }
            else
            {
                SetFunRowjsonReady(filePath);
            }
        }

        private void OnRenderRowjsonFileComplete(string filePath)
        {
            if (IsEnd())
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Log.Print.Warn("合并任务" + ID + "收到获取renderrowjson文件为空");
            }
            else
            {
                SetRenderRowjsonReady(filePath);
            }
        }

        private void OnFunHashFileComplete(string filePath)
        {
            if (IsEnd())
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Log.Print.Warn("合并任务" + ID + "收到获取hunhashjson文件为空");
            }
            else
            {
                SetFunHashjsonReady(filePath);
            }
        }

        private void OnFunjsonFileComplete(string filePath)
        {
            if (IsEnd())
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Log.Print.Warn("合并任务" + ID + "收到获取funjson文件为空");
            }
            else
            {
                SetFunjsonReady(filePath);
            }
        }

        private void OnDataFileComplete(string filePath)
        {
            if (IsEnd())
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Log.Print.Warn("合并任务" + ID + "收到获取文件为空");
            }
            else
            {
                SetCsvReady(filePath);
            }
        }

        private void OnZipFileComplete(string filePath)
        {
            if (IsEnd())
            {
                return;
            }

            if (string.IsNullOrEmpty(filePath))
            {
                Log.Print.Warn("合并任务" + ID + "收到获取zip文件为空");
            }
            else
            {
                mZipFilePath = filePath;
            }
        }

        private string GetBucket()
        {
            return string.Format("{0}-{1}", mTaskAO.GameID, mTaskAO.UUID);
        }

        public string GetSavePath()
        {
            string path = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, GetBucket());
            return path;
        }

        /// <summary>
        /// 把一个文件名转化为时间戳
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        private int GetFileTimeStamp(string filename)
        {
            string[] filenameArray = filename.Split('.');

            if (filenameArray != null && filenameArray.Length > 0)
            {
                int timestamp = 0;
                if (int.TryParse(filenameArray[0], out timestamp))
                {
                    return timestamp;
                }
            }

            return 0;
        }

        private string CertaintyInt(string data)
        {
            int xsd = data.Split('.').Length;
            if (xsd <= 1)
            {
                return data;
            }
            else
            {
                int intdata = (int)(float.Parse(data));
                return intdata.ToString();
            }
        }

        private string CertaintyFloat(string data)
        {
            int xsd = data.Split('.').Length;
            if (xsd > 1)
            {
                return data;
            }
            else
            {
                return float.Parse(data).ToString("0.0");
            }
        }

        /// <summary>
        /// 确定数据类型，int float
        /// </summary>
        /// <param name="i"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private List<string> CertaintyDataType(List<string> data)
        {
            List<string> certyaintyData = new List<string>();


            for (int i = 0; i < data.Count; i++)
            {
                switch (i)
                {
                    case 0://header.Add("Frames");       //A
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 1://header.Add("SubFrames");    //B
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 2://header.Add("FPS");          //C
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 3://header.Add("CPUTotalTime"); //D
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 4://header.Add("UsedTotalMem"); //E
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 5://header.Add("UsedUnityMemory");  //F
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 6://header.Add("UsedMonoMem");      //G
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 7://header.Add("UsedGfxMem");       //H
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 8://header.Add("UsedAudioMem");     //I
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 9://header.Add("UsedVideoMem");     //J
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 10://header.Add("UsedProfilerMem");  //K
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 11://header.Add("ResTotalMem");      //L
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 12://header.Add("ResUnityMem");      //M
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 13://header.Add("ResMonoMem");       //N
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 14://header.Add("ResGfxMem");        //O
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 15://header.Add("ResAudioMem");      //P
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 16://header.Add("ResVideoMem");      //Q
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 17://header.Add("ResProfilerMem");   //R
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 18://header.Add("TotalSysMemUsage"); //S
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 19://header.Add("TexCount");         //T
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 20://header.Add("TexMemory");        //U
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 21://header.Add("MesCount");         //V
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 22://header.Add("MesMemory");        //W
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 23://header.Add("MatCount");         //X
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 24://header.Add("MatMemory");        //Y
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 25://header.Add("AnimCount");        //Z
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 26://header.Add("AnimMemory");           //AA
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 27://header.Add("AudioClipsCount");      //AB
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 28://header.Add("AudioClipsMemory"); //AC
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 29://header.Add("Assets");           //AD
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 30://header.Add("GameObjInScene");   //AE
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 31://header.Add("TotalObjInScene");  //AF
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 32://header.Add("TotalObjectCount"); //AG
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 33://header.Add("GCAllocCount");     //AH
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 34://header.Add("GCAllocMem");       //AI
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 35://header.Add("Drawcall");         //AJ
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 36://header.Add("Setpass");          //AK
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 37://header.Add("TotalBatches");     //AL
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 38://header.Add("Tris");             //AM
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 39://header.Add("Verts");            //AN
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 40://header.Add("Active Dynamic");            //AO
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 41://header.Add("Active Kinematic");            //AP
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 42://header.Add("staticColliders");            //AQ
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 43://header.Add("contacts");            //AR
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 44://header.Add("triggerOL");            //AS
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 45://header.Add("activeCst");            //AT
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 46://header.Add("LowusedVRAM");            //AU
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 47://header.Add("HighusedVRAM");            //AV
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 48://header.Add("DBDrawcalls");            //AW
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 49://header.Add("DBbatches");            //AX
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 50://header.Add("SBDrawcalls");            //AY
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 51://header.Add("SBbatches");            //AZ
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;

                    case 52://header.Add("InsDrawCalls");            //BA
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 53://header.Add("Insbatches");            //BB
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 54://header.Add("DBBatchesSaved");            //BC
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 55://header.Add("SBBatchesSaved");
                        certyaintyData.Add(CertaintyInt(data[i]));     //BD
                        break;
                    case 56://header.Add("renTime");            //BE
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 57://header.Add("scrisTime");            //BF
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 58://header.Add("physiTime");            //BG
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 59://header.Add("otherTime");            //BH
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 60://header.Add("gcTime");            //BI
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 61://header.Add("uiTime");            //BJ
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 62://header.Add("vncTime");            //BK
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 63://header.Add("giTime");            //BL
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 64://header.Add("aniTime");            //BM
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 65://header.Add("VBOTotalB");            //BN
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 66://header.Add("VBOcount");            //BO
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 67://header.Add("rigbody");             //BP
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 68://header.Add("PlayerLoop_ms");       //BQ
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 69://header.Add("Loading_ms");          //BR
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 70://header.Add("Loading_GC");          //BS
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 71://header.Add("ParticleSystem_ms");    //BT
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 72://header.Add("Instantiate");          //BU
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 73://header.Add("Instantiate_calls")    //BV
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 74://header.Add("MeshSkinning.Update");  //BW
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 75://header.Add("Render.OpaqueGeometry");  //BX
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 76://header.Add("Render.TransparentGeometry"); //BY
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 77://header.Add("VSkinmesh蒙皮网格"); //BZ
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 78://header.Add("GameObject.Active次数"); //CA
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 79://header.Add("GameObject.Deactivate次数"); //CB
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 80://header.Add("Destroy次数");        //CC
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 81://header.Add("Camera.Render耗时");        //CD
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 82://header.Add("Shader.CreateGPUProgram耗时");        //CE
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 83://header.Add("Shader.Parse耗时");        //CF
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    case 84://header.Add("MeshSkinning_calls");        //CF
                        certyaintyData.Add(CertaintyInt(data[i]));
                        break;
                    case 85://header.Add("GC.Collect");        //CH
                        certyaintyData.Add(CertaintyFloat(data[i]));
                        break;
                    default:
                        certyaintyData.Add(data[i]);
                        break;
                }
            }

            return certyaintyData;
        }

        private void SuccessMerge(string csvfilename, string[] screenFiles, int sampleStartTimestamp, int sampleMaxTimestamp, float maxTotalTime)
        {
            // 写分析结果数据库
            mTaskAO.WorkerName = ProfilerAnalyzer.Instance.Config.serverUrl.WorkerName;
            mTaskAO.AnalyzeURL = ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP;
            mTaskAO.AnalyzeFile = csvfilename;
            mTaskAO.StartTimeStamp = mStartTimeStamp;
            mTaskAO.AnalyzeEndtime = CommonTools.GetLocalTimeFormat();
            mTaskAO.EndTimeStamp = (int)CommonTools.GetLocalTimeSeconds();
            mTaskAO.AnalyzeStarttime = mTaskAO.AnalyzeCreatetime;
            mTaskAO.ScreenState = (int)mScreenState;
            mTaskAO.TotalFrame = Totalframe;
            if (mIsUnZipSuccess && screenFiles != null)
            {
                mTaskAO.ScreenFiles = screenFiles;
            }

            mTaskAO.SampleStartTimeStamp = sampleStartTimestamp;
            mTaskAO.SampleEndTimeStamp = sampleMaxTimestamp + (int)(maxTotalTime / 1000.0f);
            mTaskAO.TaskState = (int)EAnalyzeTaskState.EAT_Success;

            DB.Instance.UpdateTong<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
            Log.Print.Info("CSV上传成功----");
            try
            {
                Task.Run(() => NotifyWeb(mTaskAO.GameID, mTaskAO.UUID, mTaskAO.DeviceInfo, mTaskAO.CaseName));
                string success = mTaskAO.GameName + "--" + mTaskAO.CaseName + "--案例解析合并完成!!!";
                string extra = "";
                if (mTaskAO.SwitchMode == 0)
                {
                    extra = "无";
                }
                else if (mTaskAO.SwitchMode == 1)
                {
                    extra = "开启了资源分析";
                }
                else if (mTaskAO.SwitchMode == 2)
                {
                    extra = "开启了自定义数据采集";
                }
                else
                {
                    extra = "开启了自定义数据采集和资源分析";
                }
                SendText(mTaskAO.GameID, mTaskAO.UUID, success, extra);
                Delete();
            }
            catch (Exception e)
            {
                string message = "更新状态失败，请检查！！！";
                Log.Print.Error("更新成功状态失败" + e);
                SendText(mTaskAO.GameID, mTaskAO.UUID, message);
            }
        }

        //删除解析器本地的案例文件并且判断创建数据库索引
        public void Delete()
        {
            string uploadfileName = ProfilerAnalyzer.Instance.Config.uploadDir.Dir;
            string casePath = uploadfileName + "\\" + mTaskAO.GameID + "-" + mTaskAO.UUID;
            foreach (string child in Directory.GetFileSystemEntries(casePath))
            {
                string extension = Path.GetExtension(child);
                if (extension == ".raw" || extension == ".csv" || extension == ".json")
                {
                    File.Delete(child);
                }
            }
        }

        //解析成功上报消息
        private void NotifyWeb(string gameid, string _uuid, string deviceinfo, string casename)
        {
            string getUrl = ProfilerAnalyzer.Instance.Config.reportUrl.NotifyUrl;

            try
            {
                string simpoledataurl = getUrl + "/case/summry/" + _uuid;
                string finishUrl = getUrl + "/case/notify";
                //http://10.11.10.147:3001/case/notify;
                string getredata = MHttpSender.SendGet(simpoledataurl);
                JObject finish = JObject.Parse(getredata);
                if (finish["code"].ToString() == "200")
                {
                    var redata = finish["data"];
                    var gameper = new { avg_fps = redata["avg_fps"].Value<float>(), max_resMono_kb = redata["max_resMono_kb"].Value<float>(), max_tex_kb = redata["max_tex_kb"].Value<float>(), max_mes_kb = redata["max_mes_kb"].Value<float>(), max_anim_kb = redata["max_anim_kb"].Value<float>(), max_audio_kb = redata["max_audio_kb"].Value<float>(), max_dc = redata["max_dc"].Value<float>(), max_tris = redata["max_tris"].Value<float>() };

                    var resultData = new { case_uuid = _uuid, case_name = casename, game_id = gameid, device = deviceinfo, game_performance = gameper };
                    var result = JsonConvert.SerializeObject(resultData);
                    string norifyResult = MHttpSender.SendPostJson(finishUrl, result);
                    Log.Print.Info("请求NotifyWeb成功:" + norifyResult);
                }
                else
                {
                    Log.Print.Warn("请求NotifyWeb失败:" + simpoledataurl);
                }

            }
            catch (Exception e)
            {
                Log.Print.Warn("请求NotifyWeb出错:" + e.ToString());
            }

        }

        private void SaveFunHashToDB(Dictionary<int, string> funhashmap)
        {
            string collection = "FunHashList";
            IMongoCollection<CaseFun> col = DB.Instance.GetConnect<CaseFun>(collection, DB.database);
            CaseAllFun caf = new CaseAllFun();
            caf.case_uuid = mTaskAO.UUID;
            caf.fun_ids = new List<long>();
            foreach (var item in funhashmap)
            {
                FilterDefinition<CaseFun> query = Builders<CaseFun>.Filter.Eq("name", item.Value);
                UpdateDefinition<CaseFun> update = Builders<CaseFun>.Update.SetOnInsert("_id", item.Key).SetOnInsert("name", item.Value);
                caf.fun_ids.Add(item.Key);
                DB.Instance.IntsertFunhash<CaseFun>(col, query, update);
            }
            DB.Instance.Insert<CaseAllFun>(caf, DB.database);
        }

        private void SaveRenderRowtoDB(Dictionary<int, CaseRender> allrow)
        {
            // 保存CaseRenderRow
            Dictionary<int, CaseRenderRow> caseFunRow = new Dictionary<int, CaseRenderRow>();
            int t = 0;
            int framecount = 0;
            int RenderThread_Hash = -1109288310;//渲染线程数据 "Render Thread"

            if (allrow.ContainsKey(RenderThread_Hash))
            {
                framecount = allrow[RenderThread_Hash].frames.Count;
            }

            foreach (var fun in allrow)
            {
                int fi = t + 1;
                ToRenderFunRow(fun, ref caseFunRow, framecount);
                t++;
            }

            //TODO：渲染线程的统计数据，无法统计汇总Main Thread数据
            List<CaseRenderRow> caseFunRowList = caseFunRow.Values.ToList<CaseRenderRow>();
            DB.Instance.InsertsFunrow<CaseRenderRow>(caseFunRowList, DB.database);
        }

        private void SaveFunJsonToDB(List<CaseFlame> framelist, ref int frame_id, IMongoCollection<CaseFlame> col)
        {
            List<CaseFlame> alllist = new List<CaseFlame>();
            foreach (var item in framelist)
            {
                CaseFlame now = new CaseFlame();
                now.case_uuid = mTaskAO.UUID;
                now.frame_id = frame_id;
                now.flame = item.flame;
                alllist.Add(now);
                frame_id++;
            }
            DB.Instance.Inserts<CaseFlame>(alllist, col);
        }

        private void SaveFunRowtoDB(Dictionary<int, CaseFunRow> allrow)
        {
            // 保存funrow
            Dictionary<long, CaseFunRow> caseFunRow = new Dictionary<long, CaseFunRow>();
            int t = 0;
            int framecount = 0;
            int MainThread_Hash = 1520718697;//主线程函数MainThread哈希值"Main Thread"
            int PlayerLoop_Hash = -548274077; //PlayerLoop哈希值"PlayerLoop"
            if (allrow.ContainsKey(MainThread_Hash))
            {
                framecount = allrow[MainThread_Hash].frames.Count;
                Totalframe = framecount;
            }
            foreach (var fun in allrow)
            {
                int fi = t + 1;
                ToFunRow(fun, ref caseFunRow, framecount);
                t++;
            }
            //TODO：主线程的数据,如果为自定义采集的文件数据则会变为0
            if (caseFunRow.ContainsKey(MainThread_Hash) && caseFunRow.ContainsKey(PlayerLoop_Hash))
            {
                caseFunRow[MainThread_Hash].total = caseFunRow[PlayerLoop_Hash].total;
                caseFunRow[MainThread_Hash].self = caseFunRow[PlayerLoop_Hash].self;
                caseFunRow[MainThread_Hash].calls = caseFunRow[PlayerLoop_Hash].calls;
                caseFunRow[MainThread_Hash].gcalloc = caseFunRow[PlayerLoop_Hash].gcalloc;
                caseFunRow[MainThread_Hash].timems = caseFunRow[PlayerLoop_Hash].timems;
                caseFunRow[MainThread_Hash].selfms = caseFunRow[PlayerLoop_Hash].selfms;
                caseFunRow[MainThread_Hash].frames = caseFunRow[PlayerLoop_Hash].frames;
            }

            List<CaseFunRow> caseFunRowList = caseFunRow.Values.ToList<CaseFunRow>();
            DB.Instance.InsertsFunrow<CaseFunRow>(caseFunRowList, DB.database);
            TopFunRow(caseFunRowList);
        }

        //函数统计Top图
        private void TopFunRow(List<CaseFunRow> allRowlist)
        {

            FunRowTop<TopRow> fav = new FunRowTop<TopRow>();
            fav.case_uuid = mTaskAO.UUID;
            fav.topName = "fun_selfms_validframecount";   //函数有效帧平均耗时Top30
            fav.topfuns = new List<TopRow>();
            string key1 = string.Format("FunRowTop-{0}-{1}", mTaskAO.UUID, fav.topName);
            //排序=>降序
            allRowlist.Sort((x, y) =>
            {
                int res = 0;
                float teX = x.selfms / x.validframecount;
                float teY = y.selfms / y.validframecount;
                if (teX < teY) res = -1;
                else if (teX > teY) res = 1;
                return -res;
            });
            //截取前30
            foreach (var i in allRowlist)
            {
                if (i.fun_id == 1520718697 || i.fun_id == -548274077)
                {
                    continue;
                }
                TopRow tr = new TopRow();
                tr.fun_id = i.fun_id;
                tr.framecount = i.framecount;
                tr.validframecount = i.validframecount;
                tr.total = i.total;
                tr.self = i.self;
                tr.calls = i.calls;
                tr.gcalloc = i.gcalloc;
                tr.timems = i.timems;
                tr.selfms = i.selfms;
                tr.max_selfms = i.frames[0].selfms;
                for (int j = 1; j < i.frames.Count; j++)
                {
                    tr.max_selfms = tr.max_selfms < i.frames[j].selfms ? i.frames[j].selfms : tr.max_selfms;
                }
                tr.max_timems = i.frames[0].timems;
                for (int j = 1; j < i.frames.Count; j++)
                {
                    tr.max_timems = tr.max_timems < i.frames[j].timems ? i.frames[j].timems : tr.max_timems;
                }
                tr.max_gcalloc = i.frames[0].gcalloc;
                for (int j = 1; j < i.frames.Count; j++)
                {
                    tr.max_gcalloc = tr.max_gcalloc < i.frames[j].gcalloc ? i.frames[j].gcalloc : tr.max_gcalloc;
                }
                fav.topfuns.Add(tr);
                if (fav.topfuns.Count == 30)
                {
                    break;
                }
            }

            FunRowTop<TopRow> avt = new FunRowTop<TopRow>();
            avt.case_uuid = mTaskAO.UUID;
            avt.topName = "fun_selfms_framecount";    //函数平均耗时Top30
            avt.topfuns = new List<TopRow>();
            string key2 = string.Format("FunRowTop-{0}-{1}", mTaskAO.UUID, avt.topName);
            //排序=>降序
            allRowlist.Sort((x, y) =>
            {
                int res = 0;
                float teX = x.selfms / x.framecount;
                float teY = y.selfms / y.framecount;
                if (teX < teY) res = -1;
                else if (teX > teY) res = 1;
                return -res;
            });
            //截取前30
            foreach (var j in allRowlist)
            {
                if (j.fun_id == 1520718697 || j.fun_id == -548274077)
                {
                    continue;
                }
                TopRow tr = new TopRow();
                tr.fun_id = j.fun_id;
                tr.framecount = j.framecount;
                tr.validframecount = j.validframecount;
                tr.total = j.total;
                tr.self = j.self;
                tr.calls = j.calls;
                tr.gcalloc = j.gcalloc;
                tr.timems = j.timems;
                tr.selfms = j.selfms;
                tr.max_selfms = j.frames[0].selfms;
                for (int i = 1; i < j.frames.Count; i++)
                {
                    tr.max_selfms = tr.max_selfms < j.frames[i].selfms ? j.frames[i].selfms : tr.max_selfms;
                }
                tr.max_timems = j.frames[0].timems;
                for (int i = 1; i < j.frames.Count; i++)
                {
                    tr.max_timems = tr.max_timems < j.frames[i].timems ? j.frames[i].timems : tr.max_timems;
                }
                tr.max_gcalloc = j.frames[0].gcalloc;
                for (int i = 1; i < j.frames.Count; i++)
                {
                    tr.max_gcalloc = tr.max_gcalloc < j.frames[i].gcalloc ? j.frames[i].gcalloc : tr.max_gcalloc;
                }
                avt.topfuns.Add(tr);
                if (avt.topfuns.Count == 30)
                {
                    break;
                }
            }

            FunRowTop<TopRow> avgcv = new FunRowTop<TopRow>();
            avgcv.case_uuid = mTaskAO.UUID;
            avgcv.topName = "fun_gcalloc_validframecount";   //函数有效帧平均GCTop30
            avgcv.topfuns = new List<TopRow>();
            string key3 = string.Format("FunRowTop-{0}-{1}", mTaskAO.UUID, avgcv.topName);
            //排序=>降序
            allRowlist.Sort((x, y) =>
            {
                int res = 0;
                float teX = x.gcalloc / x.validframecount;
                float teY = y.gcalloc / y.validframecount;
                if (teX < teY) res = -1;
                else if (teX > teY) res = 1;
                return -res;
            });
            //截取前30
            foreach (var k in allRowlist)
            {
                if (k.fun_id == 1520718697 || k.fun_id == -548274077)
                {
                    continue;
                }
                TopRow tr = new TopRow();
                tr.fun_id = k.fun_id;
                tr.framecount = k.framecount;
                tr.validframecount = k.validframecount;
                tr.total = k.total;
                tr.self = k.self;
                tr.calls = k.calls;
                tr.gcalloc = k.gcalloc;
                tr.timems = k.timems;
                tr.selfms = k.selfms;
                tr.max_selfms = k.frames[0].selfms;
                for (int j = 1; j < k.frames.Count; j++)
                {
                    tr.max_selfms = tr.max_selfms < k.frames[j].selfms ? k.frames[j].selfms : tr.max_selfms;
                }
                tr.max_timems = k.frames[0].timems;
                for (int j = 1; j < k.frames.Count; j++)
                {
                    tr.max_timems = tr.max_timems < k.frames[j].timems ? k.frames[j].timems : tr.max_timems;
                }
                tr.max_gcalloc = k.frames[0].gcalloc;
                for (int j = 1; j < k.frames.Count; j++)
                {
                    tr.max_gcalloc = tr.max_gcalloc < k.frames[j].gcalloc ? k.frames[j].gcalloc : tr.max_gcalloc;
                }
                avgcv.topfuns.Add(tr);
                if (avgcv.topfuns.Count == 30)
                {
                    break;
                }
            }

            FunRowTop<TopRow> avgc = new FunRowTop<TopRow>();
            avgc.case_uuid = mTaskAO.UUID;
            avgc.topName = "fun_gcalloc_framecount";    //函数平均GCTop30
            avgc.topfuns = new List<TopRow>();
            string key4 = string.Format("FunRowTop-{0}-{1}", mTaskAO.UUID, avgc.topName);
            //排序=>降序
            allRowlist.Sort((x, y) =>
            {
                int res = 0;
                float teX = x.gcalloc / x.framecount;
                float teY = y.gcalloc / y.framecount;
                if (teX < teY) res = -1;
                else if (teX > teY) res = 1;
                return -res;
            });
            //截取前30
            foreach (var l in allRowlist)
            {
                if (l.fun_id == 1520718697 || l.fun_id == -548274077)
                {
                    continue;
                }
                TopRow tr = new TopRow();
                tr.fun_id = l.fun_id;
                tr.framecount = l.framecount;
                tr.validframecount = l.validframecount;
                tr.total = l.total;
                tr.self = l.self;
                tr.calls = l.calls;
                tr.gcalloc = l.gcalloc;
                tr.timems = l.timems;
                tr.selfms = l.selfms;
                tr.max_selfms = l.frames[0].selfms;
                for (int j = 1; j < l.frames.Count; j++)
                {
                    tr.max_selfms = tr.max_selfms < l.frames[j].selfms ? l.frames[j].selfms : tr.max_selfms;
                }
                tr.max_timems = l.frames[0].timems;
                for (int j = 1; j < l.frames.Count; j++)
                {
                    tr.max_timems = tr.max_timems < l.frames[j].timems ? l.frames[j].timems : tr.max_timems;
                }
                tr.max_gcalloc = l.frames[0].gcalloc;
                for (int j = 1; j < l.frames.Count; j++)
                {
                    tr.max_gcalloc = tr.max_gcalloc < l.frames[j].gcalloc ? l.frames[j].gcalloc : tr.max_gcalloc;
                }
                avgc.topfuns.Add(tr);
                if (avgc.topfuns.Count == 30)
                {
                    break;
                }
            }

            FunRowTop<HighTime> gt = new FunRowTop<HighTime>();
            gt.case_uuid = mTaskAO.UUID;
            gt.topName = "high_selfms";    //高耗时帧函数统计
            gt.topfuns = new List<HighTime>();
            string key5 = string.Format("FunRowTop-{0}-{1}", mTaskAO.UUID, gt.topName);
            foreach (var item in allRowlist)
            {
                if (item.fun_id == 1520718697 || item.fun_id == -548274077)
                {
                    continue;
                }
                HighTime hti = new HighTime();
                int highfr = 0;
                int hightime = 0;
                hti.fun_id = item.fun_id;
                hti.selfms_validframecount_average = item.selfms / item.validframecount;
                hti.validframecount = item.validframecount;
                if (item.frames.Count != 0)
                {
                    foreach (var j in item.frames)
                    {
                        if (j.selfms > 1000)
                        {
                            highfr += 1;
                            hightime += j.selfms;
                        }
                    }
                }
                if (hightime != 0)
                {
                    hti.high_selfms_average = hightime / highfr;
                    hti.high_selfms_framecounts = highfr;
                    gt.topfuns.Add(hti);
                }
            }

            //入库,同时也入redisjson
            string collection = "FunRowTop";
            DB.Instance.InsertFunRowTop<FunRowTop<TopRow>>(fav, collection, DB.database);
            DB.Instance.InsertFunRowTop<FunRowTop<TopRow>>(avt, collection, DB.database);
            DB.Instance.InsertFunRowTop<FunRowTop<TopRow>>(avgcv, collection, DB.database);
            DB.Instance.InsertFunRowTop<FunRowTop<TopRow>>(avgc, collection, DB.database);
            DB.Instance.InsertFunRowTop<FunRowTop<HighTime>>(gt, collection, DB.database);
        }

        private void ToFunRow(KeyValuePair<int, CaseFunRow> a, ref Dictionary<long, CaseFunRow> caseFunRowMap, int framecount)
        {
            //转换成CaseFunRow类数据格式
            CaseFunRow csrow = new CaseFunRow();
            csrow.frames = new List<CaseFunRowInfo>();
            csrow.case_uuid = mTaskAO.UUID;
            csrow.fun_id = a.Key;
            csrow.framecount = framecount;
            csrow.validframecount = a.Value.frames.Count;
            csrow.total = 0;
            csrow.self = 0;
            csrow.calls = 0;
            csrow.gcalloc = 0;
            csrow.timems = 0;
            csrow.selfms = 0;

            foreach (var item in a.Value.frames)
            {
                CaseFunRowInfo casin = new CaseFunRowInfo();
                casin.frame = item.frame;
                casin.total = item.total;
                casin.self = item.self;
                casin.calls = item.calls;
                casin.gcalloc = item.gcalloc;
                casin.timems = item.timems;
                casin.selfms = item.selfms;
                csrow.frames.Add(casin);

                csrow.total = csrow.total + item.total;
                csrow.self = csrow.self + item.self;
                csrow.calls = csrow.calls + item.calls;
                csrow.gcalloc = csrow.gcalloc + item.gcalloc;
                csrow.timems = csrow.timems + item.timems;
                csrow.selfms = csrow.selfms + item.selfms;
            }
            caseFunRowMap.Add(a.Key, csrow);
        }

        private void ToRenderFunRow(KeyValuePair<int, CaseRender> a, ref Dictionary<int, CaseRenderRow> caseFunRowMap, int framecount)
        {
            CaseRenderRow renrow = new CaseRenderRow();
            renrow.case_uuid = mTaskAO.UUID;
            renrow.frames = new List<CaseRenderInfo>();
            renrow.fun_id = a.Key;
            renrow.framecount = framecount;
            renrow.validframecount = a.Value.frames.Count;
            renrow.timems = 0;
            renrow.selfms = 0;

            foreach (var item in a.Value.frames)
            {
                CaseRenderInfo casin = new CaseRenderInfo();
                casin.frame = item.frame;
                casin.timems = item.timems;
                casin.selfms = item.selfms;
                renrow.frames.Add(casin);

                renrow.timems = renrow.timems + item.timems;
                renrow.selfms = renrow.selfms + item.selfms;
            }
            caseFunRowMap.Add(a.Key, renrow);
        }

        private bool SaveFunjson()
        {
            try
            {
                if (mFunjsonInfoList.Count <= 0)
                {
                    string errorText = ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP + "解析器不存在funjson文件";
                    SendText(mTaskAO.GameID, mTaskAO.UUID, errorText);
                    mTaskAO.TaskState = (int)EAnalyzeTaskState.EAT_SubtaskNotFound;
                    mTaskAO.Desc = "解析器上不存在funjson文件";
                    DB.Instance.Update<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
                    Log.Print.Warn("任务ID=" + ID + "获取解析器funjson文件不存在");
                    SetEnd();
                    return false; ;
                }

                Log.Print.Info("开始写库funjson");
                string collection = "CaseFlame";
                int frame_id = 1;
                IMongoCollection<CaseFlame> col = DB.database.GetCollection<CaseFlame>(collection);
                for (int i = 1; i <= mFunjsonInfoList.Count; i++)
                {
                    if (File.Exists(mFunjsonInfoList[i - 1].FilePath))
                    {
                        List<CaseFlame> listFrame = null;
                        StreamReader sr = File.OpenText(mFunjsonInfoList[i - 1].FilePath);
                        JsonSerializerSettings max = new JsonSerializerSettings();
                        max.MaxDepth = 256;
                        string jsonStr = sr.ReadToEnd().ToString();
                        listFrame = JsonConvert.DeserializeObject<List<CaseFlame>>(jsonStr, max);
                        sr.Close();
                        sr.Dispose();
                        if (listFrame.Count != 0)
                        {
                            SaveFunJsonToDB(listFrame, ref frame_id, col);
                        }
                    }
                    else
                    {
                        Log.Print.Warn("待合并的文件不存在：" + File.Exists(mFunjsonInfoList[i - 1].FilePath));
                    }
                }
                return true;
            }

            catch (Exception e)
            {
                Log.Print.Error(e);
                string message = "funjson文件写库失败,请检查！！！";
                SendText(mTaskAO.GameID, mTaskAO.UUID, message);
                return false;
            }
        }

        private bool SaveFunHash()
        {
            try
            {
                if (mFunHashjsonInfoList.Count <= 0)
                {
                    string errorText = ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP + "解析器不存在funhash文件";
                    SendText(mTaskAO.GameID, mTaskAO.UUID, errorText);
                    mTaskAO.TaskState = (int)EAnalyzeTaskState.EAT_SubtaskNotFound;
                    mTaskAO.Desc = "解析器上不存在funhash文件";
                    DB.Instance.Update<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
                    Log.Print.Warn("任务ID=" + ID + "获取解析器funhash文件不存在");
                    SetEnd();
                    return false;
                }
                Log.Print.Info("开始合并funhash.json");
                Dictionary<int, string> funhashMap = new Dictionary<int, string>();

                for (int i = 1; i <= mFunHashjsonInfoList.Count; i++)
                {
                    if (File.Exists(mFunHashjsonInfoList[i - 1].FilePath))
                    {
                        Dictionary<int, string> funhash = null;
                        StreamReader sr = File.OpenText(mFunHashjsonInfoList[i - 1].FilePath);
                        JsonSerializerSettings max = new JsonSerializerSettings();
                        string jsonStr = sr.ReadToEnd().ToString();
                        funhash = JsonConvert.DeserializeObject<Dictionary<int, string>>(jsonStr);
                        sr.Close();
                        sr.Dispose();
                        if (funhash.Count != 0)
                        {
                            foreach (var item in funhash)
                            {
                                if (!funhashMap.ContainsKey(item.Key))
                                {
                                    funhashMap.Add(item.Key, item.Value);
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.Print.Warn("待合并的文件不存在：" + File.Exists(mFunHashjsonInfoList[i - 1].FilePath));
                    }
                }
                SaveFunHashToDB(funhashMap);
                return true;
            }
            catch (Exception e)
            {
                Log.Print.Error(e);
                string message = "合并funhash文件失败,请检查！！！";
                SendText(mTaskAO.GameID, mTaskAO.UUID, message);
                return false;
            }
        }

        private bool CombineRenderRowjson()
        {
            try
            {
                if (mRenderFunRowjsonInfoList.Count <= 0)
                {
                    string errorText = ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP + "解析器不存在renderrows文件";
                    SendText(mTaskAO.GameID, mTaskAO.UUID, errorText);
                    mTaskAO.TaskState = (int)EAnalyzeTaskState.EAT_SubtaskNotFound;
                    mTaskAO.Desc = "解析器上不存在renderrow文件";
                    DB.Instance.Update<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
                    Log.Print.Warn("任务ID=" + ID + "获取解析器funrow文件不存在");
                    SetEnd();
                    return false;
                }
                int RenderThread_Hash = -1109288310;
                //合并渲染线程函数统计逻辑
                Log.Print.Info("开始合并RenderRow.json");
                Dictionary<int, CaseRender> zifun = new Dictionary<int, CaseRender>();
                int count = 1;
                for (int i = 1; i <= mRenderFunRowjsonInfoList.Count; i++)
                {
                    if (File.Exists(mRenderFunRowjsonInfoList[i - 1].FilePath))
                    {
                        Dictionary<int, CaseRender> savefunrow = null;
                        StreamReader sr = File.OpenText(mRenderFunRowjsonInfoList[i - 1].FilePath);
                        JsonSerializerSettings max = new JsonSerializerSettings();
                        max.MaxDepth = 512;
                        string jsonStr = sr.ReadToEnd().ToString();
                        savefunrow = JsonConvert.DeserializeObject<Dictionary<int, CaseRender>>(jsonStr, max);
                        sr.Close();
                        sr.Dispose();
                        foreach (var item in savefunrow)
                        {
                            CaseRender allf = new CaseRender();
                            allf.frames = new List<CaseRenderInfo>();
                            if (!zifun.ContainsKey(item.Key))
                            {
                                allf._id = item.Key;
                                foreach (var sin in item.Value.frames)
                                {
                                    CaseRenderInfo casir = new CaseRenderInfo();
                                    casir.frame = count + sin.frame - 2;
                                    casir.timems = sin.timems;
                                    casir.selfms = sin.selfms;
                                    allf.frames.Add(casir);
                                }
                                zifun.Add(allf._id, allf);
                            }
                            else
                            {
                                allf._id = item.Key;
                                foreach (var a in item.Value.frames)
                                {
                                    CaseRenderInfo casi = new CaseRenderInfo();
                                    casi.frame = count + a.frame - 2;
                                    casi.timems = a.timems;
                                    casi.selfms = a.selfms;
                                    allf.frames.Add(casi);
                                }
                                zifun[item.Key].frames.AddRange(allf.frames);
                            }
                            if (savefunrow.Count != 0)
                            {
                                count = count + savefunrow[RenderThread_Hash].frames.Count;
                            }
                        }
                    }
                    else
                    {
                        Log.Print.Warn("待合并的文件不存在：" + File.Exists(mRenderFunRowjsonInfoList[i - 1].FilePath));
                    }
                }
                SaveRenderRowtoDB(zifun);
                return true;
            }
            catch (Exception e)
            {
                Log.Print.Error(e);
                string message = "合并funrenderrow文件失败,请检查！！！";
                SendText(mTaskAO.GameID, mTaskAO.UUID, message);
                return false;
            }
        }

        private bool CombineFunRowjson()
        {
            try
            {
                if (mFunrowjsonInfoList.Count <= 0)
                {
                    string errorText = ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP + "解析器不存在funrowjson文件";
                    SendText(mTaskAO.GameID, mTaskAO.UUID, errorText);
                    mTaskAO.TaskState = (int)EAnalyzeTaskState.EAT_SubtaskNotFound;
                    mTaskAO.Desc = "解析器上不存在funrow文件";
                    DB.Instance.Update<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
                    Log.Print.Warn("任务ID=" + ID + "获取解析器funrow文件不存在");
                    SetEnd();
                    return false; ;
                }
                Log.Print.Info("当前合并uuid:" + mTaskAO.UUID);
                int MainThread_Hash = 1520718697;
                //合并函数统计逻辑
                Log.Print.Info("开始合并FunRow.json");
                Dictionary<int, CaseFunRow> zifun = new Dictionary<int, CaseFunRow>();
                int count = 1;
                for (int i = 1; i <= mFunrowjsonInfoList.Count; i++)
                {
                    if (File.Exists(mFunrowjsonInfoList[i - 1].FilePath))
                    {
                        Dictionary<int, CaseFunRow> savefunrow = null;
                        StreamReader sr = File.OpenText(mFunrowjsonInfoList[i - 1].FilePath);
                        JsonSerializerSettings max = new JsonSerializerSettings();
                        max.MaxDepth = 512;
                        string jsonStr = sr.ReadToEnd().ToString();
                        savefunrow = JsonConvert.DeserializeObject<Dictionary<int, CaseFunRow>>(jsonStr, max);
                        sr.Close();
                        sr.Dispose();
                        foreach (var item in savefunrow)
                        {
                            CaseFunRow allf = new CaseFunRow();
                            allf.fun_id = item.Key;
                            allf.case_uuid = mTaskAO.UUID;
                            allf.framecount = 0;
                            allf.validframecount = 0;
                            allf.total = 0;
                            allf.self = 0;
                            allf.gcalloc = 0;
                            allf.timems = 0;
                            allf.selfms = 0;
                            allf.frames = new List<CaseFunRowInfo>();
                            if (!zifun.ContainsKey(item.Key))
                            {
                                allf.fun_id = item.Key;
                                foreach (var sin in item.Value.frames)
                                {
                                    CaseFunRowInfo casir = new CaseFunRowInfo();
                                    casir.frame = count + sin.frame - 2;
                                    casir.total = sin.total;
                                    casir.self = sin.self;
                                    casir.calls = sin.calls;
                                    casir.gcalloc = sin.gcalloc;
                                    casir.timems = sin.timems;
                                    casir.selfms = sin.selfms;
                                    allf.frames.Add(casir);
                                }
                                zifun.Add(item.Key, allf);
                            }
                            else
                            {
                                allf.fun_id = item.Key;
                                foreach (var a in item.Value.frames)
                                {
                                    CaseFunRowInfo casi = new CaseFunRowInfo();
                                    casi.frame = count + a.frame - 2;
                                    casi.total = a.total;
                                    casi.self = a.self;
                                    casi.calls = a.calls;
                                    casi.gcalloc = a.gcalloc;
                                    casi.timems = a.timems;
                                    casi.selfms = a.selfms;
                                    allf.frames.Add(casi);
                                }
                                zifun[item.Key].frames.AddRange(allf.frames);
                            }
                        }
                        if (savefunrow.Count != 0)
                        {
                            count = count + savefunrow[MainThread_Hash].frames.Count;
                        }
                    }
                    else
                    {
                        Log.Print.Warn("待合并的文件不存在：" + File.Exists(mFunrowjsonInfoList[i - 1].FilePath));
                    }
                }
                SaveFunRowtoDB(zifun);
                return true;
            }
            catch (Exception e)
            {
                Log.Print.Error(e);
                string message = "合并funrow文件失败,请检查！！！";
                SendText(mTaskAO.GameID, mTaskAO.UUID, message);
                return false;
            }
        }

        private bool CombineCsv()
        {
            try
            {
                Log.Print.Info("开始合并CSV");

                List<List<string>> csvDataTotal = new List<List<string>>();

                List<string> header = new List<string>();
                header.Add("Frames");       //A
                header.Add("SubFrames");    //B
                header.Add("FPS");          //C
                header.Add("CPUTotalTime"); //D
                header.Add("UsedTotalMem"); //E
                header.Add("UsedUnityMemory");  //F
                header.Add("UsedMonoMem");      //G
                header.Add("UsedGfxMem");       //H
                header.Add("UsedAudioMem");     //I
                header.Add("UsedVideoMem");     //J
                header.Add("UsedProfilerMem");  //K
                header.Add("ResTotalMem");      //L
                header.Add("ResUnityMem");      //M
                header.Add("ResMonoMem");       //N
                header.Add("ResGfxMem");        //O
                header.Add("ResAudioMem");      //P
                header.Add("ResVideoMem");      //Q
                header.Add("ResProfilerMem");   //R
                header.Add("TotalSysMemUsage"); //S
                header.Add("TexCount");         //T
                header.Add("TexMemory");        //U
                header.Add("MesCount");         //V
                header.Add("MesMemory");        //W
                header.Add("MatCount");         //X
                header.Add("MatMemory");        //Y
                header.Add("AnimCount");        //Z
                header.Add("AnimMemory");           //AA
                header.Add("AudioClipsCount");      //AB
                header.Add("AudioClipsMemory"); //AC
                header.Add("Assets");           //AD
                header.Add("GameObjInScene");   //AE
                header.Add("TotalObjInScene");  //AF
                header.Add("TotalObjectCount"); //AG
                header.Add("GCAllocCount");     //AH
                header.Add("GCAllocMem");       //AI
                header.Add("Drawcall");         //AJ
                header.Add("Setpass");          //AK
                header.Add("TotalBatches");     //AL
                header.Add("Tris");             //AM
                header.Add("Verts");            //AN
                header.Add("activeDynamic");             //AO 非运动性Rigidbody数(Active Dynamic)
                header.Add("activeKinematic");           //AP 运动性Rigidbody数(Active Kinematic)
                header.Add("staticColliders");           //AQ 无Rigidbody的拥有collider的游戏对象个数(Static Colliders)
                header.Add("contacts");                  //AR 碰撞器间接触总数
                header.Add("triggerOL");                 //AS 重叠trigger数量(对)
                header.Add("activeCst");                 //AT Active Constraints
                header.Add("LowusedVRAM");               //AU VRAM Usage(lowVRam)
                header.Add("HighusedVRAM");              //AV VRAM Usage(highVRam)
                header.Add("DBDrawcalls");               //AW Dynamic Batching(Batched Draw Calls)
                header.Add("DBbatches");                 //AX Dynamic Batching(Batches)
                header.Add("SBDrawcalls");               //AY Static Batching(Batched Draw Calls)
                header.Add("SBbatches");                 //AZ Static Batching(Batches)
                header.Add("InsDrawCalls");              //BA Instancing(Batched Draw Calls)
                header.Add("Insbatches");                //BB Instancing(Batches)
                header.Add("DBBatchesSaved");            //BC dynamicBatchesSaved
                header.Add("SBBatchesSaved");            //BD staticBatchedsSaved
                header.Add("renTime");                   //BE Rendering Time(渲染模块耗时)
                header.Add("ScriptsTime");               //BF Scripts Time(脚本模块耗时)
                header.Add("PhysicsTime");               //BG Physics Time(物理模块耗时)
                header.Add("otherTime");                 //BH Other Time(其他模块耗时)
                header.Add("gcTime");                    //BI GC Time(GC耗时)
                header.Add("uiTime");                    //BJ UI Time(UI耗时)
                header.Add("vncTime");                   //BK VSync Time(异步耗时)
                header.Add("giTime");                    //BL Global Illumination耗时
                header.Add("aniTime");                   //BM Animation(动画耗时)        
                header.Add("VBOTotalB");                 //BN VBO Total(VBOTotalBytes)
                header.Add("VBOcount");                  //BO VBO Total(VBO Count)
                header.Add("rigbody");                   //BP Rigidbody
                header.Add("PlayerLoop_ms");             //BQ PlayerLoop_ms
                header.Add("Loading_ms");                //BR Loading_ms
                header.Add("Loading_GC");                //BS Loading_GC
                header.Add("ParticleSystem_ms");         //BT ParticleSystem.Update耗时
                header.Add("Instantiate_ms");            //BU Instantiate耗时
                header.Add("Instantiate_calls");         //BV Instantiate次数
                header.Add("MeshSkinning_ms");           //BW MeshSkinning.Update耗时
                header.Add("RenderOpaque_ms");           //BX Render.OpaqueGeometry耗时
                header.Add("RenderTransparent_ms");      //BY Render.TransparentGeometry耗时
                header.Add("VSkinmesh");                 //BZ VisibleSkinnedMeshes蒙皮网格
                header.Add("GameObject.Active");                    //CA GameObject.Active次数
                header.Add("GameObject.Deactivate");                //CB GameObject.Deactivate次数
                header.Add("Destroy");                     //CC Destroy次数
                header.Add("Camera.Render");         //CD Camera.Render耗时
                header.Add("Shader.CreateGPUProgram");   //CE Shader.CreateGPUProgram耗时
                header.Add("Shader.Parse");           //CF Shader.Parse耗时
                header.Add("MeshSkinning_calls");   //CG MeshSkinning_calls次数
                header.Add("GCCollectms");              //CH GC.Collect耗时

                csvDataTotal.Add(header);

                int frame = 1;

                for (int i = 1; i <= mCsvInfoList.Count; i++)
                {
                    if (File.Exists(mCsvInfoList[i - 1].FilePath))
                    {
                        int sampleTimestamp = GetFileTimeStamp(mCsvInfoList[i - 1].File);

                        bool isMax = false;
                        float sumTotalTime = 0.0f;

                        if (sampleTimestamp > 0)
                        {
                            if (sampleStartTimestamp == 0)
                            {
                                sampleStartTimestamp = sampleTimestamp;
                            }
                            else
                            {
                                if (sampleTimestamp < sampleStartTimestamp)
                                {
                                    sampleStartTimestamp = sampleTimestamp;
                                }
                            }

                            if (sampleMaxTimestamp == 0)
                            {
                                sampleMaxTimestamp = sampleTimestamp;
                                isMax = true;
                            }
                            else
                            {
                                if (sampleTimestamp > sampleMaxTimestamp)
                                {
                                    sampleMaxTimestamp = sampleTimestamp;
                                    isMax = true;
                                }
                            }
                        }

                        CsvFileHelper csv = new CsvFileHelper(mCsvInfoList[i - 1].FilePath);

                        List<List<string>> csvData = csv.GetListCsvData();

                        for (int j = 0; j < csvData.Count; j++)
                        {
                            csvData[j].Insert(0, frame.ToString());
                            csvData[j][1] = sampleTimestamp.ToString() + csvData[j][1].PadLeft(4, '0');
                            if (i <= 1 && j < 10)
                            {
                                csvDataTotal.Add(CertaintyDataType(csvData[j]));
                            }
                            else
                            {
                                csvDataTotal.Add(csvData[j]);
                            }

                            if (isMax)
                            {
                                if (csvData[j].Count > 4)
                                {
                                    float totalTime = 0.0f;
                                    float.TryParse(csvData[j][3], out totalTime);
                                    sumTotalTime += totalTime;
                                }
                            }

                            frame++;
                        }

                        csv.Dispose();

                        if (isMax)
                        {
                            maxTotalTime = sumTotalTime;
                        }
                    }
                    else
                    {
                        Log.Print.Warn("待合并的文件不存在：" + File.Exists(mCsvInfoList[i - 1].FilePath));
                    }

                }
                filename = mTaskAO.GameID + "_" + mTaskAO.UUID + ".csv";
                string uploadname = GetBucket() + "/" + filename;
                CsvFileHelper.SaveCsvFile(Path.Combine(GetSavePath(), filename), csvDataTotal);
                string uploadurl = string.Format("{0}:{1}", mTaskAO.UploadIp, mTaskAO.UploadPort);
                MinIO minio = new MinIO();
                if (mTaskAO.AnalyzeBucket.Contains('/'))
                {
                    string bucket = "analysisdata";
                    minio.UploadFileAsync(Path.Combine(GetSavePath(), filename), uploadurl, bucket, uploadname);
                }
                else
                {
                    string bucket = mTaskAO.AnalyzeBucket;
                    minio.UploadFileAsync(Path.Combine(GetSavePath(), filename), uploadurl, bucket, filename);
                }
                return true;
            }

            catch (Exception e)
            {
                Log.Print.Error(e.ToString());
                string message = "csv文件合并失败,请检查！！！";
                SendText(mTaskAO.GameID, mTaskAO.UUID, message);
                return false;
            }
        }

        private void SuccessCase(string filename, string[] screenFiles, int sampleStartTimestamp, int sampleMaxTimestamp, float maxTotalTime)
        {
            if (!string.IsNullOrEmpty(mZipFilePath))
            {
                mTaskAO.ScreenState = (int)Get_ScreenState.Get_Doing;
                DB.Instance.Update<AnalyzeTask>(mTaskAO._id, mTaskAO, DB.database);
                if (File.Exists(mZipFilePath))
                {
                    if (UnZipScreen(out screenFiles))
                    {
                        mIsUnZipSuccess = true;
                        mScreenState = Get_ScreenState.Get_Down;
                    }
                }
            }
            else
            {
                Log.Print.Warn("任务ID=" + ID + "获取zip文件为空");
                mScreenState = Get_ScreenState.Get_None;
                SendText(mTaskAO.GameID, mTaskAO.UUID, "获取截图zip失败");
            }

            SuccessMerge(filename, screenFiles, sampleStartTimestamp, sampleMaxTimestamp, maxTotalTime);

            SetEnd();
        }
    }
}
