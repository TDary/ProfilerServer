using System;
using System.Collections.Generic;
using System.Xml.Serialization;


namespace UAuto
{

    [Serializable]
    public class ServerUrl
    {
        [XmlAttribute("WorkerName")]
        public string WorkerName { get; set; }

        [XmlAttribute("AnayzeServerIP")]
        public string AnayzeServerIP { get; set; }
    }

    [Serializable]
    public class Database
    {
        [XmlAttribute("DatabaseIP")]
        public string DatabaseIP { get; set; }

        [XmlAttribute("DatabasePort")]
        public string DatabasePort { get; set; }

        [XmlAttribute("DatabaseName")]
        public string DatabaseName { get; set; }

    }

    [Serializable]
    public class ReportUrl
    {
        [XmlAttribute("NotifyUrl")]
        public string NotifyUrl { get; set; }
    }

    [Serializable]
    public class UploadDir
    {
        [XmlAttribute("Dir")]
        public string Dir { get; set; }

        [XmlAttribute("AutoBoot")]
        public string AutoBoot { get; set; }

        [XmlAttribute("UploadMode")]
        public string UploadMode { get; set; }

        [XmlAttribute("AutoClean")]
        public string AutoClean { get; set; }

        [XmlAttribute("AutoDelete")]
        public string DeleteCase { get; set; }

        [XmlAttribute("CheckDisk")]
        public string CheckDisk { get; set; }

        [XmlAttribute("CheckDiskSize")]
        public string CheckDiskSize { get; set; }

    }

    [Serializable]
    public class ProfilerAnalyze
    {
        [XmlAttribute("Max")]
        public int Max { get; set; }
    }

    [Serializable]
    public class RobotMonitor
    {
        [XmlAttribute("FeishuRobot")]
        public string FeishuRobot { get; set; }
    }

    [Serializable]
    public class ServerConfig
    {
        [XmlElement("ServerUrl")]
        public ServerUrl serverUrl { get; set; }

        [XmlElement("Database")]
        public Database database { get; set; }

        [XmlElement("ReportUrl")]
        public ReportUrl reportUrl { get; set; }

        [XmlElement("UploadDir")]
        public UploadDir uploadDir { get; set; }

        [XmlElement("ProfilerAnalyze")]
        public ProfilerAnalyze profilerAnalyze { get; set; }

        [XmlElement("RobotMonitor")]
        public RobotMonitor RobotMonitor { get; set; }
    }

}
