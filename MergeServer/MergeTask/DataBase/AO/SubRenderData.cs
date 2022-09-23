using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class SubRenderData
    {
        public long name { get; set; }
        public double beginTime { get; set; }
        public double durTime { get; set; }
        public List<SubRenderData> children { get; set; }
    }
}
