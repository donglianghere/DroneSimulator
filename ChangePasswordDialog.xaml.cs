using System.Windows;

namespace DroneSimulator
{
    public partial class ChangePasswordDialog : Window
    {
        public string NewPassword { get; private set; } = "";

        public ChangePasswordDialog(UserInfo user)
        {
            InitializeComponent();
            // 显示用户信息
            TypeTextBox.Text = user.Type switch
            {
                UserType.Student => "学生",
                UserType.Teacher => "老师",
                UserType.Admin => "管理员",
                _ => "未知"
            };
            NameTextBox.Text = user.Name;
            IdTextBox.Text = user.IdNumber;
        }

        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            string pwd = PasswordBox.Password.Trim();
            if (string.IsNullOrEmpty(pwd) || pwd.Length < 6 || pwd.Length > 20)
            {
                MessageBox.Show("密码长度需为6-20位！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            NewPassword = pwd;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}