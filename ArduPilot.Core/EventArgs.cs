using System;
using System.Collections.Generic;

namespace ArduPilot.Core
{
    // 连接状态事件参数
    public class ConnectionEventArgs : EventArgs
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public bool IsSuccessful { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }

    // 参数接收事件参数
    public class ParameterReceivedEventArgs : EventArgs
    {
        public ParameterInfo Parameter { get; set; }
        public int CurrentCount { get; set; }
        public int TotalCount { get; set; }
        public double ProgressPercentage { get; set; }
    }

    // 状态更新事件参数
    public class StatusEventArgs : EventArgs
    {
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public StatusType Type { get; set; }
    }

    // 心跳事件参数
    public class HeartbeatEventArgs : EventArgs
    {
        public int Count { get; set; }
        public bool IsActive { get; set; }
    }

    // 参数统计事件参数
    public class ParameterStatisticsEventArgs : EventArgs
    {
        public int TotalCount { get; set; }
        public int GroupCount { get; set; }
        public Dictionary<MAV_PARAM_TYPE, int> TypeDistribution { get; set; }
        public List<string> UnknownParameters { get; set; }
        public Dictionary<string, List<ParameterInfo>> GroupedParameters { get; set; }
    }

    // 错误事件参数
    public class ErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public ErrorLevel Level { get; set; }
        public string Source { get; set; }
    }

    // 枚举定义
    public enum StatusType
    {
        Info,
        Debug,
        Warning,
        Error,
        Progress,
        Heartbeat,
        Connection,
        Protocol
    }

    public enum ErrorLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }
}