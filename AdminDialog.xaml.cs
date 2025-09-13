using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DroneSimulator
{
    public class UserTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserType type)
            {
                return type switch
                {
                    UserType.Student => "学生",
                    UserType.Teacher => "老师",
                    UserType.Admin => "管理员",
                    _ => "未知"
                };
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "学生" => UserType.Student,
                "老师" => UserType.Teacher,
                "管理员" => UserType.Admin,
                _ => UserType.Student
            };
        }
    }

    public partial class AdminDialog : Window
    {
        private ObservableCollection<UserInfo> _accounts = new();
        private UserInfo? _currentAdmin;

        public AdminDialog(UserInfo? currentAdmin = null)
        {
            InitializeComponent();
            Loaded += AdminDialog_Loaded;
            _currentAdmin = currentAdmin;
            LoadAccounts();

            // 权限控制：基于原始身份而不是当前角色
            if (!IsCurrentUserRealAdmin())
            {
                var addBtn = this.FindName("AddAccountButton") as Button;
                var delBtn = this.FindName("DeleteAccountButton") as Button;
                var editBtn = this.FindName("EditAccountButton") as Button;
                if (addBtn != null) addBtn.IsEnabled = false;
                if (delBtn != null) delBtn.IsEnabled = false;
                if (editBtn != null) editBtn.IsEnabled = false;
            }

            // 在窗口标题中显示当前登录身份信息
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            string roleInfo = "";
            if (_currentAdmin != null && _currentAdmin.Type != UserType.Admin)
            {
                roleInfo = $" - {GetUserTypeDisplayName(_currentAdmin.Type)}以管理员身份登录";
            }
            this.Title = $"系统管理{roleInfo}";
        }

        private string GetUserTypeDisplayName(UserType type)
        {
            return type switch
            {
                UserType.Admin => "管理员",
                UserType.Teacher => "教师",
                UserType.Student => "学生",
                _ => "未知"
            };
        }

        // 修改权限验证：检查原始身份而不是当前角色
        private bool IsCurrentUserRealAdmin()
        {
            return _currentAdmin != null && _currentAdmin.Type == UserType.Admin;
        }

        // 保持现有方法兼容性
        private bool IsCurrentUserAdmin()
        {
            return IsCurrentUserRealAdmin();
        }

        private void AdminDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPortsAndConfigs();
            LoadAccounts();

            // ========== 添加配置完整性检查 ==========
            CheckConfigurationCompleteness();
        }

        private void CheckConfigurationCompleteness()
        {
            try
            {
                var issues = SerialPortManager.ValidateConfigurations();

                if (issues.Any())
                {
                    var issueText = string.Join("\n", issues);
                    PortStatusText.Text = $"配置问题：{issues.Count}个";
                    PortStatusText.Foreground = new SolidColorBrush(Colors.Red);

                    MessageBox.Show($"发现串口配置问题：\n\n{issueText}\n\n建议：\n" +
                                   "• 为无人机检修功能分配一个端口\n" +
                                   "• 为电机测试功能分配一个端口（飞控通信）\n" +
                                   "• 确保每个用途只分配给一个端口",
                                   "配置检查", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    PortStatusText.Text = "配置正常";
                    PortStatusText.Foreground = new SolidColorBrush(Colors.Green);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置检查失败: {ex.Message}");
            }
        }

        private void LoadPortsAndConfigs()
        {
            try
            {
                // 加载可用端口
                var availablePorts = SerialPortManager.GetAvailablePorts();
                PortComboBox.ItemsSource = availablePorts;

                // 初始化串口用途下拉框
                PurposeComboBox.ItemsSource = new[]
                {
                    new { Value = SerialPortPurpose.DroneRepair, Display = "无人机检修" },
                    new { Value = SerialPortPurpose.FlightController, Display = "A3飞控连接" },
                    new { Value = SerialPortPurpose.ParameterDebug, Display = "PX4飞控连接" },
                    //new { Value = SerialPortPurpose.DataLogging, Display = "数据记录" },
                    //new { Value = SerialPortPurpose.SensorMonitoring, Display = "传感器监控" },
                    //new { Value = SerialPortPurpose.BackupComm, Display = "备用通信" },
                    //new { Value = SerialPortPurpose.Unassigned, Display = "未分配" }
                };
                PurposeComboBox.SelectedValuePath = "Value";
                PurposeComboBox.DisplayMemberPath = "Display";

                // 默认选择无人机检修端口
                var droneConfig = SerialPortManager.GetDroneRepairConfig();
                if (droneConfig != null)
                {
                    PortComboBox.SelectedItem = droneConfig.PortName;
                    LoadConfigToUI(droneConfig);
                }
                else if (availablePorts.Any())
                {
                    PortComboBox.SelectedIndex = 0;
                    // 为第一个端口创建默认配置
                    if (PortComboBox.SelectedItem is string firstPort)
                    {
                        var defaultConfig = new SerialPortConfig
                        {
                            PortName = firstPort,
                            BaudRate = 115200,
                            Purpose = SerialPortPurpose.Unassigned,
                            IsEnabled = true
                        };
                        LoadConfigToUI(defaultConfig);
                    }
                }

                UpdatePortStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载串口配置失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // 提供基本的fallback
                var basicPorts = SerialPort.GetPortNames();
                PortComboBox.ItemsSource = basicPorts;
                if (basicPorts.Length > 0)
                {
                    PortComboBox.SelectedIndex = 0;
                }
            }
        }


        private void PortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PortComboBox.SelectedItem is string selectedPort)
            {
                var config = SerialPortManager.GetConfigByPort(selectedPort);
                if (config != null)
                {
                    LoadConfigToUI(config);
                }
                else
                {
                    // 创建默认配置
                    var defaultConfig = new SerialPortConfig
                    {
                        PortName = selectedPort,
                        BaudRate = 115200,
                        Purpose = SerialPortPurpose.Unassigned,
                        IsEnabled = true
                    };
                    LoadConfigToUI(defaultConfig);
                }
                UpdatePortStatus();
            }
        }

        private void PurposeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePortStatus();
        }

        private void LoadConfigToUI(SerialPortConfig config)
        {
            BaudRateComboBox.Text = config.BaudRate.ToString();

            // ========== 修正奇偶校验显示逻辑 ==========
            ParityComboBox.SelectedIndex = config.Parity switch
            {
                Parity.None => 0,  // "无"
                Parity.Even => 1,  // "偶校验"
                Parity.Odd => 2,   // "奇校验"
                _ => 0
            };

            // ========== 修正停止位显示逻辑 ==========
            StopBitsComboBox.SelectedIndex = config.StopBits switch
            {
                StopBits.One => 0,           // "1"
                StopBits.OnePointFive => 1,  // "1.5"
                StopBits.Two => 2,           // "2"
                _ => 0  // 默认选择 "1"
            };

            PurposeComboBox.SelectedValue = config.Purpose;
        }

        private SerialPortConfig? GetConfigFromUI()
        {
            if (PortComboBox.SelectedItem is not string portName)
                return null;

            // 安全地获取波特率
            int baudRate = 115200; // 默认值
            if (BaudRateComboBox.SelectedItem is ComboBoxItem baudItem &&
                int.TryParse(baudItem.Content?.ToString(), out int parsedBaud))
            {
                baudRate = parsedBaud;
            }
            else if (int.TryParse(BaudRateComboBox.Text, out int textBaud))
            {
                baudRate = textBaud;
            }

            // 安全地获取用途
            var purpose = SerialPortPurpose.Unassigned;
            if (PurposeComboBox.SelectedValue is SerialPortPurpose selectedPurpose)
            {
                purpose = selectedPurpose;
            }

            // ========== 修正停止位转换逻辑 ==========
            StopBits stopBits = StopBits.One; // 默认值
            switch (StopBitsComboBox.SelectedIndex)
            {
                case 0: // "1"
                    stopBits = StopBits.One;      // 正确值: 1
                    break;
                case 1: // "1.5"
                    stopBits = StopBits.OnePointFive; // 正确值: 2
                    break;
                case 2: // "2"
                    stopBits = StopBits.Two;      // 正确值: 3
                    break;
                default:
                    stopBits = StopBits.One;
                    break;
            }

            // ========== 修正奇偶校验转换逻辑 ==========
            Parity parity = Parity.None; // 默认值
            switch (ParityComboBox.SelectedIndex)
            {
                case 0: // "无"
                    parity = Parity.None;
                    break;
                case 1: // "偶校验"
                    parity = Parity.Even;
                    break;
                case 2: // "奇校验"
                    parity = Parity.Odd;
                    break;
                default:
                    parity = Parity.None;
                    break;
            }

            return new SerialPortConfig
            {
                PortName = portName,
                BaudRate = baudRate,
                Parity = parity,
                StopBits = stopBits, // 使用修正后的值
                Purpose = purpose,
                IsEnabled = true,
                Description = GetPurposeDescription(purpose)
            };
        }

        // 调试方法：验证停止位转换
        private void DebugStopBitsConversion()
        {
            System.Diagnostics.Debug.WriteLine("停止位枚举值验证:");
            System.Diagnostics.Debug.WriteLine($"StopBits.None = {(int)StopBits.None}");
            System.Diagnostics.Debug.WriteLine($"StopBits.One = {(int)StopBits.One}");
            System.Diagnostics.Debug.WriteLine($"StopBits.OnePointFive = {(int)StopBits.OnePointFive}");
            System.Diagnostics.Debug.WriteLine($"StopBits.Two = {(int)StopBits.Two}");
        }

        // 确保 GetPurposeDescription 方法存在：
        private string GetPurposeDescription(SerialPortPurpose purpose)
        {
            return purpose switch
            {
                SerialPortPurpose.DroneRepair => "无人机检修专用端口",
                SerialPortPurpose.FlightController => "A3飞控通信端口",
                SerialPortPurpose.ParameterDebug => "PX4飞控端口",
                SerialPortPurpose.DataLogging => "数据记录端口",
                SerialPortPurpose.SensorMonitoring => "传感器监控端口",
                SerialPortPurpose.BackupComm => "备用通信端口",
                _ => "通用串口"
            };
        }

        private void OpenPort_Click(object sender, RoutedEventArgs e)
        {
            var config = GetConfigFromUI();
            if (config == null)
            {
                MessageBox.Show("请选择串口！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                PortStatusText.Text = $"正在测试 {config.PortName}...";
                PortStatusText.Foreground = new SolidColorBrush(Colors.Yellow);

                bool success = SerialPortManager.TestPortConnection(config);

                if (success)
                {
                    PortStatusText.Text = $"{config.PortName} 连接成功";
                    PortStatusText.Foreground = new SolidColorBrush(Colors.LightGreen);
                    MessageBox.Show($"端口 {config.PortName} 连接测试成功！", "测试结果",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    PortStatusText.Text = $"{config.PortName} 连接失败";
                    PortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    MessageBox.Show($"端口 {config.PortName} 连接失败！\n请检查端口是否被占用或硬件连接。",
                        "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                PortStatusText.Text = $"{config.PortName} 测试出错";
                PortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                MessageBox.Show($"测试连接时出错：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePortStatus()
        {
            if (PortComboBox.SelectedItem is string portName)
            {
                var config = SerialPortManager.GetConfigByPort(portName);
                var purpose = PurposeComboBox.SelectedValue as SerialPortPurpose? ?? SerialPortPurpose.Unassigned;

                string statusText = $"端口: {portName}";
                if (config != null)
                {
                    statusText += config.IsInUse ? " (使用中)" : " (就绪)";
                    if (config.Purpose != SerialPortPurpose.Unassigned)
                    {
                        statusText += $" - {GetPurposeDescription(config.Purpose)}";
                    }
                }
                else
                {
                    statusText += " (未配置)";
                }

                PortStatusText.Text = statusText;
                PortStatusText.Foreground = new SolidColorBrush(
                    config?.IsEnabled == true ? Colors.LightGreen : Colors.Orange);
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = GetConfigFromUI();
            if (config == null)
            {
                MessageBox.Show("请选择串口！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // ========== 关键逻辑：确保双重唯一性 ==========

                // 1. 如果用途不是"未分配"，清除其他端口的相同用途
                if (config.Purpose != SerialPortPurpose.Unassigned)
                {
                    foreach (var existingConfig in SerialPortManager.Configs)
                    {
                        if (existingConfig.Purpose == config.Purpose &&
                            existingConfig.PortName != config.PortName)
                        {
                            existingConfig.Purpose = SerialPortPurpose.Unassigned;
                            existingConfig.Description = "用途已转移到其他端口";
                        }
                    }
                }

                // 2. 当前端口如果已有其他用途，先清除（确保一个端口只有一个用途）
                var currentPortConfig = SerialPortManager.GetConfigByPort(config.PortName);
                if (currentPortConfig != null && currentPortConfig.Purpose != config.Purpose)
                {
                    // 如果当前端口之前有其他非"未分配"用途，需要用户确认
                    if (currentPortConfig.Purpose != SerialPortPurpose.Unassigned)
                    {
                        var oldPurposeDesc = GetPurposeDescription(currentPortConfig.Purpose);
                        var newPurposeDesc = GetPurposeDescription(config.Purpose);

                        var result = MessageBox.Show(
                            $"端口 {config.PortName} 当前用途为：{oldPurposeDesc}\n" +
                            $"即将更改为：{newPurposeDesc}\n\n" +
                            $"确定要更改吗？这将影响该端口的现有功能。",
                            "确认更改端口用途",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }
                }

                // 3. 保存配置
                SerialPortManager.AddOrUpdateConfig(config);

                // 4. 更新UI状态
                PortStatusText.Text = $"{config.PortName} 配置已保存";
                PortStatusText.Foreground = new SolidColorBrush(Colors.LightGreen);

                // 5. 显示保存结果
                string message = $"端口 {config.PortName} 配置已保存！\n用途：{GetPurposeDescription(config.Purpose)}";

                // 如果是关键用途，提供额外提示
                if (config.Purpose == SerialPortPurpose.DroneRepair)
                {
                    message += "\n\n✓ 无人机检修功能已配置完成";
                }
                else if (config.Purpose == SerialPortPurpose.FlightController)
                {
                    message += "\n\n✓ 电机测试功能已配置完成";
                }

                MessageBox.Show(message, "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);

                // 6. 刷新显示以反映所有更改
                LoadPortsAndConfigs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ManageAllPorts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开简化的串口概览窗口
                var window = new SerialPortOverviewWindow();
                window.ShowDialog();

                // 刷新当前显示
                LoadPortsAndConfigs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开串口管理失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }          

        
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentUserRealAdmin())
            {
                MessageBox.Show("只有管理员才能添加账户！", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new AddAccountDialog();
            if (dlg.ShowDialog() == true && dlg.NewUser != null)
            {
                var exist = UserManager.FindUser(dlg.NewUser.IdNumber, dlg.NewUser.Type);
                if (exist != null)
                {
                    MessageBox.Show("该账户已存在！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                UserManager.AddUser(dlg.NewUser);
                _accounts.Add(dlg.NewUser);
                MessageBox.Show("账户添加成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentUserRealAdmin())
            {
                MessageBox.Show("只有管理员才能删除账户！", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (AccountListView.SelectedItem is not UserInfo selected)
            {
                MessageBox.Show("请先选择要删除的账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // 当前管理员不能删除自己
            if (_currentAdmin != null &&
                selected.IdNumber == _currentAdmin.IdNumber &&
                selected.Type == _currentAdmin.Type)
            {
                MessageBox.Show("不能删除当前登录的管理员账户！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var result = MessageBox.Show($"确定要删除账户：{selected.Name}（{selected.Type}）吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                UserManager.DeleteUser(selected.IdNumber, selected.Type);
                _accounts.Remove(selected);
                MessageBox.Show("账户已删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadAccounts()
        {
            // 加载所有账户
            _accounts = new ObservableCollection<UserInfo>(UserManager.GetAllUsers());
            AccountListView.ItemsSource = _accounts;
        }

        private void EditAccount_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentUserRealAdmin())
            {
                MessageBox.Show("只有管理员才能编辑账户！", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (AccountListView.SelectedItem is not UserInfo selected)
            {
                MessageBox.Show("请先选择要编辑的账户", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new ChangePasswordDialog(selected);
            if (dlg.ShowDialog() == true)
            {
                selected.Password = dlg.NewPassword;
                UserManager.UpdateUserPassword(selected.IdNumber, selected.Type, dlg.NewPassword);
                MessageBox.Show("密码修改成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }        
    }
}