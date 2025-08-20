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
                    Window? mainWin = null;

                    // 根据用户类型创建相应的窗口
                    switch (login.LoginUser.Type)
                    {
                        case UserType.Admin:
                            mainWin = new AdminDialog(login.LoginUser);
                            break;
                        case UserType.Teacher:
                            mainWin = new QuestionPanel(login.LoginUser);
                            break;
                        case UserType.Student:
                            mainWin = new MainWindow(login.LoginUser);
                            break;
                    }

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
    }
}