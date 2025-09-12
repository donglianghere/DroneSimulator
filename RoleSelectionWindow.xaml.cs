using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DroneSimulator
{
    public partial class RoleSelectionWindow : Window
    {
        public UserType SelectedRole { get; private set; }
        public bool IsRoleSelected { get; private set; } = false;
        
        private UserInfo _currentUser;

        public RoleSelectionWindow(UserInfo user)
        {
            InitializeComponent();
            _currentUser = user;
            InitializeUI();
        }

        private void InitializeUI()
        {
            UserInfoText.Text = $"当前用户：{_currentUser.Name} ({GetUserTypeDisplayName(_currentUser.Type)})";
            
            // 根据用户权限添加可选角色按钮
            var availableRoles = GetAvailableRoles(_currentUser.Type);
            
            foreach (var role in availableRoles)
            {
                var button = CreateRoleButton(role);
                RoleButtonPanel.Children.Add(button);
            }
        }

        private List<UserType> GetAvailableRoles(UserType userType)
        {
            var roles = new List<UserType>();

            switch (userType)
            {
                case UserType.Admin:
                    roles.Add(UserType.Admin);
                    roles.Add(UserType.Teacher);
                    roles.Add(UserType.Student);  // 确保管理员可以选择学生角色
                    break;
                case UserType.Teacher:
                    roles.Add(UserType.Teacher);
                    roles.Add(UserType.Student);
                    break;
                case UserType.Student:
                    roles.Add(UserType.Student);
                    break;
            }

            return roles;
        }

        private Button CreateRoleButton(UserType role)
        {
            var button = new Button
            {
                Content = $"以{GetUserTypeDisplayName(role)}身份登录",
                Width = 200,
                Height = 40,
                Margin = new Thickness(0, 5, 0, 5),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            // 根据角色设置不同颜色
            button.Background = role switch
            {
                UserType.Admin => new SolidColorBrush(Color.FromRgb(255, 107, 107)), // 红色
                UserType.Teacher => new SolidColorBrush(Color.FromRgb(54, 162, 235)), // 蓝色
                UserType.Student => new SolidColorBrush(Color.FromRgb(75, 192, 192)), // 青色
                _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))
            };

            button.Click += (sender, e) => RoleButton_Click(role);
            
            return button;
        }

        private void RoleButton_Click(UserType role)
        {
            SelectedRole = role;
            IsRoleSelected = true;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
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
    }
}