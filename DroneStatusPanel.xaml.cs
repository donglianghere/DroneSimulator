using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DroneSimulator
{
    public partial class DroneStatusPanel : UserControl
    {
        private Storyboard? rotationStoryboard;
        private Storyboard? testStoryboard;
        private DispatcherTimer? rpmTimer;
        private double currentSpeed = 0;
        private bool isRunning = false;
        private readonly Random random = new();

        private SerialPort? serialPort;
        private bool[] switchStates = new bool[3] { false, false, false };
        // 添加开关状态改变事件
        public event Action<int, bool>? SwitchStateChanged;
        public bool AllSwitchOn => switchStates[0] && switchStates[1] && switchStates[2];
        public DroneStatusPanel()
        {
            InitializeComponent();
            InitTimer();
        }

        private void InitTimer()
        {
            rpmTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            rpmTimer.Tick += RpmTimer_Tick;
        }

        // 设置状态文本和颜色
        public void SetStatus(string status, Color color)
        {
            StatusText.Text = status;
            StatusText.Foreground = new SolidColorBrush(color);
            StatusIndicator.Fill = new SolidColorBrush(color);
        }

        // 设置转速显示
        public void SetRpm(double rpm)
        {
            RpmDisplay.Text = $"转速: {rpm:F0} RPM";
        }

        // 设置开关和线路状态
        public void SetSwitchState(int index, bool isOn)
        {
            var btn = index switch
            {
                0 => Switch1,
                1 => Switch2,
                2 => Switch3,
                _ => null
            };
            var line = index switch
            {
                0 => PowerLine1,
                1 => PowerLine2,
                2 => PowerLine3,
                _ => null
            };
            if (btn != null && line != null)
            {
                btn.Background = new SolidColorBrush(isOn ? Colors.LimeGreen : Colors.Red);
                btn.Content = isOn ? "off" : "on";
                line.Stroke = new SolidColorBrush(isOn ? Colors.LimeGreen : Colors.Red);
            }
        }

        // 设置螺旋桨动画速度
        public void SetSpeed(double speed)
        {
            currentSpeed = speed;
            if (isRunning)
                CreateAndStartAnimation();
        }

        // 启动螺旋桨动画
        public void StartPropeller()
        {
            isRunning = true;
            StopTestAnimation();
            CreateAndStartAnimation();
            SetStatus("运行中", Colors.LimeGreen);
            StartRpmTimer();
        }

        // 停止螺旋桨动画
        public void StopPropeller()
        {
            isRunning = false;
            StopAllAnimations();
            PropellerRotation.Angle = 0;
            SetStatus("停止", Colors.Red);
            StopRpmTimer();
            SetRpm(0);
        }

        // 测试旋转动画
        public void TestRotation()
        {
            if (isRunning)
            {
                MessageBox.Show("请先停止电机运行再进行测试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            StopAllAnimations();
            testStoryboard = new Storyboard();
            var testAnimation = new DoubleAnimation
            {
                From = PropellerRotation.Angle,
                To = PropellerRotation.Angle + 1080,
                Duration = TimeSpan.FromSeconds(2),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(testAnimation, PropellerCanvas);
            Storyboard.SetTargetProperty(testAnimation, new PropertyPath("(Canvas.RenderTransform).(RotateTransform.Angle)"));

            testStoryboard.Children.Add(testAnimation);
            testStoryboard.Completed += (s, e) => { PropellerRotation.Angle %= 360; };
            testStoryboard.Begin();
            SetStatus("测试中", Colors.Orange);

            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            resetTimer.Tick += (s, e) =>
            {
                resetTimer.Stop();
                if (!isRunning)
                    SetStatus("停止", Colors.Red);
            };
            resetTimer.Start();
        }

        private void CreateAndStartAnimation()
        {
            if (rotationStoryboard != null)
            {
                rotationStoryboard.Stop();
                rotationStoryboard = null;
            }
            rotationStoryboard = new Storyboard();
            double minDuration = 0.02;
            double maxDuration = 1.0;
            double speedFactor = Math.Max(currentSpeed, 1) / 100.0;
            double duration = maxDuration - speedFactor * (maxDuration - minDuration);
            duration = Math.Max(duration, minDuration);

            var rotationAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(duration),
                RepeatBehavior = RepeatBehavior.Forever
            };

            Storyboard.SetTarget(rotationAnimation, PropellerCanvas);
            Storyboard.SetTargetProperty(rotationAnimation, new PropertyPath("(Canvas.RenderTransform).(RotateTransform.Angle)"));

            rotationStoryboard.Children.Add(rotationAnimation);
            rotationStoryboard.Begin();
        }

        private void StopAllAnimations()
        {
            rotationStoryboard?.Stop();
            rotationStoryboard = null;
            StopTestAnimation();
        }

        private void StopTestAnimation()
        {
            testStoryboard?.Stop();
            testStoryboard = null;
        }

        private void StartRpmTimer() => rpmTimer?.Start();
        private void StopRpmTimer() => rpmTimer?.Stop();

        private void RpmTimer_Tick(object? sender, EventArgs e)
        {
            if (isRunning)
            {
                double baseRpm = 8000;
                double currentRpm = baseRpm * (currentSpeed / 100.0);
                double fluctuation = (random.NextDouble() - 0.5) * 100;
                currentRpm += fluctuation;
                currentRpm = Math.Max(0, currentRpm);
                SetRpm(currentRpm);
            }
        }

        // 提供串口设置方法，供 MainWindow 初始化后调用
        public void SetSerialPort(SerialPort port)
        {
            serialPort = port;
        }

        // Switch_Click 事件处理
        public void Switch_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int switchIndex = -1;
            if (button == null) return;

            if (button.Name == "Switch1") switchIndex = 0;
            else if (button.Name == "Switch2") switchIndex = 1;
            else if (button.Name == "Switch3") switchIndex = 2;

            if (switchIndex != -1)
            {
                switchStates[switchIndex] = !switchStates[switchIndex];
                SetSwitchState(switchIndex, switchStates[switchIndex]);

                // 触发开关状态改变事件
                SwitchStateChanged?.Invoke(switchIndex, switchStates[switchIndex]);

                // 串口数据发送逻辑
                if (switchStates[switchIndex])
                {
                    string? sendData = switchIndex switch
                    {
                        0 => "FFFF00040100EEEE",
                        1 => "FFFF00080100EEEE",
                        2 => "FFFF00120100EEEE",
                        _ => null
                    };
                    if (!string.IsNullOrEmpty(sendData))
                    {
                        SendSerialData(sendData!);
                    }
                }
            }
        }

        private void SendSerialData(string data)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Write(data);
                }
                else
                {
                    MessageBox.Show("串口未打开，无法发送数据。", "串口错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"串口发送失败: {ex.Message}", "串口错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}