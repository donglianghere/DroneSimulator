using System.Windows;

namespace DroneSimulator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ShowLoginLoop();
        }

        private void ShowLoginLoop()
        {
            while (true)
            {
                var login = new LoginWindow();
                var loginResult = login.ShowDialog();

                if (loginResult == true && login.LoginUser != null)
                {
                    // 如果用户有多个可选角色，显示角色选择窗口
                    UserType selectedRole = login.LoginUser.Type;

                    if (HasMultipleRoles(login.LoginUser.Type))
                    {
                        var roleSelection = new RoleSelectionWindow(login.LoginUser);
                        var roleResult = roleSelection.ShowDialog();

                        if (roleResult == true && roleSelection.IsRoleSelected)
                        {
                            selectedRole = roleSelection.SelectedRole;
                            // 设置当前角色
                            login.LoginUser.CurrentRole = selectedRole;
                        }
                        else
                        {
                            // 用户取消角色选择，回到登录界面
                            continue;
                        }
                    }
                    else
                    {
                        // 如果只有一个角色，直接设置
                        login.LoginUser.CurrentRole = login.LoginUser.Type;
                    }

                    Window? mainWin = CreateWindowForRole(selectedRole, login.LoginUser);

                    if (mainWin != null)
                    {
                        // 设置主窗口
                        Application.Current.MainWindow = mainWin;

                        // 关闭登录窗口
                        login.Close();

                        // 显示主窗口并等待其关闭
                        var result = mainWin.ShowDialog();

                        // 主窗口关闭后，重置MainWindow，继续循环显示登录窗口
                        Application.Current.MainWindow = null;
                    }
                }
                else
                {
                    // 用户取消登录或登录失败，退出循环
                    break;
                }
            }

            // 退出应用程序
            Application.Current.Shutdown();
        }

        private bool HasMultipleRoles(UserType userType)
        {
            return userType == UserType.Admin || userType == UserType.Teacher;
        }

        private Window? CreateWindowForRole(UserType role, UserInfo user)
        {
            return role switch
            {
                UserType.Admin => new AdminDialog(user),
                UserType.Teacher => new QuestionPanel(user),
                UserType.Student => new MainWindow(user),
                _ => null
            };
        }
    }
}