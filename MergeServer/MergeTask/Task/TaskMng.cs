using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace UAuto
{
    public class TaskMng
    {
        private static Dictionary<string, bool> CheckCase = new Dictionary<string, bool>(30);

        /// <summary>
        /// 并行任务列表（按最大值初始化）
        /// </summary>
        private List<TaskBase> mExecuteTaskList = new List<TaskBase>();

        /// <summary>
        /// 清理15天前的案例计时器
        /// </summary>
        private float mCleanCaseTimer = 0.0f;

        /// <summary>
        /// 清理磁盘计时器
        /// </summary>
        private float mCleanTimer = 0.0f;

        /// <summary>
        /// 初始化任务管理器
        /// </summary>
        /// <param name="maxTask">最大任务数</param>
        public void Init(int maxTask)
        {
            for (int i = 0; i < maxTask; i++)
            {
                mExecuteTaskList.Add(null);
            }
        }

        public void Update(float deltaTime)
        {

            //磁盘监控，达到设定会自动删除无用文件
            if (ProfilerAnalyzer.Instance.Config.uploadDir.AutoClean == "true")
            {
                mCleanTimer += deltaTime;
                if (mCleanTimer > 5.0f)
                {
                    mCleanTimer = 0.0f;
                    CleanDisk();
                }
            }
            //清除15天以前的案例文件夹
            if (ProfilerAnalyzer.Instance.Config.uploadDir.DeleteCase == "true")
            {
                mCleanCaseTimer += deltaTime;
                if (mCleanCaseTimer > 28800.0f)
                {
                    mCleanCaseTimer = 0.0f;
                    CleanCase();
                }
            }

            int freeTaskIndex = GetFreeExecuteTask();
            if (freeTaskIndex != -1)
            {
                TaskBase task = TryCreateTask();
                if (task != null)
                {
                    mExecuteTaskList[freeTaskIndex] = task;
                    task.Index = freeTaskIndex;
                    task.Begin();
                }
            }
            for (int i = 0; i < mExecuteTaskList.Count; i++)
            {
                TaskBase task = mExecuteTaskList[i];
                if (task != null)
                {
                    if (task.IsEnd())
                    {
                        task.Release();
                        mExecuteTaskList[i] = null;
                    }
                    else
                    {
                        task.Update(deltaTime);
                    }
                }
            }
        }

        /// <summary>
        /// 获取空闲的任务空位索引
        /// </summary>
        /// <returns></returns>
        public int GetFreeExecuteTask()
        {
            for (int i = 0; i < mExecuteTaskList.Count; i++)
            {
                if (mExecuteTaskList[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 根据数据库中的ID查找当前执行的任务
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public TaskBase GetExecuteTask(string id)
        {
            for (int i = 0; i < mExecuteTaskList.Count; i++)
            {
                if (mExecuteTaskList[i] != null)
                {
                    if (mExecuteTaskList[i].ID == id)
                    {
                        return mExecuteTaskList[i];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 创建一个任务
        /// 可以是任何任务，按照设计的优先级来
        /// </summary>
        /// <returns></returns>
        public TaskBase TryCreateTask()
        {
            //1.僵尸任务，宕机情况下
            TaskBase task_zst = RequestZombieTask();

            if (task_zst != null)
            {
                return task_zst;
            }
            // 2.查找可进行合并的任务
            TaskBase task_mt = RequestMergeTask();

            if (task_mt != null)
            {
                return task_mt;
            }

            return null;
        }

        /// <summary>
        /// 申请一个合并任务
        /// </summary>
        /// <returns></returns>
        private TaskBase RequestMergeTask()
        {
            FilterDefinition<AnalyzeTask> query = Builders<AnalyzeTask>.Filter.Eq("TaskState", EAnalyzeTaskState.EAT_Wait);
            IAsyncCursor<AnalyzeTask> atMC = DB.Instance.Find<AnalyzeTask>(query, DB.database);

            List<AnalyzeTask> etor = atMC.ToList();

            for (int i = 0; i < etor.Count; i++)
            {
                AnalyzeTask at = etor[i];
                // 竞争该任务
                if (AllSubtaskSuccess(at))
                {
                    //保证当前解析器没有进行此合并任务，且符合条件才允许申请合并任务并开始拉取分析文件进行合并
                    TaskBase task = GetExecuteTask(at._id.ToString());
                    if (task == null)
                    {
                        Log.Print.Info("成功申请合并任务：" + at._id.ToString());
                        TaskMerge taskMerge = CreateAnayze(at);
                        return taskMerge;
                    }
                }
            }
            Thread.Sleep(1000);
            return null;
        }

        /// <summary>
        /// 申请属于本机的任务，但没执行完也没在执行中，这类僵尸任务（例如机器重启）
        /// </summary>
        private TaskBase RequestZombieTask()
        {
            // 僵尸父任务
            {
                FilterDefinitionBuilder<AnalyzeTask> filterDefinitionBuilder = new FilterDefinitionBuilder<AnalyzeTask>();
                FilterDefinition<AnalyzeTask> query = filterDefinitionBuilder.And(
                        filterDefinitionBuilder.Eq("TaskState", EAnalyzeTaskState.EAT_Execution),
                        filterDefinitionBuilder.Eq("WorkerName", ProfilerAnalyzer.Instance.Config.serverUrl.WorkerName));
                IAsyncCursor<AnalyzeTask> atMC = DB.Instance.Find<AnalyzeTask>(query, DB.database);

                List<AnalyzeTask> etor = atMC.ToList();

                for (int i = 0; i < etor.Count; i++)
                {
                    AnalyzeTask at = etor[i];
                    TaskBase task = GetExecuteTask(at._id.ToString());

                    //说明本机没有在执行
                    if (task == null)
                    {
                        // 构造该项目（不需要竞争，因为任务本来就是自己的）
                        TaskMerge taskMerge = CreateAnayze(at);
                        return taskMerge;
                    }
                }
            }
            Thread.Sleep(1000);
            return null;
        }


        private TaskMerge CreateAnayze(AnalyzeTask task)
        {
            TaskMerge tm = new TaskMerge(task._id.ToString());
            return tm;
        }

        public async static Task GetFile(string url, string bucket, string filename, Action<string> callback)
        {
            if (bucket.Contains('/'))
            {
                bucket = bucket.Substring(13, bucket.Length - 13);
            }
            string localpath = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, bucket);
            string localfile = Path.Combine(localpath, filename);

            // 先判断本地有没有
            if (File.Exists(localfile))
            {
                if (callback != null)
                {
                    callback.Invoke(localfile);
                }

                return;
            }

            // 本地创建文件夹
            if (!Directory.Exists(localpath))
            {
                //目标目录不存在则创建
                try
                {
                    Directory.CreateDirectory(localpath);
                }
                catch (Exception ex)
                {
                    throw new Exception("创建目录" + localpath + "失败：" + ex.Message);
                }
            }

            string getUrl = "http://" + url + "/" + "analysisdata" + "/" + bucket + "/" + filename;

            bool isSuccess = await HttpDownloadFile(getUrl, localfile);
            if (!isSuccess)
            {
                //尝试使用另一个路径
                string gurl = "http://" + url.Substring(0, url.Length - 5) + "/" + "analysisdata" + "/" + bucket + "/" + filename;
                bool succ = await HttpDownloadFile(gurl, localfile);
                if (!succ)
                {
                    string oldurl = "http://" + url + "/" + bucket + "/" + filename;
                    bool sucsc = await HttpDownloadFile(oldurl, localfile);
                    if (!sucsc)
                    {
                        string message = "获取文件失败,Url:" + getUrl + "FileName:" + filename + "localfile:" + localfile;
                        Log.Print.Warn(message);
                        SendText(message);
                        return;
                    }
                    else
                    {
                        callback.Invoke(localfile);
                    }
                }
                else
                {
                    callback.Invoke(localfile);
                }
            }
            else
            {
                if (callback != null)
                {
                    callback.Invoke(localfile);
                }

                return;
            }

        }

        /// <summary>
        /// 获取分析后的文件
        /// </summary>
        /// <param name="url"></param>
        /// <param name="bucket"></param>
        /// <param name="filename"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public async static Task GetAnalyzeFile(string url, string uuid, string bucket, string filename, Action<string> callback)
        {
            if (bucket.Contains('/'))
            {
                bucket = bucket.Substring(13, bucket.Length - 13);
            }
            string localpath = Path.Combine(ProfilerAnalyzer.Instance.Config.uploadDir.Dir, bucket);
            string localfile = Path.Combine(localpath, filename);

            // 先判断本地有没有
            if (File.Exists(localfile))
            {
                if (callback != null)
                {
                    callback.Invoke(localfile);
                }

                return;
            }

            // 本地创建文件夹
            if (!Directory.Exists(localpath))
            {
                //目标目录不存在则创建
                try
                {
                    Directory.CreateDirectory(localpath);
                }
                catch (Exception ex)
                {
                    throw new Exception("创建目录" + localpath + "失败：" + ex.Message);
                }
            }


            // 尝试从默认地址下载（http下载）
            string getUrl = "http://" + url.Substring(0, url.Length - 2) + ":8600" + "/" + uuid + "/" + filename;

            bool isSuccess = await HttpDownloadFile(getUrl, localfile);
            if (!isSuccess)
            {
                Log.Print.Warn("UUID:" + uuid + "，该案例获取分析文件失败，url：" + getUrl + "，开始重新获取文件----");
                Thread.Sleep(10000);
                bool reSuccess = await HttpDownloadFile(getUrl, localfile);
                if (!reSuccess)
                {
                    Log.Print.Warn("UUID:" + uuid + "，该案例获取分析文件失败，url：" + getUrl + "，开始重新获取文件----");
                    bool thirdSuccess = await HttpDownloadFile(getUrl, localfile);
                    if (!thirdSuccess)
                    {
                        if (!CheckCase.ContainsKey(uuid) && CheckCase.Count <= 30)
                        {
                            string errorMessage = "UUID:" + uuid + "，该案例获取分析文件失败，url：" + getUrl + "，已重试3次";
                            CheckCase.Add(uuid, true);
                            SendText(errorMessage);
                            CheckCase[uuid] = false;
                            Log.Print.Warn("获取分析文件失败,Url:" + getUrl);
                            ChangeState(uuid);
                        }
                        if (CheckCase.Count == 30)
                        {
                            ClearCheckCase();
                        }
                    }
                    else
                    {
                        callback.Invoke(localfile);
                    }
                }
                else
                {
                    callback.Invoke(localfile);
                }
                return;
            }
            else
            {
                if (callback != null)
                {
                    //Log.Print.Info(string.Format("从默认地址获取数据文件成功 {0}", localfile));
                    callback.Invoke(localfile);
                }

                return;
            }
        }

        private static void ChangeState(string uuid)
        {
            FilterDefinition<AnalyzeTask> query = Builders<AnalyzeTask>.Filter.Eq("UUID", uuid);
            UpdateDefinition<AnalyzeTask> update = Builders<AnalyzeTask>.Update.Set("TaskState", (int)EAnalyzeTaskState.EAT_GetFilesTimeOut);
            DB.Instance.UpdateState<AnalyzeTask>(query, update, DB.database);
        }

        private static void ClearCheckCase()
        {
            foreach (var item in CheckCase)
            {
                if (!item.Value)
                {
                    CheckCase.Remove(item.Key);
                }
            }
        }

        /// <summary>
        /// 发送飞书机器人消息
        /// </summary>
        /// <param name="Messagetext"></param>
        public static void SendText(string Messagetext)
        {
            string robotUrl = ProfilerAnalyzer.Instance.Config.RobotMonitor.FeishuRobot;
            var content = new { text = Messagetext };
            var data = new { msg_type = "text", content };
            MHttpSender.SendPostJson(robotUrl, JsonConvert.SerializeObject(data));
        }

        /// <summary>
        /// 清除磁盘空间功能
        /// </summary>
        public void CleanDisk()
        {
            float freedisk = GetHardDiskFreeSpace(ProfilerAnalyzer.Instance.Config.uploadDir.CheckDisk);
            string fileName = ProfilerAnalyzer.Instance.Config.uploadDir.Dir;
            string serverurl = ProfilerAnalyzer.Instance.Config.serverUrl.AnayzeServerIP;
            if (freedisk < float.Parse(ProfilerAnalyzer.Instance.Config.uploadDir.CheckDiskSize))
            {
                Log.Print.Warn("磁盘空间剩余大小：" + freedisk);
                string warnText = serverurl + "解析服务器剩余磁盘空间:" + freedisk + "G。启动清理空间功能！";
                SendText(warnText);
                System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(fileName);
                System.IO.DirectoryInfo[] dirs = dir.GetDirectories();
                if (dirs.Length > 0)
                {
                    foreach (var childfile in dirs)
                    {

                        if (childfile.GetFiles().Length > 0 || childfile.GetDirectories().Length > 0)
                        {
                            System.IO.FileInfo[] allfile = childfile.GetFiles();
                            foreach (var child in allfile)
                            {
                                if (child.Name.Contains("raw") && File.GetLastWriteTime(child.FullName) < DateTime.Now.AddHours(-12))
                                {
                                    File.Delete(child.FullName);
                                    Log.Print.Info("成功删除文件：" + child.FullName);
                                }
                            }
                        }
                        else if (childfile.GetDirectories().Length == 0 && childfile.GetFiles().Length == 0)
                        {
                            if (childfile.LastWriteTime < DateTime.Now.AddDays(-2))
                            {
                                childfile.Delete();
                                Log.Print.Info("成功删除两天以前的空文件夹：" + childfile);
                            }
                        }
                    }

                }
            }
        }

        /// <summary>
        /// 删除解析器本地案例截图文件功能
        /// </summary>
        public void CleanCase()
        {
            FilterDefinition<AnalyzeTask> query = Builders<AnalyzeTask>.Filter.Empty;
            List<AnalyzeTask> alltask = DB.Instance.FindList<AnalyzeTask>(query, DB.database);
            string localPath = ProfilerAnalyzer.Instance.Config.uploadDir.Dir;
            foreach (var task in alltask)
            {
                string testTime = task.TestBegin;
                string casep = task.AnalyzeBucket;
                string casePath = Path.Combine(localPath, casep);
                if (Convert.ToDateTime(testTime) < DateTime.Now.AddDays(-15))
                {
                    if (Directory.Exists(casePath))
                    {
                        Directory.Delete(casePath, true);
                        FilterDefinition<AnalyzeTask> q1 = Builders<AnalyzeTask>.Filter.Eq("_id", task._id);
                        UpdateDefinition<AnalyzeTask> update = Builders<AnalyzeTask>.Update.Set("ScreenState", 0);
                        DB.Instance.FindAndModify<AnalyzeTask>(q1, update, DB.database);
                        Log.Print.Info("已删除15天前的案例文件夹：" + casep);
                    }
                }
            }
        }

        /// <summary>
        /// 通过http下载文件
        /// </summary>
        /// <param name="remoteUrl"></param>
        /// <param name="localPath"></param>
        /// <returns></returns>
        public async static Task<bool> HttpDownloadFile(string remoteUrl, string localPath)
        {
            try
            {
                //设置参数
                HttpWebRequest request = WebRequest.Create(remoteUrl) as HttpWebRequest;
                //发送请求并获取相应的回应数据
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                //知道request.GetResponse()程序才开始向目标网页发送请求
                Stream responseStream = response.GetResponseStream();

                //创建本地文件写入流
                Stream stream = new FileStream(localPath, FileMode.Create);
                byte[] bArr = new byte[1024];
                int size = responseStream.Read(bArr, 0, (int)bArr.Length);
                await Task.Run(() =>
                {
                    while (size > 0)
                    {
                        stream.Write(bArr, 0, size);
                        size = responseStream.Read(bArr, 0, (int)bArr.Length);
                    }
                });
                stream.Close();
                responseStream.Close();
                return true;
            }
            catch(Exception ex)
            {
                Log.Print.Error(ex);
                return false;
            }
        }

        ///   
        /// 获取指定驱动器的剩余空间总大小(单位为GB) 
        ///   
        ///  只需输入代表驱动器的字母即可  
        ///    
        public static float GetHardDiskFreeSpace(string str_HardDiskPath)
        {
            float freeSpace = 0.0f;
            if (!str_HardDiskPath.StartsWith("/"))
            {
                str_HardDiskPath = str_HardDiskPath + ":\\";
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
                if (drives.Length != 0)
                {
                    foreach (System.IO.DriveInfo drive in drives)
                    {
                        if (drive.Name == str_HardDiskPath)
                        {
                            freeSpace = drive.TotalFreeSpace / (float)(1024 * 1024 * 1024);
                        }
                    }
                }
            }
            if (freeSpace == 0.0f)
            {
                string printLine = " awk '{print $2,$3,$4,$5}'";
                string shellLine = string.Format("df -k {0} |", str_HardDiskPath) + printLine;
                //Log.Print.Info(string.Format("执行命令:{0}", shellLine));
                Process p = new Process();
                p.StartInfo.FileName = "sh";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.StandardInput.WriteLine(shellLine);
                p.StandardInput.WriteLine("exit");
                string strResult = p.StandardOutput.ReadToEnd();
                //Log.Print.Info(string.Format("输出结果：{0}", strResult));
                string[] arr = strResult.Split('\n');
                if (arr.Length == 0)
                {
                    return freeSpace;
                }
                string[] resultArray = arr[1].TrimStart().TrimEnd().Split(' ');
                if (resultArray == null || resultArray.Length == 0)
                {
                    return freeSpace;
                }
                freeSpace = Convert.ToInt32(resultArray[2]) / (float)(1024 * 1024);
            }
            return freeSpace;
        }

        private bool AllSubtaskSuccess(AnalyzeTask nowtask)
        {
            bool isCanDo = true;
            // 验证每一个子任务是否完成
            FilterDefinition<AnalyzeSubTask> query = Builders<AnalyzeSubTask>.Filter.Eq("UUID", nowtask.UUID);
            IAsyncCursor<AnalyzeSubTask> atMC = DB.Instance.Find<AnalyzeSubTask>(query, DB.database);
            List<AnalyzeSubTask> etor = atMC.ToList();
            if (etor.Count == 0 || etor.Count != nowtask.RawFiles.Length)
            {
                return false;
            }
            for (int i = 0; i < etor.Count; i++)
            {
                AnalyzeSubTask sub = etor[i];
                if (sub.Status != (int)EAnalyzeSubtaskState.EAS_Success)
                {
                    return false;
                }
            }
            return isCanDo;
        }

    }

}
