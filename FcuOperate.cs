using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace AutoPilot.Parameters
{
    #region 事件参数定义

    /// <summary>
    /// 状态改变事件参数
    /// </summary>
    public class StatusEventArgs : EventArgs
    {
        public string Message { get; set; }
        public StatusType Type { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public enum StatusType
    {
        Info,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// 进度改变事件参数
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
        public string Message { get; set; }
    }

    /// <summary>
    /// 参数接收事件参数
    /// </summary>
    public class ParameterReceivedEventArgs : EventArgs
    {
        public ParameterInfo Parameter { get; set; }
        public int ReceivedCount { get; set; }
        public int ExpectedTotal { get; set; }
    }

    /// <summary>
    /// 连接状态改变事件参数
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string PortName { get; set; }
        public int BaudRate { get; set; }
    }

    /// <summary>
    /// 参数读取完成事件参数
    /// </summary>
    public class ParametersCompleteEventArgs : EventArgs
    {
        public List<ParameterInfo> Parameters { get; set; }
        public Dictionary<string, List<ParameterInfo>> GroupedParameters { get; set; }
        public int TotalCount { get; set; }
        public TimeSpan ElapsedTime { get; set; }
    }

    #endregion

    #region 枚举定义

    public enum MAVLinkMessageId
    {
        HEARTBEAT = 0,
        PARAM_REQUEST_LIST = 21,
        PARAM_VALUE = 22,
        PARAM_SET = 23,
        COMMAND_LONG = 76,
        COMMAND_ACK = 77,
        PARAM_EXT_REQUEST_LIST = 321,
        PARAM_EXT_VALUE = 322,
        PARAM_EXT_SET = 323
    }

    public enum MAV_PARAM_TYPE
    {
        UINT8 = 1,
        INT8 = 2,
        UINT16 = 3,
        INT16 = 4,
        UINT32 = 5,
        INT32 = 6,
        UINT64 = 7,
        INT64 = 8,
        REAL32 = 9,
        REAL64 = 10
    }

    #endregion

    #region 参数信息类

    /// <summary>
    /// 参数信息类 - 支持属性通知
    /// </summary>
    public partial class ParameterInfo : INotifyPropertyChanged
    {
        private float _value;
        private bool _hasChanged;

        public string Name { get; set; }

        public float Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > 0.0001f)
                {
                    _value = value;
                    HasChanged = true;
                    OnPropertyChanged();
                }
            }
        }

        public MAV_PARAM_TYPE Type { get; set; }
        public ushort Index { get; set; }
        public string Description { get; set; }
        public float? MinValue { get; set; }
        public float? MaxValue { get; set; }
        public float? DefaultValue { get; set; }
        public string Units { get; set; }
        public string Group { get; set; }

        public bool HasChanged
        {
            get => _hasChanged;
            set
            {
                _hasChanged = value;
                OnPropertyChanged();
            }
        }

        public string GetTypeString()
        {
            switch (Type)
            {
                case MAV_PARAM_TYPE.UINT8: return "UINT8";
                case MAV_PARAM_TYPE.INT8: return "INT8";
                case MAV_PARAM_TYPE.UINT16: return "UINT16";
                case MAV_PARAM_TYPE.INT16: return "INT16";
                case MAV_PARAM_TYPE.UINT32: return "UINT32";
                case MAV_PARAM_TYPE.INT32: return "INT32";
                case MAV_PARAM_TYPE.REAL32: return "FLOAT";
                default: return "UNKNOWN";
            }
        }

        public string GetRangeString()
        {
            if (MinValue.HasValue && MaxValue.HasValue)
            {
                string range = $"[{MinValue:F2} ~ {MaxValue:F2}]";
                if (!string.IsNullOrEmpty(Units))
                    range += $" {Units}";
                return range;
            }

            switch (Type)
            {
                case MAV_PARAM_TYPE.UINT8: return "[0 ~ 255]";
                case MAV_PARAM_TYPE.INT8: return "[-128 ~ 127]";
                case MAV_PARAM_TYPE.UINT16: return "[0 ~ 65535]";
                case MAV_PARAM_TYPE.INT16: return "[-32768 ~ 32767]";
                case MAV_PARAM_TYPE.INT32: return "[INT32]";
                case MAV_PARAM_TYPE.REAL32: return "[FLOAT]";
                default: return "N/A";
            }
        }

        public string FormatValue()
        {
            switch (Type)
            {
                case MAV_PARAM_TYPE.UINT8:
                case MAV_PARAM_TYPE.INT8:
                case MAV_PARAM_TYPE.UINT16:
                case MAV_PARAM_TYPE.INT16:
                case MAV_PARAM_TYPE.UINT32:
                case MAV_PARAM_TYPE.INT32:
                    return $"{(int)Value}";
                case MAV_PARAM_TYPE.REAL32:
                    if (Math.Abs(Value) < 0.01 && Value != 0)
                        return $"{Value:E2}";
                    else if (Math.Abs(Value) >= 1000)
                        return $"{Value:F0}";
                    else
                        return $"{Value:F3}";
                default:
                    return $"{Value}";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion

    #region CRC和参数描述

    public static class CRCExtra
    {
        public static Dictionary<uint, byte> Values = new Dictionary<uint, byte>
        {
            { 0, 50 },    // HEARTBEAT
            { 21, 159 },  // PARAM_REQUEST_LIST
            { 22, 220 },  // PARAM_VALUE
            { 23, 168 },  // PARAM_SET
            { 76, 152 },  // COMMAND_LONG
            { 77, 143 },  // COMMAND_ACK
            { 321, 88 },  // PARAM_EXT_REQUEST_LIST
            { 322, 243 }, // PARAM_EXT_VALUE
            { 323, 78 },  // PARAM_EXT_SET
        };
    }

    public partial class ParameterInfo
    {
        public Dictionary<int, string> EnumValues { get; set; } = new Dictionary<int, string>();
        public Dictionary<int, string> BitmaskValues { get; set; } = new Dictionary<int, string>();
        public bool HasRebootRequired { get; set; }

        /// <summary>
        /// 获取格式化的值（包含枚举名称）
        /// </summary>
        public string GetFormattedValue()
        {
            int intValue = (int)Value;

            // 如果有枚举值定义
            if (EnumValues != null && EnumValues.ContainsKey(intValue))
            {
                return $"{intValue} ({EnumValues[intValue]})";
            }

            // 如果是位掩码
            if (BitmaskValues != null && BitmaskValues.Count > 0)
            {
                var setBits = new List<string>();
                for (int bit = 0; bit < 32; bit++)
                {
                    if ((intValue & (1 << bit)) != 0)
                    {
                        if (BitmaskValues.ContainsKey(bit))
                        {
                            setBits.Add(BitmaskValues[bit]);
                        }
                    }
                }
                if (setBits.Count > 0)
                {
                    return $"{intValue} ({string.Join(", ", setBits)})";
                }
            }

            return FormatValue();
        }
    }
    #endregion



    #region 公共主服务类
    public class ParameterService : IDisposable
    {
        #region 私有字段
        private SerialPort _serialPort;
        private byte _sequence = 0;
        protected Dictionary<string, ParameterInfo> _parameters;
        private int _expectedParamCount = -1;
        private HashSet<ushort> _receivedParams;
        private CancellationTokenSource _heartbeatCts;
        private Task _heartbeatTask;
        private DateTime _startTime;
        private readonly object _lockObject = new object();

        // 接收缓冲区
        private byte[] _receiveBuffer = new byte[4096];
        private int _bufferOffset = 0;

        // MAVLink ID
        private const byte GCS_SYSTEM_ID = 255;
        private const byte GCS_COMPONENT_ID = 190;
        private const byte TARGET_SYSTEM_ID = 1;
        private const byte TARGET_COMPONENT_ID = 1;

        #endregion

        #region 公共属性

        public bool IsConnected => _serialPort?.IsOpen ?? false;
        public List<ParameterInfo> Parameters => _parameters?.Values.ToList() ?? new List<ParameterInfo>();
        public int ParameterCount => _parameters?.Count ?? 0;

        #endregion

        #region 事件

        public event EventHandler<StatusEventArgs> StatusChanged;
        public event EventHandler<ProgressEventArgs> ProgressChanged;
        public event EventHandler<ConnectionEventArgs> ConnectionChanged;
        public event EventHandler<ParameterReceivedEventArgs> ParameterReceived;
        public event EventHandler<ParametersCompleteEventArgs> ParametersComplete;

        #endregion

        #region 构造函数

        public ParameterService()
        {
            _parameters = new Dictionary<string, ParameterInfo>();
            _receivedParams = new HashSet<ushort>();
        }

        #endregion

        #region 公共方法
        /// <summary>
        /// 获取可用串口列表
        /// </summary>
        public static List<string> GetAvailablePorts()
        {
            return SerialPort.GetPortNames().OrderBy(p => p).ToList();
        }

        /// <summary>
        /// 加载参数元数据（虚方法，由派生类实现）
        /// </summary>
        /// <param name="source">元数据源（可以是文件路径、URL或预定义的源类型）</param>
        /// <returns>是否成功加载元数据</returns>
        public virtual async Task<bool> LoadMetadataAsync(string source = "default")
        {
            // 基类提供默认实现：仅显示状态信息
            RaiseStatus("基类不支持元数据加载，请使用具体的飞控服务类", StatusType.Warning);
            return await Task.FromResult(false);
        }

        protected virtual void ApplyMetadataToParameter(ParameterInfo param)
        {
            // 基类默认实现为空，由派生类覆盖
        }

        /// <summary>
        /// 连接到飞控
        /// </summary>
        public async Task<bool> ConnectAsync(string portName, int baudRate = 115200)
        {
            try
            {
                if (IsConnected)
                {
                    await DisconnectAsync();
                }

                // 添加超时和异常处理
                return await Task.Run(() =>
                {
                    try
                    {
                        // 首先检查端口是否存在
                        var availablePorts = SerialPort.GetPortNames();
                        if (!availablePorts.Contains(portName))
                        {
                            RaiseStatus($"端口 {portName} 不存在", StatusType.Error);
                            return false;
                        }

                        _serialPort = new SerialPort(portName)
                        {
                            BaudRate = baudRate,
                            DataBits = 8,
                            StopBits = StopBits.One,
                            Parity = Parity.None,
                            ReadTimeout = 1000,
                            WriteTimeout = 1000,
                            DtrEnable = true,
                            RtsEnable = true
                        };

                        // 添加打开超时
                        var openTask = Task.Run(() => _serialPort.Open());
                        if (!openTask.Wait(5000)) // 5秒超时
                        {
                            RaiseStatus("串口打开超时", StatusType.Error);
                            return false;
                        }

                        _serialPort.DataReceived += OnSerialPortDataReceived;
                        StartHeartbeat();

                        RaiseConnectionChanged(true, portName, baudRate);
                        RaiseStatus($"已连接到 {portName}", StatusType.Success);
                        return true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        RaiseStatus($"端口 {portName} 被其他程序占用", StatusType.Error);
                        return false;
                    }
                    catch (ArgumentException)
                    {
                        RaiseStatus($"无效的端口名称: {portName}", StatusType.Error);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        RaiseStatus($"连接失败: {ex.Message}", StatusType.Error);
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                RaiseStatus($"连接异常: {ex.Message}", StatusType.Error);
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                StopHeartbeat();

                await Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        if (_serialPort != null)
                        {
                            if (_serialPort.IsOpen)
                            {
                                _serialPort.DataReceived -= OnSerialPortDataReceived;
                                _serialPort.Close();
                            }
                            _serialPort.Dispose();
                            _serialPort = null;
                        }
                    }
                });

                RaiseConnectionChanged(false, "", 0);
                RaiseStatus("已断开连接", StatusType.Info);
            }
            catch (Exception ex)
            {
                RaiseStatus($"断开连接失败: {ex.Message}", StatusType.Error);
            }
        }

        /// <summary>
        /// 读取参数
        /// </summary>
        public async Task<bool> ReadParametersAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                RaiseStatus("未连接到飞控", StatusType.Warning);
                return false;
            }

            try
            {
                _startTime = DateTime.Now;
                _parameters.Clear();
                _receivedParams.Clear();
                _expectedParamCount = -1;

                RaiseStatus("正在读取参数...", StatusType.Info);

                // 等待稳定
                await Task.Delay(2000, cancellationToken);

                // 请求参数列表
                RequestParameterList();
                RaiseStatus("已发送参数列表请求", StatusType.Info);

                // 等待接收
                int timeout = 60000; // 60秒
                int elapsed = 0;
                int lastCount = 0;
                int stableCounter = 0;
                int retryCount = 0;

                while (elapsed < timeout)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        RaiseStatus("参数读取已取消", StatusType.Warning);
                        return false;
                    }

                    await Task.Delay(100, cancellationToken);
                    elapsed += 100;

                    // 检查完成
                    if (_expectedParamCount > 0 && _receivedParams.Count >= _expectedParamCount)
                    {
                        var elapsedTime = DateTime.Now - _startTime;
                        RaiseParametersComplete(elapsedTime);
                        RaiseStatus($"成功接收所有 {_expectedParamCount} 个参数", StatusType.Success);
                        return true;
                    }

                    // 更新进度
                    if (_expectedParamCount > 0)
                    {
                        RaiseProgress(_receivedParams.Count, _expectedParamCount,
                            $"已接收 {_receivedParams.Count}/{_expectedParamCount} 个参数");
                    }

                    // 检查停滞
                    if (elapsed % 1000 == 0)
                    {
                        if (_receivedParams.Count == lastCount)
                        {
                            stableCounter++;
                            if (stableCounter > 5 && retryCount < 3)
                            {
                                RaiseStatus("参数接收停滞，重新请求...", StatusType.Warning);
                                RequestParameterList();
                                retryCount++;
                                stableCounter = 0;
                            }
                        }
                        else
                        {
                            stableCounter = 0;
                            lastCount = _receivedParams.Count;
                        }
                    }
                }

                if (_parameters.Count > 0)
                {
                    var elapsedTime = DateTime.Now - _startTime;
                    RaiseParametersComplete(elapsedTime);
                    RaiseStatus($"参数读取超时，已接收 {_parameters.Count} 个参数", StatusType.Warning);
                    return true;
                }
                else
                {
                    RaiseStatus("未接收到任何参数", StatusType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                RaiseStatus($"读取参数失败: {ex.Message}", StatusType.Error);
                return false;
            }
        }

        /// <summary>
        /// 写入单个参数
        /// </summary>
        public bool WriteParameter(ParameterInfo parameter)
        {
            if (!IsConnected) return false;

            try
            {
                SendParameterSet(parameter);
                parameter.HasChanged = false;
                RaiseStatus($"已写入参数 {parameter.Name} = {parameter.Value}", StatusType.Success);
                return true;
            }
            catch (Exception ex)
            {
                RaiseStatus($"写入参数失败: {ex.Message}", StatusType.Error);
                return false;
            }
        }

        /// <summary>
        /// 批量写入参数
        /// </summary>
        public async Task<bool> WriteParametersAsync(IEnumerable<ParameterInfo> parameters)
        {
            bool allSuccess = true;

            foreach (var param in parameters)
            {
                if (!WriteParameter(param))
                {
                    allSuccess = false;
                }
                await Task.Delay(50); // 避免发送过快
            }

            return allSuccess;
        }

        /// <summary>
        /// 保存参数到文件
        /// </summary>
        public bool SaveToFile(string filename)
        {
            try
            {
                using (var writer = new StreamWriter(filename, false, Encoding.UTF8))
                {
                    writer.WriteLine($"# ArduPilot参数");
                    writer.WriteLine($"# 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"# 参数总数: {_parameters.Count}");
                    writer.WriteLine($"# 格式: 参数名,值,ID,类型");
                    writer.WriteLine();

                    var groups = GetGroupedParameters();
                    foreach (var group in groups.OrderBy(g => g.Key))
                    {
                        writer.WriteLine($"# === {group.Key} ===");
                        foreach (var param in group.Value.OrderBy(p => p.Name))
                        {
                            writer.WriteLine($"{param.Name},{param.Value:F6},{param.Index},{param.GetTypeString()}");
                        }
                        writer.WriteLine();
                    }
                }

                RaiseStatus($"参数已保存到 {filename}", StatusType.Success);
                return true;
            }
            catch (Exception ex)
            {
                RaiseStatus($"保存参数失败: {ex.Message}", StatusType.Error);
                return false;
            }
        }

        /// <summary>
        /// 从文件加载参数
        /// </summary>
        public bool LoadFromFile(string filename)
        {
            try
            {
                var lines = File.ReadAllLines(filename);
                int count = 0;

                foreach (var line in lines)
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 2)
                    {
                        string name = parts[0].Trim();
                        if (float.TryParse(parts[1], out float value))
                        {
                            if (_parameters.ContainsKey(name))
                            {
                                _parameters[name].Value = value;
                                count++;
                            }
                        }
                    }
                }

                RaiseStatus($"从文件加载了 {count} 个参数", StatusType.Success);
                return true;
            }
            catch (Exception ex)
            {
                RaiseStatus($"加载参数失败: {ex.Message}", StatusType.Error);
                return false;
            }
        }

        /// <summary>
        /// 获取分组参数
        /// </summary>
        public Dictionary<string, List<ParameterInfo>> GetGroupedParameters()
        {
            var grouped = new Dictionary<string, List<ParameterInfo>>();

            foreach (var param in _parameters.Values)
            {
                string group = GetParameterGroup(param.Name);
                if (!grouped.ContainsKey(group))
                {
                    grouped[group] = new List<ParameterInfo>();
                }
                grouped[group].Add(param);
            }

            return grouped;
        }

        #endregion

        #region 私有方法

        private void StartHeartbeat()
        {
            _heartbeatCts = new CancellationTokenSource();
            _heartbeatTask = Task.Run(async () =>
            {
                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        SendHeartbeat();
                        await Task.Delay(1000, _heartbeatCts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch { }
                }
            }, _heartbeatCts.Token);
        }

        private void StopHeartbeat()
        {
            try
            {
                _heartbeatCts?.Cancel();
                _heartbeatTask?.Wait(2000);
            }
            catch { }
            finally
            {
                _heartbeatCts?.Dispose();
                _heartbeatCts = null;
                _heartbeatTask = null;
            }
        }

        private void OnSerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort == null || !_serialPort.IsOpen)
                        return;

                    int bytesToRead = _serialPort.BytesToRead;
                    if (bytesToRead <= 0) return;

                    byte[] tempBuffer = new byte[bytesToRead];
                    int bytesRead = _serialPort.Read(tempBuffer, 0, bytesToRead);

                    // 将新数据添加到缓冲区
                    if (_bufferOffset + bytesRead > _receiveBuffer.Length)
                    {
                        // 缓冲区溢出，重置
                        _bufferOffset = 0;
                    }

                    Array.Copy(tempBuffer, 0, _receiveBuffer, _bufferOffset, bytesRead);
                    _bufferOffset += bytesRead;

                    // 解析缓冲区中的消息
                    int processed = ParseMAVLinkMessages(_receiveBuffer, _bufferOffset);

                    // 移除已处理的数据
                    if (processed > 0 && processed < _bufferOffset)
                    {
                        Array.Copy(_receiveBuffer, processed, _receiveBuffer, 0, _bufferOffset - processed);
                        _bufferOffset -= processed;
                    }
                    else if (processed >= _bufferOffset)
                    {
                        _bufferOffset = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseStatus($"数据接收错误: {ex.Message}", StatusType.Error);
            }
        }

        private int ParseMAVLinkMessages(byte[] buffer, int length)
        {
            int lastProcessedIndex = 0;

            for (int i = 0; i < length; i++)
            {
                // MAVLink v2
                if (buffer[i] == 0xFD)
                {
                    if (i + 10 <= length) // 有足够的头部数据
                    {
                        byte payloadLength = buffer[i + 1];
                        byte incompatFlags = buffer[i + 2];
                        uint msgId = (uint)(buffer[i + 7] | (buffer[i + 8] << 8) | (buffer[i + 9] << 16));

                        bool isSigned = (incompatFlags & 0x01) != 0;
                        int signatureLength = isSigned ? 13 : 0;
                        int totalLength = 10 + payloadLength + 2 + signatureLength;

                        if (i + totalLength <= length) // 有完整消息
                        {
                            try
                            {
                                byte[] payload = new byte[payloadLength];
                                Array.Copy(buffer, i + 10, payload, 0, payloadLength);

                                // 验证CRC
                                byte[] messageForCrc = new byte[10 + payloadLength];
                                Array.Copy(buffer, i, messageForCrc, 0, 10 + payloadLength);

                                ushort receivedCrc = (ushort)(buffer[i + 10 + payloadLength] |
                                                             (buffer[i + 10 + payloadLength + 1] << 8));
                                ushort calculatedCrc = CalculateCRC16(messageForCrc, msgId);

                                if (receivedCrc == calculatedCrc)
                                {
                                    if (msgId == (uint)MAVLinkMessageId.PARAM_VALUE)
                                    {
                                        ParseParamValue(payload);
                                    }
                                    else if (msgId == (uint)MAVLinkMessageId.HEARTBEAT)
                                    {
                                        // 可以处理飞控心跳
                                    }
                                    else if (msgId == (uint)MAVLinkMessageId.COMMAND_ACK)
                                    {
                                        // COMMAND_ACK payload: uint16 command, uint8 result, uint8 progress, uint8 result_param2(保留), uint8 target_system, uint8 target_component
                                        // 这里只关心前两个
                                        if (payloadLength >= 3)
                                        {
                                            ushort ackCmd = (ushort)(payload[0] | (payload[1] << 8));
                                            byte result = payload[2];
                                            string resultText = result switch
                                            {
                                                0 => "ACCEPTED",
                                                1 => "TEMP_REJECT",
                                                2 => "DENIED",
                                                3 => "UNSUPPORTED",
                                                4 => "FAILED",
                                                5 => "IN_PROGRESS",
                                                6 => "CANCELLED",
                                                _ => $"UNKNOWN({result})"
                                            };
                                            RaiseStatus($"COMMAND_ACK cmd={ackCmd} result={resultText}",
                                                result == 0 ? StatusType.Success : (result == 5 ? StatusType.Info : StatusType.Warning));
                                        }
                                    }
                                }

                                lastProcessedIndex = i + totalLength;
                            }
                            catch
                            {
                                // 解析失败，跳过这个字节
                            }
                        }
                        else
                        {
                            // 消息不完整，等待更多数据
                            break;
                        }
                    }
                    else
                    {
                        // 头部不完整，等待更多数据
                        break;
                    }
                }
                // MAVLink v1
                else if (buffer[i] == 0xFE)
                {
                    if (i + 8 <= length) // 有足够的头部数据
                    {
                        byte payloadLength = buffer[i + 1];
                        byte msgId = buffer[i + 5];
                        int totalLength = 6 + payloadLength + 2;

                        if (i + totalLength <= length) // 有完整消息
                        {
                            try
                            {
                                byte[] payload = new byte[payloadLength];
                                Array.Copy(buffer, i + 6, payload, 0, payloadLength);

                                // 验证CRC (简化版，实际应该计算)
                                byte[] messageForCrc = new byte[6 + payloadLength];
                                Array.Copy(buffer, i, messageForCrc, 0, 6 + payloadLength);

                                ushort receivedCrc = (ushort)(buffer[i + 6 + payloadLength] |
                                                             (buffer[i + 6 + payloadLength + 1] << 8));
                                ushort calculatedCrc = CalculateCRC16(messageForCrc, msgId);

                                if (receivedCrc == calculatedCrc)
                                {
                                    if (msgId == (byte)MAVLinkMessageId.PARAM_VALUE)
                                    {
                                        ParseParamValue(payload);
                                    }
                                }

                                lastProcessedIndex = i + totalLength;
                            }
                            catch
                            {
                                // 解析失败，跳过这个字节
                            }
                        }
                        else
                        {
                            // 消息不完整，等待更多数据
                            break;
                        }
                    }
                    else
                    {
                        // 头部不完整，等待更多数据
                        break;
                    }
                }
                else
                {
                    // 不是有效的起始字节，继续查找
                    lastProcessedIndex = i + 1;
                }
            }

            return lastProcessedIndex;
        }

        private ushort CalculateCRC16(byte[] buffer, uint msgId)
        {
            ushort crc = 0xFFFF;

            // 跳过STX字节
            for (int i = 1; i < buffer.Length; i++)
            {
                crc = CRC16_Accumulate(buffer[i], crc);
            }

            // 添加CRC_EXTRA
            if (CRCExtra.Values.ContainsKey(msgId))
            {
                crc = CRC16_Accumulate(CRCExtra.Values[msgId], crc);
            }

            return crc;
        }

        private ushort CRC16_Accumulate(byte b, ushort crc)
        {
            byte ch = (byte)(b ^ (byte)(crc & 0x00FF));
            ch = (byte)(ch ^ (ch << 4));
            return (ushort)((crc >> 8) ^ (ch << 8) ^ (ch << 3) ^ (ch >> 4));
        }

        private void ParseParamValue(byte[] payload)
        {
            try
            {
                float paramValue = BitConverter.ToSingle(payload, 0);
                ushort paramCount = BitConverter.ToUInt16(payload, 4);
                ushort paramIndex = BitConverter.ToUInt16(payload, 6);

                byte[] paramIdBytes = new byte[16];
                Array.Copy(payload, 8, paramIdBytes, 0, 16);
                string paramId = Encoding.ASCII.GetString(paramIdBytes).TrimEnd('\0').Trim();

                byte paramType = payload[24];

                if (_expectedParamCount == -1 && paramCount > 0)
                {
                    _expectedParamCount = paramCount;
                    RaiseStatus($"期望接收 {paramCount} 个参数", StatusType.Info);
                }

                if (!string.IsNullOrEmpty(paramId))
                {
                    ParameterInfo param;

                    if (!_parameters.ContainsKey(paramId))
                    {
                        param = new ParameterInfo
                        {
                            Name = paramId,
                            Value = paramValue,
                            Type = (MAV_PARAM_TYPE)paramType,
                            Index = paramIndex,
                            Group = GetParameterGroup(paramId)
                        };

                        // 然后应用元数据（如果已加载）
                        ApplyMetadataToParameter(param);

                        _parameters[paramId] = param;
                    }
                    else
                    {
                        param = _parameters[paramId];
                        param.Value = paramValue;
                    }

                    _receivedParams.Add(paramIndex);

                    // 触发参数接收事件
                    RaiseParameterReceived(param, _receivedParams.Count, _expectedParamCount);
                }
            }
            catch { }
        }

        private void SendHeartbeat()
        {
            try
            {
                byte[] message = new byte[21];

                // MAVLink v2 header
                message[0] = 0xFD;
                message[1] = 9;    // payload length
                message[2] = 0;    // incompat flags
                message[3] = 0;    // compat flags
                message[4] = _sequence++;
                message[5] = GCS_SYSTEM_ID;
                message[6] = GCS_COMPONENT_ID;
                message[7] = 0;    // Message ID low (HEARTBEAT)
                message[8] = 0;    // Message ID middle
                message[9] = 0;    // Message ID high

                // HEARTBEAT Payload
                BitConverter.GetBytes((uint)0).CopyTo(message, 10);  // custom_mode
                message[14] = 6;   // type (MAV_TYPE_GCS)
                message[15] = 0;   // autopilot (MAV_AUTOPILOT_INVALID)
                message[16] = 0;   // base_mode
                message[17] = 3;   // system_status (MAV_STATE_STANDBY)
                message[18] = 3;   // mavlink_version

                // Calculate CRC
                byte[] crcData = new byte[19];
                Array.Copy(message, 0, crcData, 0, 19);
                ushort crc = CalculateCRC16(crcData, 0);

                message[19] = (byte)(crc & 0xFF);
                message[20] = (byte)(crc >> 8);

                lock (_lockObject)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Write(message, 0, 21);
                    }
                }
            }
            catch { }
        }

        private void RequestParameterList()
        {
            try
            {
                byte[] message = new byte[14];

                // MAVLink v2 header
                message[0] = 0xFD;
                message[1] = 2;    // payload length
                message[2] = 0;    // incompat flags
                message[3] = 0;    // compat flags
                message[4] = _sequence++;
                message[5] = GCS_SYSTEM_ID;
                message[6] = GCS_COMPONENT_ID;
                message[7] = 21;   // Message ID low (PARAM_REQUEST_LIST)
                message[8] = 0;    // Message ID middle
                message[9] = 0;    // Message ID high

                // PARAM_REQUEST_LIST Payload
                message[10] = TARGET_SYSTEM_ID;
                message[11] = TARGET_COMPONENT_ID;

                // Calculate CRC
                byte[] crcData = new byte[12];
                Array.Copy(message, 0, crcData, 0, 12);
                ushort crc = CalculateCRC16(crcData, 21);

                message[12] = (byte)(crc & 0xFF);
                message[13] = (byte)(crc >> 8);

                lock (_lockObject)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Write(message, 0, 14);
                    }
                }
            }
            catch { }
        }

        private void SendParameterSet(ParameterInfo parameter)
        {
            try
            {
                byte[] message = new byte[33];

                // MAVLink v2 header
                message[0] = 0xFD;
                message[1] = 25;
                message[2] = 0;
                message[3] = 0;
                message[4] = _sequence++;
                message[5] = GCS_SYSTEM_ID;
                message[6] = GCS_COMPONENT_ID;
                message[7] = 23; // PARAM_SET
                message[8] = 0;
                message[9] = 0;

                // Payload
                BitConverter.GetBytes(parameter.Value).CopyTo(message, 10);
                message[14] = TARGET_SYSTEM_ID;
                message[15] = TARGET_COMPONENT_ID;

                byte[] nameBytes = Encoding.ASCII.GetBytes(parameter.Name);
                Array.Copy(nameBytes, 0, message, 16, Math.Min(nameBytes.Length, 16));

                message[32] = (byte)parameter.Type;

                lock (_lockObject)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Write(message, 0, 33);
                    }
                }
            }
            catch { }
        }

        protected virtual string GetParameterGroup(string paramName)
        {
            return null;
        }

        #endregion

        #region 事件触发

        protected void RaiseStatus(string message, StatusType type)
        {
            StatusChanged?.Invoke(this, new StatusEventArgs { Message = message, Type = type });
        }

        protected void RaiseProgress(int current, int total, string message)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                Current = current,
                Total = total,
                Message = message
            });
        }

        protected void RaiseConnectionChanged(bool isConnected, string portName, int baudRate)
        {
            ConnectionChanged?.Invoke(this, new ConnectionEventArgs
            {
                IsConnected = isConnected,
                PortName = portName,
                BaudRate = baudRate
            });
        }

        protected void RaiseParameterReceived(ParameterInfo parameter, int receivedCount, int expectedTotal)
        {
            ParameterReceived?.Invoke(this, new ParameterReceivedEventArgs
            {
                Parameter = parameter,
                ReceivedCount = receivedCount,
                ExpectedTotal = expectedTotal
            });
        }

        protected void RaiseParametersComplete(TimeSpan elapsedTime)
        {
            ParametersComplete?.Invoke(this, new ParametersCompleteEventArgs
            {
                Parameters = Parameters,
                GroupedParameters = GetGroupedParameters(),
                TotalCount = _parameters.Count,
                ElapsedTime = elapsedTime
            });
        }

        #endregion

        #region 电机操作
        public async Task<bool> SendCommandLongAsync(
        ushort command,
        float param1 = 0, float param2 = 0, float param3 = 0, float param4 = 0,
        float param5 = 0, float param6 = 0, float param7 = 0,
        byte targetSystem = TARGET_SYSTEM_ID,
        byte targetComponent = TARGET_COMPONENT_ID,
        byte confirmation = 0)
        {
            if (!IsConnected)
            {
                RaiseStatus("未连接飞控，无法发送 COMMAND_LONG", StatusType.Error);
                return false;
            }

            try
            {
                const byte payloadLen = 33; // 7*float(28) + command(2) + target_sys(1) + target_comp(1) + confirmation(1)
                                            // MAVLink v2: 10字节头 + payload + 2字节CRC
                byte[] packet = new byte[10 + payloadLen + 2];

                // 头部
                packet[0] = 0xFD;          // STX
                packet[1] = payloadLen;    // payload length
                packet[2] = 0;             // incompat flags
                packet[3] = 0;             // compat flags
                packet[4] = _sequence++;   // seq
                packet[5] = GCS_SYSTEM_ID; // sysid
                packet[6] = GCS_COMPONENT_ID; // compid
                packet[7] = 76;            // msgid low (COMMAND_LONG = 76)
                packet[8] = 0;             // msgid mid
                packet[9] = 0;             // msgid high

                // Payload 按 MAVLink 字段打包顺序（大类型优先）：float param1..7, uint16 command, uint8 target_system, uint8 target_component, uint8 confirmation
                int o = 10;
                void WFloat(float v) { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, packet, o, 4); o += 4; }
                WFloat(param1);
                WFloat(param2);
                WFloat(param3);
                WFloat(param4);
                WFloat(param5);
                WFloat(param6);
                WFloat(param7);

                // command
                var cmdBytes = BitConverter.GetBytes(command);
                packet[o++] = cmdBytes[0];
                packet[o++] = cmdBytes[1];

                // target system/component & confirmation
                packet[o++] = targetSystem;
                packet[o++] = targetComponent;
                packet[o++] = confirmation;

                // 计算 CRC（沿用现有 CalculateCRC16：需提供 header+payload）
                byte[] crcData = new byte[10 + payloadLen];
                Buffer.BlockCopy(packet, 0, crcData, 0, 10 + payloadLen);
                ushort crc = CalculateCRC16(crcData, 76);
                packet[o++] = (byte)(crc & 0xFF);
                packet[o++] = (byte)(crc >> 8);

                lock (_lockObject)
                {
                    _serialPort.Write(packet, 0, packet.Length);
                }

                RaiseStatus($"已发送 COMMAND_LONG ({command})", StatusType.Info);
                return true;
            }
            catch (Exception ex)
            {
                RaiseStatus($"发送 COMMAND_LONG 失败: {ex.Message}", StatusType.Error);
                return false;
            }
        }

        /// <summary>
        /// 电机测试（ArduPilot: MAV_CMD_DO_MOTOR_TEST = 209）
        /// param1: 电机号(1~4), param2: 测试类型(0=THROTTLE_PERCENT), param3: 百分比, param4: 时长秒
        /// </summary>
        public Task<bool> MotorTestAsync(byte motorIndex, int throttlePercent = 30, int durationSeconds = 5)
        {
            // 读取到的最小起转参数（若你已缓存，可改成外部传入）
            int minPercent = 15;
            try
            {
                // 如果已经读取过参数，可通过 _parameters 查 MOT_SPIN_MIN / MOT_SPIN_ARM
                var spinMin = _parameters.TryGetValue("MOT_SPIN_MIN", out var pMin) ? pMin.Value : -1;
                var spinArm = _parameters.TryGetValue("MOT_SPIN_ARM", out var pArm) ? pArm.Value : -1;
                float baseSpin = Math.Max(spinMin, spinArm);        // 典型 0.1~0.15 (比例)
                if (baseSpin > 0 && baseSpin < 1)
                {
                    minPercent = (int)Math.Clamp(Math.Ceiling(baseSpin * 100 + 5), 15, 60);
                }
            }
            catch { /* 忽略读取异常 */ }

            throttlePercent = Math.Max(throttlePercent, minPercent);
            throttlePercent = Math.Clamp(throttlePercent, 1, 100);

            // 直接用百分数(0~100)，不要除以100
            float throttleParam = throttlePercent;

            return SendCommandLongAsync(
                command: 209,
                param1: motorIndex,       // 电机号(1..N)
                param2: 0,                // 0=THROTTLE_PERCENT
                param3: throttleParam,    // 百分比 0~100
                param4: durationSeconds,
                targetSystem: 1,
                targetComponent: 1);
        }

        /// <summary>
        /// 解锁/加锁（MAV_CMD_COMPONENT_ARM_DISARM = 400）param1=1解锁,0加锁
        /// </summary>
        public Task<bool> ArmAsync(bool arm)
        {
            return SendCommandLongAsync(
                command: 400,
                param1: arm ? 1 : 0,
                targetSystem: TARGET_SYSTEM_ID,
                targetComponent: TARGET_COMPONENT_ID);
        }
        #endregion

        #region IDisposable

        public void Dispose()
        {
            DisconnectAsync().Wait(5000);
            _serialPort?.Dispose();
            _heartbeatCts?.Dispose();
        }

        #endregion 电机操作
    }
    #endregion



    #region ArduPilot主服务类
    /// <summary>
    /// ArduPilot参数服务 - 事件驱动架构
    /// </summary>
    public class ArduPilotParameterService : ParameterService
    {
        #region 私有字段
        private ArduPilotParameterMetadataLoader _metadataLoader;
        #endregion

        #region 构造函数

        public ArduPilotParameterService()
        {
            ;
        }

        #endregion

        #region 公共方法
        protected override void ApplyMetadataToParameter(ParameterInfo param)
        {
            if (_metadataLoader != null)
            {
                _metadataLoader.ApplyMetadata(param);
            }
        }

        /// <summary>
        /// 加载参数元数据（从在线或本地）
        /// </summary>
        public override async Task<bool> LoadMetadataAsync(string source = "online")
        {
            _metadataLoader = new ArduPilotParameterMetadataLoader();

            bool success = false;

            if (source.ToLower() == "online")
            {
                RaiseStatus($"正在从在线仓库加载元数据...", StatusType.Info);
                success = await _metadataLoader.LoadFromOnlineAsync();
            }
            else
            {
                RaiseStatus($"正在从文件加载元数据: {source}", StatusType.Info);
                success = _metadataLoader.LoadFromFile(source);
            }

            if (success)
            {                
                // 如果参数已经存在，应用到现有参数
                if (_parameters.Count > 0)
                {
                    _metadataLoader.ApplyMetadataToAll(_parameters.Values);
                }
                RaiseStatus("元数据加载成功", StatusType.Success);
            }
            else
            {
                RaiseStatus("元数据加载失败", StatusType.Warning);
            }

            return success;
        }

        public class ArduPilotParameterMetadataLoader
        {
            private Dictionary<string, ParameterMetadata> _metadata = new Dictionary<string, ParameterMetadata>();

            /// <summary>
            /// 参数元数据
            /// </summary>
            public class ParameterMetadata
            {
                public string Name { get; set; }
                public string DisplayName { get; set; }
                public string Description { get; set; }
                public string Documentation { get; set; }
                public string User { get; set; }  // Standard/Advanced
                public string Units { get; set; }
                public string Range { get; set; }
                public float? RangeMin { get; set; }
                public float? RangeMax { get; set; }
                public float? Increment { get; set; }
                public string RebootRequired { get; set; }
                public Dictionary<int, string> Values { get; set; } = new Dictionary<int, string>();
                public Dictionary<int, string> Bitmask { get; set; } = new Dictionary<int, string>();
                public string Calibration { get; set; }
                public string ReadOnly { get; set; }

                public string GetFormattedRange()
                {
                    if (RangeMin.HasValue && RangeMax.HasValue)
                    {
                        string range = $"{RangeMin:G} to {RangeMax:G}";
                        if (!string.IsNullOrEmpty(Units))
                            range += $" {Units}";
                        if (Increment.HasValue && Increment > 0)
                            range += $" (步进: {Increment:G})";
                        return range;
                    }
                    return Range ?? "";
                }
            }

            /// <summary>
            /// 从在线仓库加载元数据（Mission Planner的方式）
            /// </summary>
            public async Task<bool> LoadFromOnlineAsync(string url = $"https://autotest.ardupilot.org/Parameters/ArduCopter/apm.pdef.xml")
            {
                try
                {
                    // ArduPilot官方参数元数据URL格式
                    // https://autotest.ardupilot.org/Parameters/ArduCopter/apm.pdef.xml
                    // string url = $"https://autotest.ardupilot.org/Parameters/ArduCopter/apm.pdef.xml";

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                        string xmlContent = await client.GetStringAsync(url);
                        return ParseXml(xmlContent);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"从在线加载元数据失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 从本地XML文件加载元数据
            /// </summary>
            public bool LoadFromFile(string filename)
            {
                try
                {
                    if (!File.Exists(filename))
                    {
                        Console.WriteLine($"文件不存在: {filename}");
                        return false;
                    }

                    string xmlContent = File.ReadAllText(filename);
                    return ParseXml(xmlContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载XML文件失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 解析ArduPilot参数定义XML
            /// </summary>
            private bool ParseXml(string xmlContent)
            {
                try
                {
                    _metadata.Clear();
                    XDocument doc = XDocument.Parse(xmlContent);

                    // ArduPilot XML格式示例:
                    // <paramfile>
                    //   <vehicles>
                    //     <parameters name="ArduCopter">
                    //       <param name="ACCEL_Z_D" humanName="Accel (vertical) controller D gain" documentation="..." user="Advanced">
                    //         <field name="Range">0 0.02</field>
                    //         <field name="Increment">0.001</field>
                    //         <field name="Units">Hz</field>
                    //         <field name="UnitText">hertz</field>
                    //       </param>

                    var parameters = doc.Descendants("param");

                    foreach (var param in parameters)
                    {
                        var metadata = new ParameterMetadata
                        {
                            Name = param.Attribute("name")?.Value,
                            DisplayName = param.Attribute("humanName")?.Value,
                            Documentation = param.Attribute("documentation")?.Value,
                            User = param.Attribute("user")?.Value
                        };

                        if (string.IsNullOrEmpty(metadata.Name))
                            continue;

                        // 解析字段
                        foreach (var field in param.Elements("field"))
                        {
                            string fieldName = field.Attribute("name")?.Value;
                            string fieldValue = field.Value.Trim();

                            switch (fieldName)
                            {
                                case "Range":
                                    ParseRange(fieldValue, metadata);
                                    break;
                                case "Units":
                                case "UnitText":
                                    metadata.Units = fieldValue;
                                    break;
                                case "Increment":
                                    if (float.TryParse(fieldValue, out float inc))
                                        metadata.Increment = inc;
                                    break;
                                case "RebootRequired":
                                    metadata.RebootRequired = fieldValue;
                                    break;
                                case "Calibration":
                                    metadata.Calibration = fieldValue;
                                    break;
                                case "ReadOnly":
                                    metadata.ReadOnly = fieldValue;
                                    break;
                                case "Values":
                                    ParseValues(fieldValue, metadata);
                                    break;
                                case "Bitmask":
                                    ParseBitmask(fieldValue, metadata);
                                    break;
                            }
                        }

                        // 解析值选项（另一种格式）
                        var values = param.Elements("values");
                        foreach (var valueGroup in values)
                        {
                            foreach (var value in valueGroup.Elements("value"))
                            {
                                string code = value.Attribute("code")?.Value;
                                string name = value.Value;

                                if (!string.IsNullOrEmpty(code) && int.TryParse(code, out int intCode))
                                {
                                    metadata.Values[intCode] = name;
                                }
                            }
                        }

                        _metadata[metadata.Name] = metadata;
                    }

                    Console.WriteLine($"成功加载 {_metadata.Count} 个参数的元数据");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析XML失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 解析范围字符串（格式: "min max" 或 "min to max" 或 "min - max"）
            /// </summary>
            private void ParseRange(string rangeStr, ParameterMetadata metadata)
            {
                metadata.Range = rangeStr;

                if (string.IsNullOrWhiteSpace(rangeStr))
                    return;

                // 处理不同的范围格式
                rangeStr = rangeStr.Replace(" to ", " ").Replace(" - ", " ").Replace("-", " ");
                var parts = rangeStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2)
                {
                    if (float.TryParse(parts[0], out float min))
                        metadata.RangeMin = min;
                    if (float.TryParse(parts[1], out float max))
                        metadata.RangeMax = max;
                }
            }

            /// <summary>
            /// 解析值选项（枚举）
            /// </summary>
            private void ParseValues(string valuesStr, ParameterMetadata metadata)
            {
                // 格式: "0:Disabled,1:Enabled" 或 "0=Disabled 1=Enabled"
                var pairs = valuesStr.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in pairs)
                {
                    var parts = pair.Split(new[] { ':', '=' }, 2);
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0].Trim(), out int code))
                        {
                            metadata.Values[code] = parts[1].Trim();
                        }
                    }
                }
            }

            /// <summary>
            /// 解析位掩码
            /// </summary>
            private void ParseBitmask(string bitmaskStr, ParameterMetadata metadata)
            {
                // 格式: "0:First,1:Second,2:Third"
                var pairs = bitmaskStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in pairs)
                {
                    var parts = pair.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0].Trim(), out int bit))
                        {
                            metadata.Bitmask[bit] = parts[1].Trim();
                        }
                    }
                }
            }

            /// <summary>
            /// 获取参数元数据
            /// </summary>
            public ParameterMetadata GetMetadata(string paramName)
            {
                return _metadata.ContainsKey(paramName) ? _metadata[paramName] : null;
            }

            /// <summary>
            /// 应用元数据到参数信息
            /// </summary>
            public void ApplyMetadata(ParameterInfo param)
            {
                var metadata = GetMetadata(param.Name);
                if (metadata != null)
                {
                    // 应用详细描述
                    if (!string.IsNullOrEmpty(metadata.Documentation))
                        param.Description = metadata.Documentation;
                    else if (!string.IsNullOrEmpty(metadata.DisplayName))
                        param.Description = metadata.DisplayName;

                    // 应用精确的范围
                    if (metadata.RangeMin.HasValue)
                        param.MinValue = metadata.RangeMin;
                    if (metadata.RangeMax.HasValue)
                        param.MaxValue = metadata.RangeMax;

                    // 应用单位
                    if (!string.IsNullOrEmpty(metadata.Units))
                        param.Units = metadata.Units;

                    // 应用其他属性
                    if (!string.IsNullOrEmpty(metadata.RebootRequired))
                        param.HasRebootRequired = metadata.RebootRequired.ToLower() == "true";

                    // 如果有枚举值，添加到描述中
                    if (metadata.Values.Count > 0)
                    {
                        param.EnumValues = metadata.Values;

                        // 在描述中添加可选值
                        var valueList = string.Join(", ", metadata.Values.Select(v => $"{v.Key}={v.Value}"));
                        param.Description += $"\n可选值: {valueList}";
                    }

                    // 如果有位掩码，添加到描述中
                    if (metadata.Bitmask.Count > 0)
                    {
                        param.BitmaskValues = metadata.Bitmask;

                        var bitmaskList = string.Join(", ", metadata.Bitmask.Select(b => $"Bit{b.Key}:{b.Value}"));
                        param.Description += $"\n位定义: {bitmaskList}";
                    }
                }
            }

            /// <summary>
            /// 批量应用元数据
            /// </summary>
            public void ApplyMetadataToAll(IEnumerable<ParameterInfo> parameters)
            {
                foreach (var param in parameters)
                {
                    ApplyMetadata(param);
                }
            }
        }
        #endregion

        #region 保护方法
        protected override string GetParameterGroup(string paramName)
        {
            if (paramName.StartsWith("MOT_")) return "电机控制";
            if (paramName.StartsWith("ATC_")) return "姿态控制";
            if (paramName.StartsWith("PSC_")) return "位置控制";
            if (paramName.StartsWith("GPS_")) return "GPS配置";
            if (paramName.StartsWith("BATT")) return "电池管理";
            if (paramName.StartsWith("FS_")) return "故障保护";
            if (paramName.StartsWith("ARMING_")) return "解锁设置";
            if (paramName.StartsWith("INS_")) return "惯性传感器";
            if (paramName.StartsWith("COMPASS_")) return "罗盘设置";
            if (paramName.StartsWith("LOG_")) return "日志设置";
            if (paramName.StartsWith("FLTMODE")) return "飞行模式";
            if (paramName.StartsWith("RC")) return "遥控器";
            if (paramName.StartsWith("SERIAL")) return "串口设置";
            if (paramName.StartsWith("SYSID_")) return "系统ID";
            if (paramName.StartsWith("EK")) return "EKF滤波器";
            if (paramName.StartsWith("WPNAV_")) return "航点导航";
            if (paramName.StartsWith("RTL_")) return "返航设置";
            return "其他参数";
        }
        #endregion        
    }

    #endregion



    #region PX4 主服务类
    /// <summary>
    /// PX4参数服务（继承自ArduPilot服务并添加PX4特性）
    /// </summary>
    public class PX4ParameterService : ParameterService
    {
        private PX4ParameterMetadataLoader _px4MetadataLoader;

        /// <summary>
        /// 加载PX4参数元数据
        /// </summary>
        public override async Task<bool> LoadMetadataAsync(string source = "repository")
        {
            _px4MetadataLoader = new PX4ParameterMetadataLoader();

            bool success = false;

            switch (source.ToLower())
            {
                case "repository":
                case "repo":
                    RaiseStatus("正在从PX4官方仓库加载元数据...", StatusType.Info);
                    success = await _px4MetadataLoader.LoadFromPX4RepositoryAsync();
                    break;

                case "qgc":
                case "qgroundcontrol":
                    RaiseStatus("正在从QGroundControl API加载元数据...", StatusType.Info);
                    success = await _px4MetadataLoader.LoadFromQGCAsync();
                    break;

                default:
                    // 假设是文件路径
                    RaiseStatus($"正在从文件加载元数据: {source}", StatusType.Info);
                    success = _px4MetadataLoader.LoadFromFile(source);
                    break;
            }

            if (success)
            {
                // 应用到现有参数
                if (_parameters.Count > 0)
                {
                    _px4MetadataLoader.ApplyMetadataToAll(_parameters.Values);
                }
                RaiseStatus("PX4元数据加载成功", StatusType.Success);
            }
            else
            {
                RaiseStatus("PX4元数据加载失败", StatusType.Warning);
            }

            return success;
        }

        protected override void ApplyMetadataToParameter(ParameterInfo param)
        {
            if (_px4MetadataLoader != null)
            {
                _px4MetadataLoader.ApplyMetadata(param);
            }
        }

        protected override string GetParameterGroup(string paramName)
        {
            // PX4的参数分组规则
            if (paramName.StartsWith("MC_")) return "多旋翼控制";
            if (paramName.StartsWith("FW_")) return "固定翼控制";
            if (paramName.StartsWith("VTOL_")) return "垂直起降";
            if (paramName.StartsWith("EKF2_")) return "EKF2滤波器";
            if (paramName.StartsWith("IMU_")) return "IMU传感器";
            if (paramName.StartsWith("GPS_")) return "GPS设置";
            if (paramName.StartsWith("COM_")) return "通信设置";
            if (paramName.StartsWith("SYS_")) return "系统设置";
            if (paramName.StartsWith("CAL_")) return "传感器校准";
            if (paramName.StartsWith("BAT")) return "电池管理";
            if (paramName.StartsWith("RC_")) return "遥控器";
            if (paramName.StartsWith("NAV_")) return "导航设置";
            if (paramName.StartsWith("MIS_")) return "任务设置";
            if (paramName.StartsWith("GND_")) return "地面检测";
            if (paramName.StartsWith("MPC_")) return "多旋翼位置控制";
            if (paramName.StartsWith("FW_")) return "固定翼控制";
            if (paramName.StartsWith("SENS_")) return "传感器设置";
            if (paramName.StartsWith("SIM_")) return "仿真设置";
            if (paramName.StartsWith("TEL_")) return "数传设置";
            if (paramName.StartsWith("UAVCAN_")) return "UAVCAN设置";
            if (paramName.StartsWith("PWM_")) return "PWM输出";
            if (paramName.StartsWith("CBRK_")) return "安全开关";
            return "其他参数";
        }

        /// <summary>
        /// PX4参数元数据加载器
        /// </summary>
        public class PX4ParameterMetadataLoader
        {
            private Dictionary<string, PX4ParameterMetadata> _metadata = new Dictionary<string, PX4ParameterMetadata>();

            /// <summary>
            /// PX4参数元数据
            /// </summary>
            public class PX4ParameterMetadata
            {
                [JsonProperty("name")]
                public string Name { get; set; }

                [JsonProperty("type")]
                public string Type { get; set; }  // Float, Int32, Int16, Int8

                [JsonProperty("default")]
                public object DefaultValue { get; set; }

                [JsonProperty("group")]
                public string Group { get; set; }

                [JsonProperty("category")]
                public string Category { get; set; }

                [JsonProperty("shortDesc")]
                public string ShortDescription { get; set; }

                [JsonProperty("longDesc")]
                public string LongDescription { get; set; }

                [JsonProperty("unit")]
                public string Unit { get; set; }

                [JsonProperty("units")]
                public string Units { get; set; }  // 有些JSON用units而不是unit

                [JsonProperty("min")]
                public float? Min { get; set; }

                [JsonProperty("max")]
                public float? Max { get; set; }

                [JsonProperty("increment")]
                public float? Increment { get; set; }

                [JsonProperty("decimal")]
                public int? Decimal { get; set; }

                [JsonProperty("decimalPlaces")]
                public int? DecimalPlaces { get; set; }  // 有些版本用这个字段名

                [JsonProperty("rebootRequired")]
                public bool RebootRequired { get; set; }

                [JsonProperty("volatileParam")]
                public bool Volatile { get; set; }

                [JsonProperty("values")]
                public List<PX4ValueOption> Values { get; set; }  // 改为List

                [JsonProperty("bitmask")]
                public List<PX4BitmaskOption> Bitmask { get; set; }  // 改为List

                public string GetFormattedDescription()
                {
                    if (!string.IsNullOrEmpty(LongDescription))
                        return LongDescription;
                    if (!string.IsNullOrEmpty(ShortDescription))
                        return ShortDescription;
                    return "";
                }

                public string GetFormattedRange()
                {
                    if (Min.HasValue && Max.HasValue)
                    {
                        string range = $"[{Min:G} - {Max:G}]";
                        string unitStr = Units ?? Unit;  // 兼容两种字段名
                        if (!string.IsNullOrEmpty(unitStr))
                            range += $" {unitStr}";
                        return range;
                    }
                    return "";
                }

                public Dictionary<int, string> GetValuesAsDictionary()
                {
                    var dict = new Dictionary<int, string>();
                    if (Values != null)
                    {
                        foreach (var val in Values)
                        {
                            dict[val.Value] = val.Description;
                        }
                    }
                    return dict;
                }

                public Dictionary<int, string> GetBitmaskAsDictionary()
                {
                    var dict = new Dictionary<int, string>();
                    if (Bitmask != null)
                    {
                        foreach (var bit in Bitmask)
                        {
                            dict[bit.Index] = bit.Description;
                        }
                    }
                    return dict;
                }
            }

            /// <summary>
            /// PX4参数值选项
            /// </summary>
            public class PX4ValueOption
            {
                [JsonProperty("value")]
                public int Value { get; set; }

                [JsonProperty("description")]
                public string Description { get; set; }
            }

            /// <summary>
            /// PX4参数位掩码选项
            /// </summary>
            public class PX4BitmaskOption
            {
                [JsonProperty("index")]
                public int Index { get; set; }

                [JsonProperty("description")]
                public string Description { get; set; }
            }

            /// <summary>
            /// 从PX4官方仓库加载最新的参数元数据
            /// </summary>
            public async Task<bool> LoadFromPX4RepositoryAsync()
            {
                try
                {
                    string url = "https://raw.githubusercontent.com/PX4/PX4-Autopilot/main/Tools/px4params/parameters.json";

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                        string jsonContent = await client.GetStringAsync(url);
                        return ParsePX4Json(jsonContent);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"从PX4仓库加载元数据失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 从QGroundControl API加载元数据
            /// </summary>
            public async Task<bool> LoadFromQGCAsync(int majorVersion = 1, int minorVersion = 14)
            {
                try
                {
                    string url = $"https://api.qgroundcontrol.com/parameter/{majorVersion}/{minorVersion}/PX4/parameters.json";

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                        string jsonContent = await client.GetStringAsync(url);
                        return ParseQGCJson(jsonContent);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"从QGC API加载元数据失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 从本地JSON文件加载元数据
            /// </summary>
            public bool LoadFromFile(string filename)
            {
                try
                {
                    if (!File.Exists(filename))
                    {
                        Console.WriteLine($"文件不存在: {filename}");
                        return false;
                    }

                    string jsonContent = File.ReadAllText(filename);
                    return ParsePX4Json(jsonContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载JSON文件失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 解析PX4参数JSON格式
            /// </summary>
            private bool ParsePX4Json(string jsonContent)
            {
                try
                {
                    _metadata.Clear();

                    // PX4 JSON格式是包含parameters数组的对象
                    var json = JObject.Parse(jsonContent);

                    // 检查是否有parameters属性
                    if (json["parameters"] != null)
                    {
                        // 标准PX4格式：{ "parameters": [...], "version": 1 }
                        var parametersArray = json["parameters"] as JArray;
                        if (parametersArray != null)
                        {
                            foreach (var paramToken in parametersArray)
                            {
                                var param = paramToken.ToObject<PX4ParameterMetadata>();
                                if (param != null && !string.IsNullOrEmpty(param.Name))
                                {
                                    _metadata[param.Name] = param;
                                }
                            }
                        }
                    }
                    else if (json["items"] != null)
                    {
                        // 另一种格式（可能来自某些版本）
                        var items = json["items"]["parameters"]["list"]["items"] as JArray;
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var param = item.ToObject<PX4ParameterMetadata>();
                                if (param != null && !string.IsNullOrEmpty(param.Name))
                                {
                                    _metadata[param.Name] = param;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 尝试直接解析为参数字典
                        var parameters = json.ToObject<Dictionary<string, PX4ParameterMetadata>>();
                        if (parameters != null)
                        {
                            foreach (var kvp in parameters)
                            {
                                kvp.Value.Name = kvp.Key;
                                _metadata[kvp.Key] = kvp.Value;
                            }
                        }
                    }

                    Console.WriteLine($"成功加载 {_metadata.Count} 个PX4参数的元数据");
                    return _metadata.Count > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析PX4 JSON失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 解析QGC格式的JSON
            /// </summary>
            private bool ParseQGCJson(string jsonContent)
            {
                try
                {
                    _metadata.Clear();

                    // QGC JSON格式略有不同
                    var root = JObject.Parse(jsonContent);
                    var version = root["version"]?.ToString();
                    var parameters = root["parameters"] as JArray;

                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            var metadata = param.ToObject<PX4ParameterMetadata>();
                            if (!string.IsNullOrEmpty(metadata.Name))
                            {
                                _metadata[metadata.Name] = metadata;
                            }
                        }
                    }

                    Console.WriteLine($"成功加载 {_metadata.Count} 个PX4参数的元数据 (版本: {version})");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"解析QGC JSON失败: {ex.Message}");
                    return false;
                }
            }

            /// <summary>
            /// 获取参数元数据
            /// </summary>
            public PX4ParameterMetadata GetMetadata(string paramName)
            {
                return _metadata.ContainsKey(paramName) ? _metadata[paramName] : null;
            }

            /// <summary>
            /// 应用元数据到参数信息
            /// </summary>
            public void ApplyMetadata(ParameterInfo param)
            {
                var metadata = GetMetadata(param.Name);
                if (metadata != null)
                {
                    // 应用描述
                    param.Description = metadata.GetFormattedDescription();

                    // 应用范围
                    if (metadata.Min.HasValue)
                        param.MinValue = metadata.Min;
                    if (metadata.Max.HasValue)
                        param.MaxValue = metadata.Max;

                    // 应用单位（兼容两种字段名）
                    string unitStr = metadata.Units ?? metadata.Unit;
                    if (!string.IsNullOrEmpty(unitStr))
                        param.Units = unitStr;

                    // 应用默认值
                    if (metadata.DefaultValue != null)
                    {
                        if (float.TryParse(metadata.DefaultValue.ToString(), out float defaultVal))
                        {
                            param.DefaultValue = defaultVal;
                        }
                    }

                    // 应用分组
                    if (!string.IsNullOrEmpty(metadata.Group))
                        param.Group = metadata.Group;

                    // 应用枚举值
                    var valuesDict = metadata.GetValuesAsDictionary();
                    if (valuesDict.Count > 0)
                    {
                        param.EnumValues = valuesDict;
                    }

                    // 应用位掩码
                    var bitmaskDict = metadata.GetBitmaskAsDictionary();
                    if (bitmaskDict.Count > 0)
                    {
                        param.BitmaskValues = bitmaskDict;
                    }

                    param.HasRebootRequired = metadata.RebootRequired;
                }
            }

            /// <summary>
            /// 批量应用元数据
            /// </summary>
            public void ApplyMetadataToAll(IEnumerable<ParameterInfo> parameters)
            {
                foreach (var param in parameters)
                {
                    ApplyMetadata(param);
                }
            }

            /// <summary>
            /// 导出元数据到文件（用于离线使用）
            /// </summary>
            public void SaveToFile(string filename)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_metadata.Values, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(filename, json);
                    Console.WriteLine($"元数据已保存到: {filename}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"保存元数据失败: {ex.Message}");
                }
            }
        }
    }
    #endregion
    #region 控制台程序示例
    #endregion
}