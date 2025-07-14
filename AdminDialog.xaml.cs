using RJCP.IO.Ports;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RjcpPorts = RJCP.IO.Ports;

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

            // 权限控制：非管理员禁用账户管理按钮
            if (!IsCurrentUserAdmin())
            {
                var addBtn = this.FindName("AddAccountButton") as Button;
                var delBtn = this.FindName("DeleteAccountButton") as Button;
                if (addBtn != null) addBtn.IsEnabled = false;
                if (delBtn != null) delBtn.IsEnabled = false;
            }
        }

        private void AdminDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 3. 使用新库的方法获取串口列表
            PortComboBox.ItemsSource = System.IO.Ports.SerialPort.GetPortNames();

            int defaultBaudIndex = 0;
            int defaultParityIndex = 0;
            int defaultStopBitsIndex = 1;

            if (File.Exists("config_serialport.json"))
            {
                try
                {
                    var json = File.ReadAllText("config_serialport.json");
                    var config = JsonSerializer.Deserialize<SerialPortConfig>(json);

                    if (config != null)
                    {
                        if (!string.IsNullOrEmpty(config.PortName))
                        {
                            PortComboBox.SelectedItem = config.PortName;
                        }
                        else if (PortComboBox.Items.Count > 0)
                        {
                            PortComboBox.SelectedIndex = 0;
                        }

                        BaudRateComboBox.Text = config.BaudRate.ToString();
                        ParityComboBox.SelectedIndex = (int)config.Parity;
                        StopBitsComboBox.SelectedIndex = (int)config.StopBits;
                    }
                }
                catch
                {
                    if (PortComboBox.Items.Count > 0) PortComboBox.SelectedIndex = 0;
                    BaudRateComboBox.SelectedIndex = defaultBaudIndex;
                    ParityComboBox.SelectedIndex = defaultParityIndex;
                    StopBitsComboBox.SelectedIndex = defaultStopBitsIndex;
                }
            }
            else
            {
                if (PortComboBox.Items.Count > 0) PortComboBox.SelectedIndex = 0;
                BaudRateComboBox.SelectedIndex = defaultBaudIndex;
                ParityComboBox.SelectedIndex = defaultParityIndex;
                StopBitsComboBox.SelectedIndex = defaultStopBitsIndex;
            }
        }

        private void OpenPort_Click(object sender, RoutedEventArgs e)
        {
            if (PortComboBox.SelectedItem == null)
            {
                MessageBox.Show("请选择串口！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 4. 使用新库的 "using" 模式来测试端口，确保资源被释放
            using (var testPort = new RjcpPorts.SerialPortStream())
            {
                try
                {
                    testPort.PortName = PortComboBox.SelectedItem.ToString()!;
                    testPort.BaudRate = int.Parse(((ComboBoxItem)BaudRateComboBox.SelectedItem!).Content.ToString()!);
                    testPort.Open();
                    MessageBox.Show("串口打开成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"串口打开失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            } // testPort 在这里会自动关闭和释放
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = new SerialPortConfig
            {
                PortName = PortComboBox.Text,
                BaudRate = int.Parse(BaudRateComboBox.Text),
                // *** 关键改动：保存时，也使用新库的枚举类型 ***
                Parity = (RjcpPorts.Parity)ParityComboBox.SelectedIndex,
                StopBits = (RjcpPorts.StopBits)StopBitsComboBox.SelectedIndex
            };

            string json = JsonSerializer.Serialize(config);
            File.WriteAllText("config_serialport.json", json);
            MessageBox.Show("串口参数已保存。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private bool IsCurrentUserAdmin()
        {
            return _currentAdmin != null && _currentAdmin.Type == UserType.Admin;
        }

        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            if (!IsCurrentUserAdmin())
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
            if (!IsCurrentUserAdmin())
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
            if (!IsCurrentUserAdmin())
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
                UserManager.UpdateUserPassword(selected.IdNumber, selected.Type, dlg.NewPassword); // 你需实现此方法
                MessageBox.Show("密码修改成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}