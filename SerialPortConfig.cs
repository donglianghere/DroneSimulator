using System.IO.Ports;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.IO;
using System.Linq;

namespace DroneSimulator
{
    /// <summary>
    /// 串口用途枚举
    /// </summary>
    public enum SerialPortPurpose
    {
        [Description("未分配")]
        Unassigned = 0,
        [Description("无人机检修")]
        DroneRepair = 1,
        [Description("飞控连接")]
        FlightController = 2,
        [Description("参数调试")]
        ParameterDebug = 3,
        [Description("数据记录")]
        DataLogging = 4,
        [Description("传感器监控")]
        SensorMonitoring = 5,
        [Description("备用通信")]
        BackupComm = 6
    }

    /// <summary>
    /// 串口配置类 - 扩展版本，保持向后兼容
    /// </summary>
    public class SerialPortConfig : INotifyPropertyChanged
    {
        private string _portName = "";
        private int _baudRate = 9600;
        private Parity _parity = Parity.None;
        private StopBits _stopBits = StopBits.One;
        private SerialPortPurpose _purpose = SerialPortPurpose.Unassigned;
        private string _description = "";
        private bool _isEnabled = true;
        private bool _isInUse = false;
        private DateTime _lastUsed = DateTime.MinValue;

        public string PortName
        {
            get => _portName;
            set { _portName = value; OnPropertyChanged(); }
        }

        public int BaudRate
        {
            get => _baudRate;
            set { _baudRate = value; OnPropertyChanged(); }
        }

        public Parity Parity
        {
            get => _parity;
            set { _parity = value; OnPropertyChanged(); }
        }

        public StopBits StopBits
        {
            get => _stopBits;
            set { _stopBits = value; OnPropertyChanged(); }
        }

        public SerialPortPurpose Purpose
        {
            get => _purpose;
            set { _purpose = value; OnPropertyChanged(); OnPropertyChanged(nameof(PurposeDisplay)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
        }

        public bool IsInUse
        {
            get => _isInUse;
            set { _isInUse = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
        }

        public DateTime LastUsed
        {
            get => _lastUsed;
            set { _lastUsed = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastUsedDisplay)); }
        }

        // 显示属性
        public string PurposeDisplay => GetEnumDescription(Purpose);
        public string LastUsedDisplay => LastUsed == DateTime.MinValue ? "从未使用" : LastUsed.ToString("yyyy-MM-dd HH:mm");
        public string StatusDisplay => IsInUse ? "使用中" : (IsEnabled ? "就绪" : "禁用");

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault() as DescriptionAttribute;
            return attribute?.Description ?? value.ToString();
        }

        /// <summary>
        /// 创建 SerialPort 实例
        /// </summary>
        public SerialPort CreateSerialPort()
        {
            return new SerialPort(PortName)
            {
                BaudRate = BaudRate,
                Parity = Parity,
                StopBits = StopBits,
                DataBits = 8,
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                DtrEnable = true,
                RtsEnable = true
            };
        }
    }

    /// <summary>
    /// 统一的串口管理器
    /// </summary>
    public static class SerialPortManager
    {
        private const string CONFIG_FILE = "serial_ports_config.json";
        private const string LEGACY_CONFIG_FILE = "config_serialport.json"; // 兼容旧配置文件
        private static readonly object _lock = new object();
        private static List<SerialPortConfig>? _configs;

        public static List<SerialPortConfig> Configs
        {
            get
            {
                if (_configs == null)
                {
                    LoadConfigurations();
                }
                return _configs!;
            }
        }

        /// <summary>
        /// 获取可用串口列表
        /// </summary>
        public static List<string> GetAvailablePorts()
        {
            return SerialPort.GetPortNames().OrderBy(p => p).ToList();
        }

        /// <summary>
        /// 根据用途获取串口配置
        /// </summary>
        public static SerialPortConfig? GetConfigByPurpose(SerialPortPurpose purpose)
        {
            return Configs.FirstOrDefault(c => c.Purpose == purpose && c.IsEnabled);
        }

        /// <summary>
        /// 获取无人机检修端口配置（系统主要用途）
        /// </summary>
        public static SerialPortConfig? GetDroneRepairConfig()
        {
            var config = GetConfigByPurpose(SerialPortPurpose.DroneRepair);
            if (config == null)
            {
                // 尝试使用 COM7（系统默认）
                config = Configs.FirstOrDefault(c => c.PortName == "COM6");
                if (config != null)
                {
                    config.Purpose = SerialPortPurpose.DroneRepair;
                    config.Description = "无人机检修默认端口";
                    SaveConfigurations();
                }
                else if (GetAvailablePorts().Contains("COM6"))
                {
                    // 创建 COM6 的默认配置
                    config = new SerialPortConfig
                    {
                        PortName = "COM6",
                        BaudRate = 9600,
                        Purpose = SerialPortPurpose.DroneRepair,
                        Description = "无人机检修默认端口",
                        IsEnabled = true
                    };
                    Configs.Add(config);
                    SaveConfigurations();
                }
            }
            return config;
        }

        /// <summary>
        /// 根据端口名获取配置
        /// </summary>
        public static SerialPortConfig? GetConfigByPort(string portName)
        {
            return Configs.FirstOrDefault(c => c.PortName.Equals(portName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 添加或更新配置
        /// </summary>
        public static void AddOrUpdateConfig(SerialPortConfig config)
        {
            lock (_lock)
            {
                var existing = Configs.FirstOrDefault(c => c.PortName == config.PortName);
                if (existing != null)
                {
                    existing.BaudRate = config.BaudRate;
                    existing.Parity = config.Parity;
                    existing.StopBits = config.StopBits;
                    existing.Purpose = config.Purpose;
                    existing.Description = config.Description;
                    existing.IsEnabled = config.IsEnabled;
                    existing.IsInUse = config.IsInUse;
                }
                else
                {
                    Configs.Add(config);
                }
                SaveConfigurations();
            }
        }

        /// <summary>
        /// 设置端口使用状态
        /// </summary>
        public static void SetPortInUse(string portName, bool inUse)
        {
            var config = GetConfigByPort(portName);
            if (config != null)
            {
                config.IsInUse = inUse;
                if (inUse)
                {
                    config.LastUsed = DateTime.Now;
                }
                SaveConfigurations();
            }
        }

        /// <summary>
        /// 测试端口连接
        /// </summary>
        public static bool TestPortConnection(SerialPortConfig config)
        {
            try
            {
                using var port = config.CreateSerialPort();
                port.Open();
                port.Close();

                config.LastUsed = DateTime.Now;
                SaveConfigurations();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public static void SaveConfigurations()
        {
            try
            {
                lock (_lock)
                {
                    var json = JsonSerializer.Serialize(Configs, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(CONFIG_FILE, json);

                    // 同时保存兼容的旧格式配置（无人机检修端口）
                    var droneConfig = GetDroneRepairConfig();
                    if (droneConfig != null)
                    {
                        var legacyConfig = new
                        {
                            PortName = droneConfig.PortName,
                            BaudRate = droneConfig.BaudRate,
                            Parity = droneConfig.Parity,
                            StopBits = droneConfig.StopBits
                        };
                        var legacyJson = JsonSerializer.Serialize(legacyConfig, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(LEGACY_CONFIG_FILE, legacyJson);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存串口配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public static void LoadConfigurations()
        {
            lock (_lock)
            {
                try
                {
                    _configs = new List<SerialPortConfig>();

                    // 优先加载新格式配置
                    if (File.Exists(CONFIG_FILE))
                    {
                        var json = File.ReadAllText(CONFIG_FILE);
                        var configs = JsonSerializer.Deserialize<List<SerialPortConfig>>(json);

                        if (configs != null)
                        {
                            _configs.AddRange(configs);
                        }
                    }
                    // 如果新配置不存在，尝试从旧配置迁移
                    else if (File.Exists(LEGACY_CONFIG_FILE))
                    {
                        MigrateFromLegacyConfig();
                    }

                    // 确保有基本配置
                    EnsureBasicConfigurations();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"加载串口配置失败: {ex.Message}");
                    _configs = new List<SerialPortConfig>();
                    EnsureBasicConfigurations();
                }
            }
        }

        /// <summary>
        /// 从旧配置文件迁移
        /// </summary>
        private static void MigrateFromLegacyConfig()
        {
            try
            {
                var json = File.ReadAllText(LEGACY_CONFIG_FILE);
                var legacyConfig = JsonSerializer.Deserialize<SerialPortConfig>(json);

                if (legacyConfig != null && !string.IsNullOrEmpty(legacyConfig.PortName))
                {
                    legacyConfig.Purpose = SerialPortPurpose.DroneRepair;
                    legacyConfig.Description = "从旧配置迁移的无人机检修端口";
                    legacyConfig.IsEnabled = true;
                    _configs!.Add(legacyConfig);

                    System.Diagnostics.Debug.WriteLine($"已从旧配置迁移端口: {legacyConfig.PortName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"迁移旧配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保有基本配置
        /// </summary>
        private static void EnsureBasicConfigurations()
        {
            var availablePorts = GetAvailablePorts();

            foreach (var port in availablePorts)
            {
                if (!_configs!.Any(c => c.PortName == port))
                {
                    var config = new SerialPortConfig
                    {
                        PortName = port,
                        BaudRate = port == "COM6" ? 115200 : 9600,
                        Purpose = port == "COM6" ? SerialPortPurpose.DroneRepair : SerialPortPurpose.Unassigned,
                        Description = port == "COM6" ? "无人机检修默认端口" : "",
                        IsEnabled = true
                    };
                    _configs.Add(config);
                }
            }

            SaveConfigurations();
        }

        /// <summary>
        /// 刷新端口列表
        /// </summary>
        public static void RefreshPorts()
        {
            var availablePorts = GetAvailablePorts();

            // 添加新端口
            foreach (var port in availablePorts)
            {
                if (!Configs.Any(c => c.PortName == port))
                {
                    var config = new SerialPortConfig
                    {
                        PortName = port,
                        BaudRate = 9600,
                        Purpose = SerialPortPurpose.Unassigned,
                        IsEnabled = true
                    };
                    Configs.Add(config);
                }
            }

            // 更新端口可用状态
            foreach (var config in Configs)
            {
                config.IsEnabled = availablePorts.Contains(config.PortName);
                if (!config.IsEnabled)
                {
                    config.IsInUse = false; // 不可用的端口不能处于使用状态
                }
            }

            SaveConfigurations();
        }

        /// <summary>
        /// 智能分配端口用途，确保唯一性
        /// </summary>
        public static bool AssignPortPurpose(string portName, SerialPortPurpose newPurpose, out string conflictInfo)
        {
            conflictInfo = "";

            try
            {
                // 1. 检查是否有其他端口已使用此用途
                var existingPortWithPurpose = Configs.FirstOrDefault(c =>
                    c.Purpose == newPurpose &&
                    c.PortName != portName &&
                    newPurpose != SerialPortPurpose.Unassigned);

                if (existingPortWithPurpose != null)
                {
                    conflictInfo = $"用途'{GetPurposeDescription(newPurpose)}'已被端口{existingPortWithPurpose.PortName}使用";

                    // 清除冲突端口的用途
                    existingPortWithPurpose.Purpose = SerialPortPurpose.Unassigned;
                    existingPortWithPurpose.Description = $"用途已转移到{portName}";
                }

                // 2. 更新目标端口配置
                var targetConfig = GetConfigByPort(portName);
                if (targetConfig != null)
                {
                    var oldPurpose = targetConfig.Purpose;
                    targetConfig.Purpose = newPurpose;
                    targetConfig.Description = GetPurposeDescription(newPurpose);
                    targetConfig.LastUsed = DateTime.Now;

                    if (oldPurpose != SerialPortPurpose.Unassigned && oldPurpose != newPurpose)
                    {
                        conflictInfo += (string.IsNullOrEmpty(conflictInfo) ? "" : "; ") +
                                       $"端口{portName}原用途'{GetPurposeDescription(oldPurpose)}'已更改";
                    }
                }
                else
                {
                    // 创建新配置
                    var newConfig = new SerialPortConfig
                    {
                        PortName = portName,
                        BaudRate = GetDefaultBaudRate(newPurpose),
                        Purpose = newPurpose,
                        Description = GetPurposeDescription(newPurpose),
                        IsEnabled = true,
                        LastUsed = DateTime.Now
                    };
                    Configs.Add(newConfig);
                }

                SaveConfigurations();
                return true;
            }
            catch (Exception ex)
            {
                conflictInfo = $"配置失败：{ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 根据用途获取默认波特率
        /// </summary>
        private static int GetDefaultBaudRate(SerialPortPurpose purpose)
        {
            return purpose switch
            {
                SerialPortPurpose.FlightController => 115200,
                SerialPortPurpose.DroneRepair => 115200,
                SerialPortPurpose.ParameterDebug => 57600,
                SerialPortPurpose.DataLogging => 9600,
                _ => 9600
            };
        }

        /// <summary>
        /// 获取用途描述（静态方法）
        /// </summary>
        private static string GetPurposeDescription(SerialPortPurpose purpose)
        {
            return purpose switch
            {
                SerialPortPurpose.DroneRepair => "无人机检修专用端口",
                SerialPortPurpose.FlightController => "飞控通信端口",
                SerialPortPurpose.ParameterDebug => "参数调试端口",
                SerialPortPurpose.DataLogging => "数据记录端口",
                SerialPortPurpose.SensorMonitoring => "传感器监控端口",
                SerialPortPurpose.BackupComm => "备用通信端口",
                _ => "通用串口"
            };
        }

        /// <summary>
        /// 验证配置完整性
        /// </summary>
        public static List<string> ValidateConfigurations()
        {
            var issues = new List<string>();

            // 检查是否有重复用途
            var purposeGroups = Configs
                .Where(c => c.Purpose != SerialPortPurpose.Unassigned)
                .GroupBy(c => c.Purpose)
                .Where(g => g.Count() > 1);

            foreach (var group in purposeGroups)
            {
                var ports = string.Join(", ", group.Select(c => c.PortName));
                issues.Add($"用途'{GetPurposeDescription(group.Key)}'被多个端口使用：{ports}");
            }

            // 检查关键用途是否已配置
            if (!Configs.Any(c => c.Purpose == SerialPortPurpose.DroneRepair && c.IsEnabled))
            {
                issues.Add("未配置无人机检修端口");
            }

            if (!Configs.Any(c => c.Purpose == SerialPortPurpose.FlightController && c.IsEnabled))
            {
                issues.Add("未配置飞控通信端口（电机测试需要）");
            }

            return issues;
        }
    }
}