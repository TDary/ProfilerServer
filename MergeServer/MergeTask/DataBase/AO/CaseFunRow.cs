using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class CaseFunRow
    {
        //案例UUID
        public string case_uuid;

        //函数哈希值
        public long fun_id { get; set; }

        //帧数
        public int framecount { get; set; }

        //有效帧
        public int validframecount { get; set; }

        //*100
        public long total { get; set; }

        //*100
        public long self { get; set; }

        public long calls { get; set; }

        //*100
        public long gcalloc { get; set; }

        //*100
        public long timems { get; set; }

        //*100
        public long selfms { get; set; }

        public List<CaseFunRowInfo> frames { get; set; }
    }
}
