using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class HighTime
    {
        public long fun_id { get; set; }
        public int high_selfms_average { get; set; }      //较高耗时帧均耗时
        public int high_selfms_framecounts { get; set; }   //高耗时帧总数
        public long selfms_validframecount_average { get; set; }    //函数有效帧均耗时
        public int validframecount { get; set; }         //函数有效帧数
    }
}
