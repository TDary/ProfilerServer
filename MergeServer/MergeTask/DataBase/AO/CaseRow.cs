using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class CaseRow
    {
        public ObjectId _id;    //BsonType.ObjectId 这个对应了 MongoDB.Bson.ObjectId

        //总帧数
        public int frame { get; set; }

        //函数名hash
        public long name { get; set; }

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
}
