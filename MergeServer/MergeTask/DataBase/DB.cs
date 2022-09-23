using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Newtonsoft.Json;
using System.Threading.Tasks;
using MongoDB.Driver.GridFS;
using System.IO;
using System.Xml.Serialization;

namespace UAuto
{
    public class DB
    {
        private static DB _instance = new DB();
        public static DB Instance => _instance;
        //数据库名称
        private string dbName = null;

        private DB()
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

            // 初始化数据库
            dbName = mServerConfig.database.DatabaseName;

            MongoClientSettings setting = new MongoClientSettings();
            setting.MaxConnectionPoolSize = 5;
            setting.Server = new MongoServerAddress(mServerConfig.database.DatabaseIP);
            client = new MongoClient(setting);
            //获得数据库cnblogs
            database = client.GetDatabase(dbName);
        }

        public static IMongoDatabase database;

        private MongoClient client;
        private ServerConfig mServerConfig;

        //删除案例的存储桶以及所有文件
        public void DropFile(string uuid, IMongoDatabase db)
        {
            var bucket = new GridFSBucket(db, new GridFSBucketOptions
            {
                BucketName = uuid,
            });

            bucket.Drop();
        }

        public void Drop(string collection, IMongoDatabase db)
        {
            //删除集合
            db.DropCollection(collection);
        }

        public void Inserts<T>(List<T> ts, IMongoCollection<T> col)
        {
            try
            {
                //执行非顺序插入操作
                InsertManyOptions ordere = new InsertManyOptions();
                ordere.IsOrdered = false;
                ordere.BypassDocumentValidation = true;

                //执行插入操作
                col.InsertMany(ts, ordere);
            }
            catch(Exception e)
            {
                Log.Print.Info(e);
            }
        }
        public void InsertsFunrow<T>(List<T> ts, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);

            //执行非顺序插入操作
            InsertManyOptions ordere = new InsertManyOptions();
            ordere.IsOrdered = false;
            ordere.BypassDocumentValidation = true;
            col.InsertMany(ts, ordere);
        }

        public void InsertFunRowTop<T>(T t,string collection, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(collection);

            //执行插入操作
            col.InsertOne(t);
        }

        //插入逻辑
        public void Insert<T>(T t, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(t.GetType().Name);

            //执行插入操作
            col.InsertOne(t);
        }

        //public void Insert<T>(T t, IMongoDatabase db)
        //{
        //    //获得集合,如果数据库中没有，先新建一个
        //    IMongoCollection<T> col = db.GetCollection<T>(t.GetType().Name);

        //    //执行插入操作
        //    col.InsertOne(t);
        //}

        //异步插入逻辑
        public void InsertAsync<T>(T t, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(t.GetType().Name);

            //执行插入操作
            col.InsertOneAsync(t);
        }

        //插入函数哈希值名
        public void InsertFunhash<T>(string collection, T t, FilterDefinition<T> query, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(collection);

            var find = col.Find(query);

            if (find.FirstOrDefault() == null)
            {
                col.InsertOne(t);
            }
        }

        public IMongoCollection<T> GetConnect<T>(string collection, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(collection);

            return col;
        }

        //插入函数哈希值名
        public void IntsertFunhash<T>(IMongoCollection<T> col, FilterDefinition<T> query, UpdateDefinition<T> update)
        {
            col.UpdateOne(query, update, new UpdateOptions() { IsUpsert = true });
        }


        public void Update<T>(ObjectId id, T t, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(t.GetType().Name);

            // 更新
            FilterDefinition<T> filter = Builders<T>.Filter.Eq("_id", id);
            col.ReplaceOne(filter, t);
        }

        public void UpdateAsync<T>(ObjectId id, T t, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(t.GetType().Name);

            // 更新
            FilterDefinition<T> filter = Builders<T>.Filter.Eq("_id", id);
            col.ReplaceOneAsync(filter, t);
        }

        public void UpdateTong<T>(ObjectId id, T t, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(t.GetType().Name);

            // 更新
            FilterDefinition<T> filter = Builders<T>.Filter.Eq("_id", id);
            col.ReplaceOne(filter, t);
        }

        public bool Reanalyze<T>(Expression<Func<T, bool>> filter, FilterDefinition<T> query, UpdateDefinition<T> update, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);
            var find = col.Find(query);
            if (find != null)
            {
                col.UpdateManyAsync<T>(filter, update);
                return true;
            }
            return false;
        }

        public void UpdateTaskAsync<T>(FilterDefinition<T> query, UpdateDefinition<T> update, IMongoDatabase db)
        {
            var collection = db.GetCollection<T>(typeof(T).Name);

            collection.UpdateOneAsync(query, update);

        }

        public bool UpdateState<T>(FilterDefinition<T> query, UpdateDefinition<T> update, IMongoDatabase db)
        {
            var collection = db.GetCollection<T>(typeof(T).Name);

            collection.UpdateOne(query, update);

            return true;
        }

        public bool FindAndModify<T>(FilterDefinition<T> query, UpdateDefinition<T> update, IMongoDatabase db)
        {
            var collection = db.GetCollection<T>(typeof(T).Name);

            var fmr = collection.UpdateOne(query, update);

            if (fmr == null)
            {
                return false;
            }

            if (fmr.ModifiedCount == 0)
            {
                return false;
            }

            return true;
        }

        public T FindOne<T>(FilterDefinition<T> query, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);
            var find = col.Find(query);
            T data = default(T);
            if (find.FirstOrDefaultAsync().Result != null)
            {
                data = find.FirstOrDefaultAsync().Result;
            }

            return data;
        }

        public List<T> FindList<T>(FilterDefinition<T> query, IMongoDatabase db)
        {
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);
            List<T> list = new List<T>();

            var find = col.Find<T>(query);
            if (find != null)
            {
                list = find.ToCursor().ToList();
            }

            return list;
        }

        public IAsyncCursor<T> Find<T>(FilterDefinition<T> query, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);

            IAsyncCursor<T> mc = col.FindSync<T>(query);

            return mc;

        }

        public T FindOne<T>(ObjectId id, IMongoDatabase db)
        {
            //获得集合,如果数据库中没有，先新建一个
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);
            T data = default(T);
            FilterDefinition<T> query = Builders<T>.Filter.Eq("_id", id);
            var find = col.Find<T>(query);
            if (find.FirstOrDefaultAsync().Result != null)
            {
                data = find.FirstOrDefaultAsync().Result;
            }

            return data;
        }

        public T FindOne<T>(string id)
        {
            return FindOne<T>(ObjectId.Parse(id), database);
        }

        //删除案例
        public void DropCase<T>(FilterDefinition<T> query, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);
            col.DeleteManyAsync(query);
        }

        public void DropTopRow<T>(string collection, FilterDefinition<T> query, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(collection);
            col.DeleteManyAsync(query);
        }

        //删除子任务表数据和原始数据指向
        public void DeleteSub<T>(FilterDefinition<T> query, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);
            col.DeleteManyAsync(query);
        }

        //设置案例状态
        public void SetSubTaskState<T>(Expression<Func<T, bool>> query, UpdateDefinition<T> update, IMongoDatabase db)
        {
            //获取Users集合
            IMongoCollection<T> col = db.GetCollection<T>(typeof(T).Name);
            col.UpdateOne<T>(query, update);
        }

        public List<T> FindList2<T>(FilterDefinition<T> query, string collection, IMongoDatabase db)
        {
            IMongoCollection<T> col = db.GetCollection<T>(collection);
            List<T> list = new List<T>();

            var find = col.Find<T>(query);
            if (find != null)
            {
                list = find.ToCursor().ToList();
            }

            return list;
        }

    }
}
