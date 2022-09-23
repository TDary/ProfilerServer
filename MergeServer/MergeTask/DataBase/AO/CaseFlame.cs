using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto 
{
   public  class CaseFlame
    {
        public ObjectId _id;    //BsonType.ObjectId 这个对应了 MongoDB.Bson.ObjectId

        public string case_uuid;

        public int frame_id;

        public SubData flame { get; set; }
    }
}
