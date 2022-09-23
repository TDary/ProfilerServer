using Minio;
using Minio.Exceptions;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace UAuto
{
    public class MinIO
    {

        //public static MinIO Instance { get; } = new MinIO();
        public static string IP = string.Empty;

        public static string Port = string.Empty;

        private static string mAccessKey = string.Empty;

        private static string mSecretKey = string.Empty;

        public static MinioClient miniominioClient;
        public void Init()
        {
            FilterDefinition<UAutoServer> filter = Builders<UAutoServer>.Filter.Eq("Worker_Type", 3);
            UAutoServer data = DB.Instance.FindOne<UAutoServer>(filter, DB.database);
            IP = data.IP;
            Port = data.Port;
            mAccessKey = data.access_key;
            mSecretKey = data.secret_key;
            string url = string.Format("{0}:{1}", IP, Port);
            miniominioClient = new MinioClient(url, mAccessKey, mSecretKey);//.WithSSL();
        }

        public static bool DownLoadFile(string bucket, string objectName, string filePath)
        {
            try
            {
                Task task = miniominioClient.GetObjectAsync(bucket, objectName, filePath);
                task.Wait();
                return true;
            }
            catch (MinioException e)
            {
                Log.Print.Warn("下载文件失败：" + e);
                return false;
            }
        }

        // File uploader task.
        public async static Task<bool> DownloadAsync(string url, string bucket, string objectName, string filePath)
        {
            

            //var bucketName = "test1-test1";
            ////var location = "us-east-1";
            //var objectName = "2021-05-21-17-43-53-0.raw";
            //var filePath = @"D:\abc1\2021-05-21-17-43-53-0.raw";
            ////var contentType = "application/zip";

            try
            {
                // Check whether the object exists using statObjectAsync().
                // If the object is not found, statObjectAsync() throws an exception,
                // else it means that the object exists.
                // Execution is successful.
                await miniominioClient.StatObjectAsync(bucket, objectName);

                // Gets the object's data and stores it in photo.jpg
                await miniominioClient.GetObjectAsync(bucket, objectName, filePath);

                //Console.Out.WriteLine("下载成功 ");
                //Log.Print.Info(string.Format("{0},{1},{2}下载成功", url, bucket, objectName));

                return true;

            }
            catch (MinioException e)
            {
                //Console.Out.WriteLine("Error occurred: " + e);
                Log.Print.Warn(string.Format("{0},{1},{2}下载Error:{3}", url, bucket, objectName, e));

                return false;
            }
        }

        public async Task UploadFileAsync(string filePath, string url, string bucket, string objectName)
        {
            try
            {
                await miniominioClient.PutObjectAsync(bucket, objectName, filePath).ConfigureAwait(false);
            }
            catch(MinioException e)
            {
                Log.Print.Warn("上传csv文件失败。Error：" + e);
            }
        }
        //// File uploader task.
        //private async static Task UploadRun(MinioClient minio)
        //{
        //    var bucketName = "test-test1";
        //    var location = "us-east-1";
        //    var objectName = "2021-05-19-21-08-20-0.raw";
        //    var filePath = @"D:\2021-05-19-21-08-20-0.raw";
        //    //var contentType = "application/zip";

        //    try
        //    {
        //        // Make a bucket on the server, if not already present.
        //        bool found = await minio.BucketExistsAsync(bucketName);
        //        if (!found)
        //        {
        //            await minio.MakeBucketAsync(bucketName, location);
        //        }
        //        // Upload a file to bucket.
        //        await minio.PutObjectAsync(bucketName, objectName, filePath);

        //        Log.Print.Info("Successfully上传");
        //        Console.WriteLine("Successfully uploaded " + objectName);
        //    }
        //    catch (MinioException e)
        //    {
        //        Console.WriteLine("File Upload Error: {0}", e.Message);
        //    }
        //}


        //// File uploader task.
        //private async static Task DownloadRun(MinioClient minio)
        //{
        //    var bucketName = "test1-test1";
        //    //var location = "us-east-1";
        //    var objectName = "2021-05-21-17-43-53-0.raw";
        //    var filePath = @"D:\abc1\2021-05-21-17-43-53-0.raw";
        //    //var contentType = "application/zip";


        //    try
        //    {
        //        // Check whether the object exists using statObjectAsync().
        //        // If the object is not found, statObjectAsync() throws an exception,
        //        // else it means that the object exists.
        //        // Execution is successful.
        //        await minio.StatObjectAsync(bucketName, objectName);

        //        // Gets the object's data and stores it in photo.jpg
        //        await minio.GetObjectAsync(bucketName, objectName, filePath);

        //        Console.Out.WriteLine("下载成功 ");

        //    }
        //    catch (MinioException e)
        //    {
        //        Console.Out.WriteLine("Error occurred: " + e);
        //    }
        //}

    }
}
