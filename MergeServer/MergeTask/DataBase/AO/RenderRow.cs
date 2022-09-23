using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class CaseRenderRow
    {
        //案例UUID
        public string case_uuid;

        //函数哈希值
        public int fun_id { get; set; }

        //帧数
        public int framecount { get; set; }

        //有效帧
        public int validframecount { get; set; }

        //*100
        public double timems { get; set; }

        //*100
        public double selfms { get; set; }

        public List<CaseRenderInfo> frames { get; set; }
    }
}
