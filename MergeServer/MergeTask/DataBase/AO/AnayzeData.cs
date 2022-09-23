using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class AnayzeData
    {
        public ObjectId _id;    //BsonType.ObjectId 这个对应了 MongoDB.Bson.ObjectId  　　　　

        public string GameID { get; set; }

        public string UUID { set; get; }

        public int State { set; get; }

        public string DataUrl { set; get; }

        public string BucketName { set; get; }

        public string DataName { set; get; }

        public string AnayzeStarttime { set; get; }

        public string AnayzeEndtime { set; get; }

        public int TimeStamp { set; get; }

    }

}
