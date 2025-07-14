using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace DroneSimulator
{
    public partial class AddAccountDialog : Window
    {
        public UserInfo? NewUser { get; private set; }

        public AddAccountDialog()
        {
            InitializeComponent();
            TypeComboBox.SelectedIndex = 0;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string typeStr = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "学生";
            UserType type = typeStr == "老师" ? UserType.Teacher :
                            typeStr == "管理员" ? UserType.Admin : UserType.Student;
            string name = NameTextBox.Text.Trim();
            string id = IdTextBox.Text.Trim();
            string pwd = PasswordBox.Password.Trim();

            // 校验
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("请填写所有信息！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Regex.IsMatch(name, @"^[\u4e00-\u9fa5]+$"))
            {
                MessageBox.Show("姓名必须为汉字！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Regex.IsMatch(id, @"^\d{17}[\dXx]$"))
            {
                MessageBox.Show("身份证号格式不正确！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (pwd.Length < 6 || pwd.Length > 20)
            {
                MessageBox.Show("密码长度需为6-20位！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            NewUser = new UserInfo
            {
                Name = name,
                IdNumber = id,
                Password = pwd,
                Type = type
            };
            DialogResult = true;
        }
    }
}