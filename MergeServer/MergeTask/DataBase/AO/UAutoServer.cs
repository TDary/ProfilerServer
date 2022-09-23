using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class UAutoServer
    {
        public ObjectId _id { get; set; }
        public int Worker_Type { get; set; }
        public string IP { get; set; }
        public string Port { get; set; }
        public string access_key { get; set; }
        public string secret_key { get; set; }
    }
}
