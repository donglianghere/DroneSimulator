using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace ArduPilot.Core
{
    public class MAVLinkV2Parser : IDisposable
    {
        #region 私有字段
        private SerialPort serialPort;
        private byte sequence = 0;
        private Dictionary<string, ParameterInfo> parameters = new Dictionary<string, ParameterInfo>();
        private int expectedParamCount = -1;
        private HashSet<ushort> receivedParams = new HashSet<ushort>();
        private byte[] receiveBuffer = new byte[1024];
        private int bufferIndex = 0;
        private int heartbeatCount = 0;
        private bool isDisposed = false;

        // ArduPilot系统和组件ID
        private const byte GCS_SYSTEM_ID = 255;
        private const byte GCS_COMPONENT_ID = 190;
        private const byte TARGET_SYSTEM_ID = 1;
        private const byte TARGET_COMPONENT_ID = 1;
        #endregion

        #region 事件定义
        // 连接生命周期事件
        public event EventHandler<ConnectionEventArgs> ConnectionAttempting;
        public event EventHandler<ConnectionEventArgs> ConnectionEstablished;
        public event EventHandler<ConnectionEventArgs> ConnectionFailed;
        public event EventHandler<ConnectionEventArgs> ConnectionClosed;

        // 通信状态事件
        public event EventHandler<HeartbeatEventArgs> HeartbeatStarted;
        public event EventHandler<HeartbeatEventArgs> HeartbeatSent;
        public event EventHandler<HeartbeatEventArgs> HeartbeatStopped;
        public event EventHandler<StatusEventArgs> ParameterRequestSent;
        public event EventHandler<StatusEventArgs> RetryAttempted;

        // 数据接收事件
        public event EventHandler<StatusEventArgs> ExpectedParameterCountSet;
        public event EventHandler<ParameterReceivedEventArgs> ParameterReceived;
        public event EventHandler<ParameterReceivedEventArgs> ProgressUpdated;
        public event EventHandler<ParameterStatisticsEventArgs> ParametersLoadCompleted;

        // 状态和日志事件
        public event EventHandler<StatusEventArgs> StatusUpdated;
        public event EventHandler<StatusEventArgs> DebugMessageReceived;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        // 数据分析事件
        public event EventHandler<ParameterStatisticsEventArgs> ParametersGrouped;
        public event EventHandler<ParameterStatisticsEventArgs> StatisticsCalculated;
        #endregion

        #region 公共方法
        public async Task<Dictionary<string, ParameterInfo>> ConnectAndReadParametersAsync(
            string portName, 
            int baudRate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // 触发连接开始事件
                OnConnectionAttempting(new ConnectionEventArgs
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    Message = $"正在连接到 {portName} (波特率: {baudRate})..."
                });

                // 配置串口
                await ConfigureSerialPortAsync(portName, baudRate);

                // 建立连接
                await EstablishConnectionAsync(cancellationToken);

                // 读取参数
                await ReadParametersAsync(cancellationToken);

                // 分析和输出结果
                await AnalyzeParametersAsync();

                // 关闭连接
                await CloseConnectionAsync();

                return new Dictionary<string, ParameterInfo>(parameters);
            }
            catch (Exception ex)
            {
                OnConnectionFailed(new ConnectionEventArgs
                {
                    PortName = portName,
                    BaudRate = baudRate,
                    IsSuccessful = false,
                    Message = $"连接失败: {ex.Message}",
                    Exception = ex
                });
                throw;
            }
        }
        #endregion

        #region 私有实现方法
        private async Task ConfigureSerialPortAsync(string portName, int baudRate)
        {
            OnStatusUpdated(new StatusEventArgs
            {
                Message = "配置串口参数...",
                Type = StatusType.Info
            });

            serialPort = new SerialPort(portName)
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

            serialPort.DataReceived += SerialPort_DataReceived;
            serialPort.Open();

            OnConnectionEstablished(new ConnectionEventArgs
            {
                PortName = portName,
                BaudRate = baudRate,
                IsSuccessful = true,
                Message = "串口已打开"
            });
        }

        private async Task EstablishConnectionAsync(CancellationToken cancellationToken)
        {
            OnStatusUpdated(new StatusEventArgs
            {
                Message = "等待连接稳定...",
                Type = StatusType.Info
            });

            await Task.Delay(2000, cancellationToken);

            // 启动心跳任务
            OnHeartbeatStarted(new HeartbeatEventArgs { IsActive = true });
            
            var heartbeatTask = Task.Run(async () =>
            {
                while (serialPort?.IsOpen == true && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        SendHeartbeatV2();
                        heartbeatCount++;
                        
                        OnHeartbeatSent(new HeartbeatEventArgs 
                        { 
                            Count = heartbeatCount, 
                            IsActive = true 
                        });

                        await Task.Delay(1000, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred(new ErrorEventArgs
                        {
                            ErrorMessage = "心跳发送失败",
                            Exception = ex,
                            Level = ErrorLevel.Warning,
                            Source = "Heartbeat"
                        });
                    }
                }
            }, cancellationToken);

            await Task.Delay(2000, cancellationToken);
        }

        private async Task ReadParametersAsync(CancellationToken cancellationToken)
        {
            OnStatusUpdated(new StatusEventArgs
            {
                Message = "请求参数列表...",
                Type = StatusType.Info
            });

            RequestParameterListV2();

            OnParameterRequestSent(new StatusEventArgs
            {
                Message = "已发送参数列表请求",
                Type = StatusType.Protocol
            });

            // 参数接收循环
            await WaitForParametersAsync(cancellationToken);
        }

        private async Task WaitForParametersAsync(CancellationToken cancellationToken)
        {
            int retryCount = 0;
            const int maxRetries = 3;
            const int timeout = 60000; // 60秒超时
            int elapsed = 0;
            int lastReceivedCount = 0;
            int stableCounter = 0;

            while (elapsed < timeout && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
                elapsed += 100;

                // 检查是否收到所有参数
                if (expectedParamCount > 0 && receivedParams.Count >= expectedParamCount)
                {
                    OnStatusUpdated(new StatusEventArgs
                    {
                        Message = $"成功接收所有 {expectedParamCount} 个参数",
                        Type = StatusType.Info
                    });
                    break;
                }

                // 每秒输出进度
                if (elapsed % 1000 == 0)
                {
                    if (receivedParams.Count > 0)
                    {
                        double progressPercent = expectedParamCount > 0 
                            ? (double)receivedParams.Count / expectedParamCount * 100 
                            : 0;

                        OnProgressUpdated(new ParameterReceivedEventArgs
                        {
                            CurrentCount = receivedParams.Count,
                            TotalCount = expectedParamCount,
                            ProgressPercentage = progressPercent
                        });
                    }

                    // 检查是否停止接收新参数
                    if (receivedParams.Count == lastReceivedCount)
                    {
                        stableCounter++;
                        if (stableCounter > 5 && retryCount < maxRetries)
                        {
                            OnRetryAttempted(new StatusEventArgs
                            {
                                Message = $"参数接收停滞，第 {retryCount + 1} 次重试...",
                                Type = StatusType.Warning
                            });

                            RequestParameterListV2();
                            retryCount++;
                            stableCounter = 0;
                        }
                    }
                    else
                    {
                        stableCounter = 0;
                    }
                    lastReceivedCount = receivedParams.Count;
                }
            }

            if (parameters.Count == 0)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    ErrorMessage = "未接收到任何参数",
                    Level = ErrorLevel.Error,
                    Source = "ParameterReading"
                });
            }
        }

        private async Task AnalyzeParametersAsync()
        {
            if (parameters.Count == 0) return;

            await Task.Run(() =>
            {
                // 按组分类参数
                var groupedParams = new Dictionary<string, List<ParameterInfo>>();

                foreach (var param in parameters.Values)
                {
                    string group = GetParameterGroup(param.Name);
                    if (!groupedParams.ContainsKey(group))
                    {
                        groupedParams[group] = new List<ParameterInfo>();
                    }
                    groupedParams[group].Add(param);
                }

                // 统计各类型参数数量
                var typeStats = parameters.Values.GroupBy(p => p.Type)
                    .ToDictionary(g => g.Key, g => g.Count());

                // 找出未识别的参数
                var unknownParams = parameters.Values
                    .Where(p => string.IsNullOrEmpty(p.Description) &&
                               !ParameterDescriptions.CommonParams.ContainsKey(p.Name))
                    .Select(p => p.Name)
                    .OrderBy(n => n)
                    .ToList();

                // 触发分析完成事件
                OnParametersGrouped(new ParameterStatisticsEventArgs
                {
                    TotalCount = parameters.Count,
                    GroupCount = groupedParams.Count,
                    TypeDistribution = typeStats,
                    UnknownParameters = unknownParams,
                    GroupedParameters = groupedParams
                });

                OnStatisticsCalculated(new ParameterStatisticsEventArgs
                {
                    TotalCount = parameters.Count,
                    GroupCount = groupedParams.Count,
                    TypeDistribution = typeStats,
                    UnknownParameters = unknownParams
                });
            });

            OnParametersLoadCompleted(new ParameterStatisticsEventArgs
            {
                TotalCount = parameters.Count,
                GroupedParameters = parameters.Values
                    .GroupBy(p => GetParameterGroup(p.Name))
                    .ToDictionary(g => g.Key, g => g.ToList())
            });
        }

        private async Task CloseConnectionAsync()
        {
            try
            {
                OnHeartbeatStopped(new HeartbeatEventArgs 
                { 
                    Count = heartbeatCount, 
                    IsActive = false 
                });

                if (serialPort?.IsOpen == true)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                }

                OnConnectionClosed(new ConnectionEventArgs
                {
                    Message = "串口已关闭",
                    IsSuccessful = true
                });
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    ErrorMessage = "关闭连接时出错",
                    Exception = ex,
                    Level = ErrorLevel.Warning,
                    Source = "Connection"
                });
            }
        }
        #endregion

        #region 事件触发方法
        protected virtual void OnConnectionAttempting(ConnectionEventArgs e)
        {
            ConnectionAttempting?.Invoke(this, e);
        }

        protected virtual void OnConnectionEstablished(ConnectionEventArgs e)
        {
            ConnectionEstablished?.Invoke(this, e);
        }

        protected virtual void OnConnectionFailed(ConnectionEventArgs e)
        {
            ConnectionFailed?.Invoke(this, e);
        }

        protected virtual void OnConnectionClosed(ConnectionEventArgs e)
        {
            ConnectionClosed?.Invoke(this, e);
        }

        protected virtual void OnHeartbeatStarted(HeartbeatEventArgs e)
        {
            HeartbeatStarted?.Invoke(this, e);
        }

        protected virtual void OnHeartbeatSent(HeartbeatEventArgs e)
        {
            HeartbeatSent?.Invoke(this, e);
        }

        protected virtual void OnHeartbeatStopped(HeartbeatEventArgs e)
        {
            HeartbeatStopped?.Invoke(this, e);
        }

        protected virtual void OnParameterRequestSent(StatusEventArgs e)
        {
            ParameterRequestSent?.Invoke(this, e);
        }

        protected virtual void OnRetryAttempted(StatusEventArgs e)
        {
            RetryAttempted?.Invoke(this, e);
        }

        protected virtual void OnExpectedParameterCountSet(StatusEventArgs e)
        {
            ExpectedParameterCountSet?.Invoke(this, e);
        }

        protected virtual void OnParameterReceived(ParameterReceivedEventArgs e)
        {
            ParameterReceived?.Invoke(this, e);
        }

        protected virtual void OnProgressUpdated(ParameterReceivedEventArgs e)
        {
            ProgressUpdated?.Invoke(this, e);
        }

        protected virtual void OnParametersLoadCompleted(ParameterStatisticsEventArgs e)
        {
            ParametersLoadCompleted?.Invoke(this, e);
        }

        protected virtual void OnStatusUpdated(StatusEventArgs e)
        {
            StatusUpdated?.Invoke(this, e);
        }

        protected virtual void OnDebugMessageReceived(StatusEventArgs e)
        {
            DebugMessageReceived?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        protected virtual void OnParametersGrouped(ParameterStatisticsEventArgs e)
        {
            ParametersGrouped?.Invoke(this, e);
        }

        protected virtual void OnStatisticsCalculated(ParameterStatisticsEventArgs e)
        {
            StatisticsCalculated?.Invoke(this, e);
        }
        #endregion

        #region 原有的核心方法（保持不变，但移除 Console 输出）
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                int bytesRead = serialPort.Read(buffer, 0, bytesToRead);

                for (int i = 0; i < bytesRead; i++)
                {
                    receiveBuffer[bufferIndex++] = buffer[i];
                    if (bufferIndex >= receiveBuffer.Length)
                    {
                        bufferIndex = 0;
                    }
                }

                ParseMAVLinkV2Messages(buffer, bytesRead);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    ErrorMessage = "接收数据错误",
                    Exception = ex,
                    Level = ErrorLevel.Error,
                    Source = "SerialPort"
                });
            }
        }

        private void ParseParamValueV2(byte[] payload)
        {
            try
            {
                if (payload.Length < 25) return;

                float paramValue = BitConverter.ToSingle(payload, 0);
                ushort paramCount = BitConverter.ToUInt16(payload, 4);
                ushort paramIndex = BitConverter.ToUInt16(payload, 6);

                byte[] paramIdBytes = new byte[16];
                Array.Copy(payload, 8, paramIdBytes, 0, 16);
                string paramId = System.Text.Encoding.ASCII.GetString(paramIdBytes).TrimEnd('\0').Trim();

                byte paramType = payload[24];

                // 更新期望的参数数量
                if (expectedParamCount == -1 && paramCount > 0)
                {
                    expectedParamCount = paramCount;
                    OnExpectedParameterCountSet(new StatusEventArgs
                    {
                        Message = $"期望接收 {paramCount} 个参数",
                        Type = StatusType.Info
                    });
                }

                // 添加或更新参数信息
                if (!string.IsNullOrEmpty(paramId))
                {
                    ParameterInfo param;
                    bool isNewParam = false;

                    if (!parameters.ContainsKey(paramId))
                    {
                        param = new ParameterInfo
                        {
                            Name = paramId,
                            Value = paramValue,
                            Type = (MAV_PARAM_TYPE)paramType,
                            Index = paramIndex,
                            Group = GetParameterGroup(paramId)
                        };

                        // 初始化参数元数据
                        if (ParameterDescriptions.CommonParams.ContainsKey(paramId))
                        {
                            var info = ParameterDescriptions.CommonParams[paramId];
                            param.Description = info.description;
                            param.Units = info.units;
                            param.MinValue = info.min;
                            param.MaxValue = info.max;
                        }

                        parameters[paramId] = param;
                        isNewParam = true;
                    }
                    else
                    {
                        param = parameters[paramId];
                        param.Value = paramValue;
                        param.Type = (MAV_PARAM_TYPE)paramType;
                        param.Index = paramIndex;
                    }

                    receivedParams.Add(paramIndex);

                    if (isNewParam)
                    {
                        OnParameterReceived(new ParameterReceivedEventArgs
                        {
                            Parameter = param,
                            CurrentCount = receivedParams.Count,
                            TotalCount = expectedParamCount,
                            ProgressPercentage = expectedParamCount > 0 
                                ? (double)receivedParams.Count / expectedParamCount * 100 
                                : 0
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    ErrorMessage = "解析参数值失败",
                    Exception = ex,
                    Level = ErrorLevel.Warning,
                    Source = "ParameterParser"
                });
            }
        }

        // 其他核心方法保持不变，只是移除 Console.WriteLine...
        private void ParseMAVLinkV2Messages(byte[] buffer, int length) { /* 原有实现，移除Console输出 */ }
        private void SendHeartbeatV2() { /* 原有实现，移除Console输出 */ }
        private void RequestParameterListV2() { /* 原有实现，移除Console输出 */ }
        private ushort CalculateCRC16(byte[] buffer, uint msgId) { /* 原有实现 */ }
        private ushort CRC16_Accumulate(byte b, ushort crc) { /* 原有实现 */ }
        private string GetParameterGroup(string paramName) { /* 原有实现 */ }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            if (!isDisposed)
            {
                CloseConnectionAsync().Wait(2000);
                serialPort?.Dispose();
                isDisposed = true;
            }
        }
        #endregion
    }
}