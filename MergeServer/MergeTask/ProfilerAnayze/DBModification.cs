using MongoDB.Driver;
using System.Collections.Generic;

namespace UAuto
{
    public class DBModification
    {
        public static void inserttest()
        {
            
        }

        public static void test()
        {
            string mainth = "Main Thread";
            Log.Print.Info(mainth.GetHashCode());
        }
    }
}
