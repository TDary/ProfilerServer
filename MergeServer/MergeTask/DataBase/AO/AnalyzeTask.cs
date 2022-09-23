using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{

    public enum EAnalyzeTaskState
    {
        EAT_Retrieval = 50,  //开始解析前检索，状态文件检索中
        EAT_Wait = 0,   //等待子任务完成, 状态：解析中
        EAT_Success = 1,    //已解析完毕，状态：完成
        EAT_Standby = 2,       //等待接取合并任务，状态：解析中
        EAT_Execution = 3,   //合并文件中，状态：解析中
        EAT_CantSubtask = 100,   //子任务缺失，状态：错误
        EAT_SubtaskFaild = 101,   //子任务失败，状态：错误
        EAT_SubtaskNotFound = 102,    //子任务解析结果找不到，状态：错误
        EAT_GetFilesTimeOut = 103,    //获取文件超时，状态：错误
        EAT_IsDeleted = 500,          //案例的数据已被删除，状态：已删除
        EAT_Recovering = 600,        //转存案例源文件回复中，状态：错误
        EAT_Error = 404,             //请求接口失败，状态：错误
        EAT_MergeError = 400, //合并失败
    }

    public enum ESwitch
    {
        ENone = 0,                        //都未开启
        EResource_Analyze = 1,    //开启资源分析
        ECustom_Analyze = 2, //开启自定义数据采集
    }

    /// <summary>
    /// 解析任务表
    /// </summary>
    public class AnalyzeTask
    {

        public ObjectId _id;    //BsonType.ObjectId 这个对应了 MongoDB.Bson.ObjectId  　　　　

        public string GameName { get; set; }

        public string CaseName { get; set; }

        //public string CaseID { get; set; }

        public string DeviceInfo { get; set; }

        public string AnalyzeCreatetime { set; get; }

        public int CreateTimeStamp { set; get; }

        public string GameID { get; set; }

        public string UUID { set; get; }

        public string[] RawFiles { set; get; }

        public string[] SnapFiles { set; get; }

        public string UploadIp { set; get; }

        public string UploadPort { set; get; }

        public string MigrateIp { set; get; }

        public string TestBegin { set; get; }

        public string TestEnd { set; get; }

        public int FilesHash { set; get; }

        public string UnityVersion { set; get; }

        public string GameVersion { get; set; }

        /// <summary>
        /// anayzetype：解析类型，现在没有，防止以后有浅层解析，深度解析，等等
        /// </summary>
        public string AnalyzeType { set; get; }

        public int TaskID { set; get; }

        /// <summary>
        /// 对应EAnayzeTaskState
        /// </summary>
        public int TaskState { set; get; }

        public string WorkerName { set; get; }

        public string AnalyzeURL { set; get; }

        public string AnalyzeBucket { set; get; }

        public string AnalyzeFile { set; get; }

        public string AnalyzeStarttime { set; get; }

        public string AnalyzeEndtime { set; get; }

        public int StartTimeStamp { set; get; }

        public int EndTimeStamp { set; get; }

        public string[] ScreenFiles { set; get; }

        public int ScreenState { set; get; }

        public int SampleStartTimeStamp { set; get; }

        public int SampleEndTimeStamp { set; get; }

        public string Tag { set; get; }

        public string Machine { set; get; }

        public int TotalFrame { get; set; }

        public string Desc { set; get; }

        public bool ShieldSwitch { set; get; }

        public int SwitchMode { get; set; }
    }
}
