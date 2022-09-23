using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UAuto
{
    public abstract class TaskBase
    {

        private string _id = null;

        public string ID
        {
            get
            {
                return _id;
            }
        }

        /// <summary>
        /// 任务在管理器列表的索引
        /// </summary>
        private int _taskIndex = -1;

        public int Index
        {
            set
            {
                _taskIndex = value;
            }

            get
            {
                return _taskIndex;
            }
        }

        public TaskBase(string id)
        {
            _id = id;
        }

        public abstract void Begin();

        public abstract void Update(float deltaTime);

        public abstract bool IsEnd();

        public abstract void Release();

    }

}
