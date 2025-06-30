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
                if (login.ShowDialog() == true && login.LoginUser != null)
                {
                    Window? mainWin = null;
                    if (login.LoginUser.Type == UserType.Admin)
                        mainWin = new AdminDialog();
                    if (login.LoginUser.Type == UserType.Teacher)
                        mainWin = new QuestionPanel(login.LoginUser);
                    if (login.LoginUser.Type == UserType.Student)
                        mainWin = new MainWindow(login.LoginUser);

                    Application.Current.MainWindow = mainWin;
                    mainWin.ShowDialog(); // 用 ShowDialog 等待窗口关闭
                }
                else
                {
                    break; // 登录取消或失败，退出循环
                }
            }
            Application.Current.Shutdown();
        }
    }
}
