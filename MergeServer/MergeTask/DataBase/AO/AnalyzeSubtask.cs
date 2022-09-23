using MongoDB.Bson;

namespace UAuto
{

    public enum EAnalyzeSubtaskState
    {
        EAS_Standby = 0,   //待接取
        EAS_Success = 1,    //成功完成
        EAS_Execution = 2,  //执行中
        EAS_NotFound = 100,   //找不到要解析的文件
        EAS_NoUnityversion = 101,   //没有对应的unity
        EAS_Exception = 102,    //解析异常
        EAS_CSVNotFound = 103,    //结果丢失
        EAS_FunjsonNotFound = 104,    //结果丢失
    }

    public class AnalyzeSubTask
    {
        public ObjectId _id;    //BsonType.ObjectId 这个对应了 MongoDB.Bson.ObjectId  　　　　

        public string UUID { set; get; }

        public string Worker { get; set; }

        public string Minio_Path { get; set; }

        public int Status { get; set; }

        public long BeginTime { get; set; }

        public long EndTime { get; set; }

        public int RetryCnt { get; set; }
    }
}
