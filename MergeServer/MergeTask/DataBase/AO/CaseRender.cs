using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class CaseRender
    {
        //函数hash为主键
        public int _id;
        public List<CaseRenderInfo> frames { get; set; }
    }
}
