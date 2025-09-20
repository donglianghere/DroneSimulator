using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
namespace DroneSimulator
{

    public partial class LoginWindow : Window
    {
        // 辅助类型
        private class LastUserInfo
        {
            public string Name { get; set; } = "";
            public string IdNumber { get; set; } = "";
            public UserType Type { get; set; }
        }

        private void SaveLastUser(string name, string id, UserType type)
        {
            var lastUser = new { Name = name, IdNumber = id, Type = type };
            File.WriteAllText("last_user.json", System.Text.Json.JsonSerializer.Serialize(lastUser));
        }

        public UserInfo? LoginUser { get; private set; }

        private void UserTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetDefaultUserInfo();
        }

        private void SetDefaultUserInfo()
        {
            string userTypeStr = (UserTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "学生";
            if (userTypeStr == "管理员")
            {
                NameBox.Text = "超级管理员";
                IdBox.Text = "000000000000000001";
                PasswordBox.Password = "";
            }
            else
            {
                NameBox.Text = "";
                IdBox.Text = "";
                PasswordBox.Password = "";
            }
        }

        private void EnsureDefaultAdmin()
        {
            var admin = UserManager.FindUser("000000000000000001", UserType.Admin);
            if (admin == null)
            {
                UserManager.AddUser(new UserInfo
                {
                    Name = "超级管理员",
                    IdNumber = "000000000000000001",
                    Password = "kanghe",
                    Type = UserType.Admin
                });
            }
        }

        public LoginWindow()
        {
            InitializeComponent();            
            UserManager.Load(); // 加载所有用户

            // 确保有初始管理员账户
            EnsureDefaultAdmin();

            // 自动加载最近注册用户
            if (File.Exists("last_user.json"))
            {
                try
                {
                    var json = File.ReadAllText("last_user.json");
                    var lastUser = System.Text.Json.JsonSerializer.Deserialize<LastUserInfo>(json);
                    if (lastUser != null)
                    {
                        NameBox.Text = lastUser.Name;
                        IdBox.Text = lastUser.IdNumber;
                        // 用内容匹配设置下拉框
                        string typeStr = lastUser.Type switch
                        {
                            UserType.Student => "学生",
                            UserType.Teacher => "老师",
                            UserType.Admin => "管理员",
                            _ => "学生"
                        };
                        foreach (ComboBoxItem item in UserTypeCombo.Items)
                        {
                            if ((item.Content?.ToString() ?? "") == typeStr)
                            {
                                UserTypeCombo.SelectedItem = item;
                                break;
                            }
                        }
                        PasswordBox.Password = ""; // 密码不自动填充
                    }
                }
                catch { }
            }
            else
            {
                SetDefaultUserInfo();
            }

            //加载完毕后再绑定事件
            UserTypeCombo.SelectionChanged += UserTypeCombo_SelectionChanged;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            if (hwndSource != null)
                hwndSource.AddHook(WndProc);
        }

        private const int WM_NCLBUTTONDBLCLK = 0x00A3;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
            {
                // 阻止标题栏双击最大化/还原
                handled = true;
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
                NameBox.Tag = "姓名不能为空";
            else if (!Regex.IsMatch(NameBox.Text, @"^[\u4e00-\u9fa5]+$"))
                NameBox.Tag = "姓名必须为汉字";
            else
                NameBox.Tag = "";
        }

        private void IdBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IdBox.Text))
                IdBox.Tag = "身份证不能为空";
            else if (!Regex.IsMatch(IdBox.Text, @"^\d{17}[\dXx]$"))
                IdBox.Tag = "身份证号格式不正确";
            else
                IdBox.Tag = null;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var pwd = PasswordBox.Password;
            if (string.IsNullOrEmpty(pwd))
                PasswordBox.Tag = "密码不能为空";
            else if (pwd.Length < 6 || pwd.Length > 20)
                PasswordBox.Tag = "密码长度需为6-20位";
            else
                PasswordBox.Tag = "";
        }

        bool IsChineseName(string name)
        {
            return Regex.IsMatch(name, @"^[\u4e00-\u9fa5]+$");
        }
        bool IsValidIdNumber(string id)
        {
            return Regex.IsMatch(id, @"^\d{17}[\dXx]$");
        }
        bool IsValidPassword(string pwd)
        {
            return pwd.Length >= 6 && pwd.Length <= 20;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            string id = IdBox.Text.Trim();
            string pwd = PasswordBox.Password.Trim();
            string userTypeStr = (UserTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "学生";
            UserType type = userTypeStr == "老师" ? UserType.Teacher :
                    userTypeStr == "管理员" ? UserType.Admin :
                    UserType.Student;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("请填写所有信息！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsChineseName(name))
            {
                MessageBox.Show("姓名必须为汉字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsValidIdNumber(id))
            {
                MessageBox.Show("身份证号格式不正确！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsValidPassword(pwd))
            {
                MessageBox.Show("密码长度需为6-20位！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 只允许学生注册
            if (type != UserType.Student)
            {
                MessageBox.Show("只允许注册学生用户！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 新增：确保身份证号在所有用户中唯一
            var allUsers = UserManager.GetAllUsers();
            if (allUsers.Any(u => u.IdNumber == id))
            {
                MessageBox.Show("该身份证号已被注册，不能重复注册。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var user = new UserInfo { Name = name, IdNumber = id, Password = pwd, Type = UserType.Student };
            UserManager.AddUser(user);

            // 保存最近注册用户（不含密码）
            var lastUser = new { user.Name, user.IdNumber, user.Type };
            File.WriteAllText("last_user.json", System.Text.Json.JsonSerializer.Serialize(lastUser));

            MessageBox.Show("注册成功，请登录！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);


        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameBox.Text.Trim();
            string id = IdBox.Text.Trim();
            string pwd = PasswordBox.Password.Trim();
            string userTypeStr = (UserTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "学生";
            UserType type = userTypeStr == "老师" ? UserType.Teacher :
                    userTypeStr == "管理员" ? UserType.Admin :
                    UserType.Student;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("请填写所有信息！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsChineseName(name))
            {
                MessageBox.Show("姓名必须为汉字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsValidIdNumber(id))
            {
                MessageBox.Show("身份证号格式不正确！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!IsValidPassword(pwd))
            {
                MessageBox.Show("密码长度需为6-20位！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exist = UserManager.FindUser(id, type);
            if (exist == null)
            {
                MessageBox.Show("用户不存在，请先注册！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (exist.Name != name || exist.Password != pwd)
            {
                MessageBox.Show("用户名或密码错误！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoginUser = exist;
            SaveLastUser(name, id, type);
            DialogResult = true;
        }
    }
}