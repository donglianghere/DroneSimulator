using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DroneSimulator
{
    public partial class MainWindow : Window
    {
        private Storyboard rotationStoryboard;
        private Storyboard testStoryboard; // 添加测试动画的引用
        private DispatcherTimer rpmTimer;
        private bool isRunning = false;
        private double currentSpeed = 0;
        private Random random = new Random();

        public MainWindow()
        {
            InitializeComponent();
            this.RegisterName("PropellerRotation", PropellerRotation);
            InitializeAnimation();
            InitializeTimer();
        }

        private void InitializeAnimation()
        {
            // 不在这里创建Storyboard，而是在需要时动态创建
        }

        private void InitializeTimer()
        {
            // 创建RPM更新定时器
            rpmTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            rpmTimer.Tick += RpmTimer_Tick;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isRunning)
            {
                StartPropeller();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning)
            {
                StopPropeller();
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // 测试旋转 - 停止其他动画后执行
            TestRotation();
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            currentSpeed = e.NewValue;
            if (SpeedValue != null) // 添加null检查
            {
                SpeedValue.Text = $"{currentSpeed:F0}%";
            }

            if (isRunning)
            {
                UpdateAnimationSpeed();
            }
        }

        private void StartPropeller()
        {
            isRunning = true;

            // 停止测试动画（如果正在运行）
            StopTestAnimation();

            // 创建新的旋转动画
            CreateAndStartAnimation();

            // 更新UI状态
            StatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
            StatusText.Text = "运行中";
            StatusText.Foreground = new SolidColorBrush(Colors.LimeGreen);

            // 添加状态指示器闪烁效果
            var pulseAnimation = new DoubleAnimation(0.3, 1.0, TimeSpan.FromSeconds(0.5))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            StatusIndicator.BeginAnimation(OpacityProperty, pulseAnimation);

            // 启动RPM更新
            rpmTimer.Start();

            // 禁用启动按钮，启用停止按钮
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }

        private void StopPropeller()
        {
            isRunning = false;

            // 停止所有动画
            StopAllAnimations();

            // 重置旋转角度
            PropellerRotation.Angle = 0;

            // 更新UI状态
            StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            StatusText.Text = "停止";
            StatusText.Foreground = new SolidColorBrush(Colors.Red);

            // 停止闪烁效果
            StatusIndicator.BeginAnimation(OpacityProperty, null);
            StatusIndicator.Opacity = 1.0;

            // 停止RPM更新
            rpmTimer.Stop();
            RpmDisplay.Text = "转速: 0 RPM";

            // 启用启动按钮，禁用停止按钮
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void UpdateAnimationSpeed()
        {
            if (isRunning)
            {
                // 重新創建動畫以應用新的速度
                CreateAndStartAnimation();
            }
        }

        private void CreateAndStartAnimation()
        {
            // 停止现有动画
            if (rotationStoryboard != null)
            {
                rotationStoryboard.Stop(this);
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

            // 关键：用名称绑定目标
            Storyboard.SetTargetName(rotationAnimation, "PropellerRotation");
            Storyboard.SetTargetProperty(rotationAnimation, new PropertyPath("Angle"));

            rotationStoryboard.Children.Add(rotationAnimation);

            try
            {
                // 关键：指定宿主为 this
                rotationStoryboard.Begin(this, true);
                System.Diagnostics.Debug.WriteLine($"动画已启动，持续时间: {duration:F2}秒，速度: {currentSpeed}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"动画启动失败: {ex.Message}");
                MessageBox.Show($"动画启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RpmTimer_Tick(object sender, EventArgs e)
        {
            if (isRunning)
            {
                // 计算RPM（基于速度百分比）
                double baseRpm = 8000; // 最大RPM
                double currentRpm = baseRpm * (currentSpeed / 100.0);

                // 添加一些随机波动使其更真实
                double fluctuation = (random.NextDouble() - 0.5) * 100;
                currentRpm += fluctuation;
                currentRpm = Math.Max(0, currentRpm);

                RpmDisplay.Text = $"转速: {currentRpm:F0} RPM";
            }
        }

        //private Storyboard testStoryboard; // 添加为成员变量

        private void TestRotation()
        {
            try
            {
                StopAllAnimations();

                if (isRunning)
                {
                    MessageBox.Show("请先停止电机运行再进行测试", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                testStoryboard = new Storyboard();
                var testAnimation = new DoubleAnimation
                {
                    From = PropellerRotation.Angle,
                    To = PropellerRotation.Angle + 1080,
                    Duration = TimeSpan.FromSeconds(2),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };

                Storyboard.SetTargetName(testAnimation, "PropellerRotation");
                Storyboard.SetTargetProperty(testAnimation, new PropertyPath("Angle"));

                testStoryboard.Children.Add(testAnimation);

                testStoryboard.Completed += (s, e) =>
                {
                    PropellerRotation.Angle = PropellerRotation.Angle % 360;
                };

                // 关键：指定宿主为 this
                testStoryboard.Begin(this, true);

                StatusText.Text = "测试中";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);
                StatusIndicator.Fill = new SolidColorBrush(Colors.Orange);

                var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                resetTimer.Tick += (s, e) =>
                {
                    resetTimer.Stop();
                    if (!isRunning)
                    {
                        StatusText.Text = "停止";
                        StatusText.Foreground = new SolidColorBrush(Colors.Red);
                        StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    }
                };
                resetTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试旋转失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopAllAnimations()
        {
            // 停止主旋转动画
            if (rotationStoryboard != null)
            {
                rotationStoryboard.Stop(this);
                rotationStoryboard = null;
            }

            // 停止测试动画
            StopTestAnimation();
        }

        private void StopTestAnimation()
        {
            if (testStoryboard != null)
            {
                testStoryboard.Stop();
                testStoryboard = null;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 清理资源
            StopAllAnimations();
            rpmTimer?.Stop();
            base.OnClosed(e);
        }
    }
}