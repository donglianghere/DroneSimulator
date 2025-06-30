using DroneSimulator;
using System;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DroneSimulator
{
    public class SerialPortConfig
    {
        public string PortName { get; set; } = "";
        public int BaudRate { get; set; } = 9600;
        public Parity Parity { get; set; } = Parity.None;
        public StopBits StopBits { get; set; } = StopBits.One;
    }

    public partial class MainWindow : Window
    {
        public static SerialPort GlobalSerialPort = new SerialPort();
        public static SerialPortConfig SerialConfig = new SerialPortConfig();

        private SerialPort serialPort;
        private UserInfo currentUser;
        private bool[] switchStates = new bool[3] { false, false, false };
        private double currentSpeed = 0;
        private bool isRunning = false;

        // 获取 DroneStatusPanel 实例
        private DroneStatusPanel? dronePanel => LeftPanel.Content as DroneStatusPanel;
        // 在创建DroneStatusPanel时订阅事件
        private void InitializeStudentPanel()
        {
            var studentPanel = new DroneStatusPanel();
            LeftPanel.Content = studentPanel;

            // 订阅开关状态改变事件
            studentPanel.SwitchStateChanged += (index, state) =>
            {
                switchStates[index] = state;
                UpdateButtonState();

                // 只有全部开时才允许动画
                if (studentPanel.AllSwitchOn)
                {
                    // 可选：自动启用“开始”按钮
                }
                else
                {
                    // 只要有一个断开，立即停止动画
                    isRunning = false;
                    dronePanel?.StopPropeller();
                    UpdateButtonState();
                }
            };

            InitializeSerialPort();

            studentPanel.SetSerialPort(serialPort);
            for (int i = 0; i < 3; i++)
                studentPanel.SetSwitchState(i, switchStates[i]);
            studentPanel.SetStatus("停止", System.Windows.Media.Colors.Red);
            studentPanel.SetRpm(0);
        }

        public MainWindow(UserInfo user)
        {
            InitializeComponent();
            LoadSerialPortConfigAndOpen();
            currentUser = user;
            // 你可以在这里根据 currentUser 做初始化
        }

        private void LoadSerialPortConfigAndOpen()
        {
            if (File.Exists("config_serialport.json"))
            {
                try
                {
                    var json = File.ReadAllText("config_serialport.json");
                    SerialConfig = JsonSerializer.Deserialize<SerialPortConfig>(json);

                    GlobalSerialPort.PortName = SerialConfig.PortName;
                    GlobalSerialPort.BaudRate = SerialConfig.BaudRate;
                    GlobalSerialPort.Parity = SerialConfig.Parity;
                    GlobalSerialPort.StopBits = SerialConfig.StopBits;
                    GlobalSerialPort.Open();
                }
                catch
                {
                    ShowAdminDialog();
                }
            }
            else
            {
                ShowAdminDialog();
            }
        }

        private void ShowAdminDialog()
        {
            var dlg = new AdminDialog();
            dlg.ShowDialog();
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

        private void InitializeSerialPort()
        {
            serialPort = new SerialPort("COM11", 9600, Parity.None, 8, StopBits.One);
            try
            {
                if (!serialPort.IsOpen)
                    serialPort.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"串口打开失败: {ex.Message}", "串口错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning && AllSwitchOn())
            {
                isRunning = true;
                dronePanel?.SetSpeed(currentSpeed);
                dronePanel?.StartPropeller();
                UpdateButtonState();
            }
            else if (!AllSwitchOn())
            {
                MessageBox.Show("请确保所有电源开关都已接通", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                isRunning = false;
                dronePanel?.StopPropeller();
                UpdateButtonState();
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning && AllSwitchOn())
            {
                dronePanel?.TestRotation();
            }
            else if (isRunning)
            {
                MessageBox.Show("请先停止电机运行再进行测试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("请确保所有电源开关都已接通", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currentSpeed = e.NewValue;
            dronePanel?.SetSpeed(currentSpeed);
        }
        
        private void UpdateButtonState()
        {
            bool powerEnabled = dronePanel?.AllSwitchOn ?? false;
            StartButton.IsEnabled = powerEnabled && !isRunning;
            StopButton.IsEnabled = powerEnabled && isRunning;
            TestButton.IsEnabled = powerEnabled && !isRunning;
        }

        private bool AllSwitchOn() => switchStates[0] && switchStates[1] && switchStates[2];

        protected override void OnClosed(EventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.Close();
                serialPort.Dispose();
            }
            base.OnClosed(e);
        }
    }
}