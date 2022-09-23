using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public class FunRowTop<T>
    {
        public string case_uuid { get; set; }
        public string topName { get; set; }
        public List<T> topfuns { get; set; }
    }
}
