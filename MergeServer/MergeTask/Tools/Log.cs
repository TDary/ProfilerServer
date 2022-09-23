

namespace UAuto
{
    sealed class Log
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public static NLog.Logger Print
        {
            get
            {
                return _logger;
            }
        }

        private Log() { }

    }
}
