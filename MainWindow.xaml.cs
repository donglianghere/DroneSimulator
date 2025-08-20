using DroneSimulator;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DroneSimulator
{
    public partial class MainWindow : Window
    {
        public static SerialPortConfig SerialConfig = new SerialPortConfig();

        private UserInfo currentUser;
        private ExamData? latestExam;

        // 添加两个变量统计答题情况
        private int correctAnswers = 0;      // 正确答题数量
        private int wrongAnswers = 0;        // 误答题数量

        // 添加考试时间相关变量
        private DateTime examStartTime;      // 考试开始时间
        private DateTime examEndTime;        // 考试结束时间
        private bool examTimerHasStarted = false;
        private DispatcherTimer examTimer;  // 计时器，用于实时更新显示

        // 添加考试状态变量
        private bool isExamSubmitted = false;

        // 添加计算分数的方法
        private int CalculateExamScore()
        {
            int totalQuestions = latestExam?.Questions?.Count(q => q.IsChecked) ?? 0;
            if (totalQuestions == 0) return 0;

            // 简单的评分规则：
            // 正确答题每题得分 = 100 / 总题数
            // 误答题扣分 = (100 / 总题数) * 0.5
            int baseScore = 100;
            double scorePerQuestion = (double)baseScore / totalQuestions;

            double totalScore = (correctAnswers * scorePerQuestion) - (wrongAnswers * scorePerQuestion * 0.5);

            // 确保分数不低于0
            return Math.Max(0, (int)Math.Round(totalScore));
        }

        // 禁用所有修复按钮
        private void DisableAllRepairButtons()
        {
            // 处理第一个页面的按钮
            DisableCanvasButtons(ZoomCanvas);
            // 处理第二个页面的按钮
            DisableCanvasButtons(MotorCanvas);
            // 处理第三个页面的按钮(如果需要)
            DisableCanvasButtons(ExtensionCanvas);
        }

        private void DisableCanvasButtons(Canvas canvas)
        {
            if (canvas == null) return;

            foreach (var child in canvas.Children)
            {
                if (child is Button btn && btn.Content?.ToString() == "修  复")
                {
                    btn.IsEnabled = false;
                    btn.Background = new SolidColorBrush(Colors.Gray);
                }
            }
        }

        private bool IsSerialWriteSuccessful(string result, out string errorMessage, out string receivedData)
        {
            errorMessage = "";
            receivedData = "";

            try
            {
                using var doc = JsonDocument.Parse(result);
                var root = doc.RootElement;

                if (root.TryGetProperty("result", out var resultProp))
                {
                    if (resultProp.GetString() == "ok")
                    {
                        if (root.TryGetProperty("recv", out var recvProp))
                        {
                            receivedData = recvProp.GetString() ?? "";
                        }
                        return true;
                    }
                }
                else if (root.TryGetProperty("error", out var errorProp))
                {
                    errorMessage = errorProp.GetString() ?? "未知错误";
                }

                return false;
            }
            catch (JsonException)
            {
                errorMessage = "返回数据格式错误";
                return false;
            }
        }

        // 使用 System.IO.Ports 发送串口数据
        private string SendSerialData(string portName, int baudRate, Parity parity, StopBits stopBits, string data)
        {
            try
            {
                using var serialPort = new SerialPort(portName, baudRate, parity, 8, stopBits)
                {
                    ReadTimeout = 3000,  // 3秒读取超时
                    WriteTimeout = 3000  // 3秒写入超时
                };

                serialPort.Open();

                // 发送数据
                serialPort.Write(data);

                // 等待并读取响应
                Thread.Sleep(100); // 短暂等待设备响应

                string receivedData = "";
                if (serialPort.BytesToRead > 0)
                {
                    receivedData = serialPort.ReadExisting();
                }

                serialPort.Close();

                // 返回JSON格式的成功结果，保持与原有代码兼容
                return JsonSerializer.Serialize(new { result = "ok", recv = receivedData });
            }
            catch (Exception ex)
            {
                // 返回JSON格式的错误结果，保持与原有代码兼容
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        public MainWindow(UserInfo user)
        {
            InitializeComponent();
            // 注册所有ScrollViewer的滚轮事件
            MainScrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
            MotorScrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
            ExtensionScrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
            // 初始化计时器
            InitializeExamTimer();

            // 添加窗口加载完成事件
            this.Loaded += MainWindow_Loaded;

            // 添加TabControl选择变化事件
            MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

            this.Loaded += async (s, e) =>
            {
                await ShowStudentAndExamInfoAsync();
            };
            currentUser = user;
            // 你可以在这里根据 currentUser 做初始化
            // 修正：根据用户类型初始化相应界面
            if (user.Type == UserType.Student)
            {
                // 学生用户可以看到TabControl，但功能有限
                InitializeSerialPort();
            }
        }

        // 窗口加载完成后自动缩放当前TabItem的Canvas
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 延迟执行，确保UI完全渲染
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FitCanvasToView();
            }), DispatcherPriority.Loaded);
        }

        // TabControl选择变化时自动缩放新选中的Canvas
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == MainTabControl)
            {
                // 延迟执行，确保TabItem切换完成
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitCanvasToView();
                }), DispatcherPriority.Loaded);
            }
        }

        // 自动缩放Canvas到视窗大小并居中
        private void FitCanvasToView()
        {
            var selectedTabItem = MainTabControl.SelectedItem as TabItem;
            if (selectedTabItem == null) return;

            ScrollViewer? scrollViewer = null;
            Canvas? canvas = null;
            ScaleTransform? scaleTransform = null;

            // 根据选中的TabItem确定对应的ScrollViewer、Canvas和ScaleTransform
            if (selectedTabItem.Header.ToString() == "GPS～飞控～接收机")
            {
                scrollViewer = MainScrollViewer;
                canvas = ZoomCanvas;
                scaleTransform = CanvasScale;
            }
            else if (selectedTabItem.Header.ToString() == "电机～电调～飞控")
            {
                scrollViewer = MotorScrollViewer;
                canvas = MotorCanvas;
                scaleTransform = MotorCanvasScale;
            }
            else if (selectedTabItem.Header.ToString() == "扩展～飞控～电源")
            {
                scrollViewer = ExtensionScrollViewer;
                canvas = ExtensionCanvas;
                scaleTransform = ExtensionCanvasScale;
            }

            if (scrollViewer == null || canvas == null || scaleTransform == null) return;

            // 获取ScrollViewer的可视区域大小
            double viewportWidth = scrollViewer.ViewportWidth;
            double viewportHeight = scrollViewer.ViewportHeight;

            // 如果ViewportWidth/Height为0，使用ActualWidth/Height
            if (viewportWidth == 0) viewportWidth = scrollViewer.ActualWidth;
            if (viewportHeight == 0) viewportHeight = scrollViewer.ActualHeight;

            // 如果仍然为0，说明控件还没有完全渲染，退出
            if (viewportWidth <= 0 || viewportHeight <= 0) return;

            // 计算缩放比例，保持宽高比
            double scaleX = viewportWidth / canvas.Width;
            double scaleY = viewportHeight / canvas.Height;
            double scale = Math.Min(scaleX, scaleY) * 0.9; // 留10%边距

            // 确保缩放比例不小于0.1
            scale = Math.Max(0.1, scale);

            // 应用缩放
            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;

            // 重置ScrollViewer滚动位置到中心
            scrollViewer.ScrollToHorizontalOffset((scrollViewer.ExtentWidth - scrollViewer.ViewportWidth) / 2);
            scrollViewer.ScrollToVerticalOffset((scrollViewer.ExtentHeight - scrollViewer.ViewportHeight) / 2);
        }

        // 添加公共方法，允许手动调用缩放
        public void ResetCanvasScale()
        {
            FitCanvasToView();
        }

        // 修改现有的滚轮缩放方法，添加边界检查
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                ScrollViewer scrollViewer = sender as ScrollViewer;
                ScaleTransform scaleTransform = null;

                if (scrollViewer == MainScrollViewer)
                    scaleTransform = CanvasScale;
                else if (scrollViewer == MotorScrollViewer)
                    scaleTransform = MotorCanvasScale;
                else if (scrollViewer == ExtensionScrollViewer)
                    scaleTransform = ExtensionCanvasScale;

                if (scaleTransform != null)
                {
                    double zoom = e.Delta > 0 ? 0.1 : -0.1;
                    double newScaleX = Math.Max(0.1, Math.Min(5.0, scaleTransform.ScaleX + zoom));
                    double newScaleY = Math.Max(0.1, Math.Min(5.0, scaleTransform.ScaleY + zoom));

                    scaleTransform.ScaleX = newScaleX;
                    scaleTransform.ScaleY = newScaleY;
                    e.Handled = true;
                }
            }
        }

        // 初始化考试计时器
        private void InitializeExamTimer()
        {
            examTimer = new DispatcherTimer();
            examTimer.Interval = TimeSpan.FromSeconds(1); // 每秒更新一次
            examTimer.Tick += ExamTimer_Tick;
        }

        // 计时器事件处理
        private void ExamTimer_Tick(object sender, EventArgs e)
        {
            UpdateExamStats();
        }

        // 开始考试计时
        private void StartExamTimer()
        {
            examStartTime = DateTime.Now;
            examTimerHasStarted = true;
            examTimer.Start();
        }

        // 停止考试计时
        private void StopExamTimer()
        {
            if (examTimer.IsEnabled)
            {
                examEndTime = DateTime.Now;
                examTimer.Stop();
            }
        }

        // 获取已用考试时间
        private TimeSpan GetElapsedExamTime()
        {
            if (!examTimerHasStarted)
            {
                return TimeSpan.Zero;
            }

            if (examTimer.IsEnabled)
            {
                // 计时器仍在运行，返回当前时间差
                return DateTime.Now - examStartTime;
            }
            else
            {
                // 计时器已停止，返回停止时的时间差
                return examEndTime - examStartTime;
            }
        }

        private void InitializeSerialPort()
        {
            if (!File.Exists("config_serialport.json"))
            {
                ShowAdminDialog();
                if (!File.Exists("config_serialport.json"))
                {
                    MessageBox.Show("串口配置文件未找到，无法进行操作。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            try
            {
                var json = File.ReadAllText("config_serialport.json");
                SerialConfig = JsonSerializer.Deserialize<SerialPortConfig>(json) ?? new SerialPortConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化或打开串口失败: {ex.Message}", "串口错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ShowStudentAndExamInfoAsync()
        {
            // 显示学生信息
            StudentNameText.Text = $"考生姓名：{currentUser.Name}";
            StudentIdText.Text = $"身份证号：{currentUser.IdNumber}";

            // 查找最近试卷
            string examDir = "Exams";
            if (Directory.Exists(examDir))
            {
                var files = Directory.GetFiles(examDir, "*.json");
                if (files.Length > 0)
                {
                    var latestFile = files.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                    var json = await File.ReadAllTextAsync(latestFile);
                    latestExam = System.Text.Json.JsonSerializer.Deserialize<ExamData>(json);
                    if (latestExam != null)
                    {
                        ExamTitleText.Text = $"试题名称：{latestExam.ExamName}";
                        ExamTeacherText.Text = $"出题老师：{latestExam.TeacherName}";
                        int totalQuestions = latestExam.Questions?.Count(q => q.IsChecked) ?? 0;
                        ShowExamStats(totalQuestions, 0, 0, TimeSpan.Zero);

                        // 设备初始化
                        if (latestExam?.Questions != null)
                        {
                            var cfg = SerialConfig;
                            var allQuestions = latestExam.Questions.Where(q => !string.IsNullOrEmpty(q.CommandString)).ToList();
                            int total = allQuestions.Count;
                            int done = 0;
                            LoadingProgressBar.Visibility = Visibility.Visible;
                            LoadingProgressBar.Value = 0;

                            bool initializationSuccessful = true;
                            string errorMessage = "";

                            foreach (var question in allQuestions)
                            {
                                try
                                {
                                    string cmd = question.CommandString;

                                    // 对未选中试题，指令第10位（下标9）改为 '0'
                                    if (!question.IsChecked && !string.IsNullOrEmpty(cmd) && cmd.Length >= 10)
                                    {
                                        var sb = new StringBuilder(cmd);
                                        sb[9] = '0';
                                        cmd = sb.ToString();
                                    }

                                    // 在后台线程执行串口操作
                                    string result = await Task.Run(() =>
                                        SendSerialData(cfg.PortName, cfg.BaudRate, cfg.Parity, cfg.StopBits, cmd));

                                    // 在UI线程处理结果
                                    if (IsSerialWriteSuccessful(result, out string error, out string received))
                                    {
                                        done++;
                                        LoadingProgressBar.Value = (double)done / total * 100;
                                    }
                                    else
                                    {
                                        errorMessage = $"试题初始化失败：指令 {cmd} 未能成功发送。错误：{error}";
                                        initializationSuccessful = false;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errorMessage = $"串口发送异常：{ex.Message}";
                                    initializationSuccessful = false;
                                    break;
                                }
                            }

                            // 根据初始化结果设置UI状态
                            if (initializationSuccessful)
                            {
                                LoadingProgressText.Text = "试题加载完毕，设备初始化完成！";
                                ExamMachineText.Text = "考试设备：正常";
                                StartExamTimer();
                            }
                            else
                            {
                                LoadingProgressText.Text = "试题加载失败，设备初始化未完成！";
                                ExamMachineText.Text = "考试设备：异常！";
                                MessageBox.Show(errorMessage, "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("试卷文件可能损坏，请联系管理员！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("未找到试卷目录（Exams），请联系管理员！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    ExamTitleText.Text = "试题名称：";
                    ExamTeacherText.Text = "出题老师：";
                }
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

        // 填充数据示例
        private void ShowExamStats(int total, int correct, int wrong, TimeSpan duration)
        {
            var stats = new List<ExamStatItem>
            {
                new ExamStatItem { Name = "题目总数", Value = total.ToString() },
                new ExamStatItem { Name = "正确答题", Value = correct.ToString() },
                new ExamStatItem { Name = "误答题", Value = wrong.ToString() },
                new ExamStatItem { Name = "答题耗时", Value = duration.ToString(@"mm\:ss") }
            };
            ExamStatListView.ItemsSource = stats;
        }

        // 添加更新统计的方法
        private void UpdateExamStats()
        {
            int totalQuestions = latestExam?.Questions?.Count(q => q.IsChecked) ?? 0;
            var duration = GetElapsedExamTime();
            ShowExamStats(totalQuestions, correctAnswers, wrongAnswers, duration);
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 停止计时器
            StopExamTimer();
        }

        private void RepairButton_Click(object sender, RoutedEventArgs e)
        {
            // 如果考试已提交，禁止继续答题
            if (isExamSubmitted)
            {
                MessageBox.Show("考试已提交，无法继续答题！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is not Button btn || btn.Tag is not string questionName || latestExam == null) return;

            btn.IsEnabled = false;

            var question = latestExam.Questions?.Find(q => q.Name == questionName);

            if (question != null && question.IsChecked)
            {
                btn.Background = new SolidColorBrush(Colors.LightGreen);
                btn.Content = "已修复";

                // 增加正确答题计数
                correctAnswers++;

                if (!string.IsNullOrEmpty(question.CommandString))
                {
                    try
                    {
                        string cmd = question.CommandString;

                        if (!string.IsNullOrEmpty(cmd) && cmd.Length >= 9)
                        {
                            var sb = new StringBuilder(cmd);
                            sb[9] = '0'; // 第十个字符（下标9）改为'0'
                            cmd = sb.ToString();
                        }
                        else
                        {
                            MessageBox.Show("指令格式不正确，无法修复！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            btn.IsEnabled = true;
                            btn.Background = new SolidColorBrush(Colors.Goldenrod);
                            btn.Content = "修  复";
                            correctAnswers--;
                            return;
                        }

                        var cfg = SerialConfig;
                        string result = SendSerialData(cfg.PortName, cfg.BaudRate, cfg.Parity, cfg.StopBits, cmd);

                        if (IsSerialWriteSuccessful(result, out string error, out string received))
                        {
                            // 修复成功
                        }
                        else
                        {
                            MessageBox.Show($"修复指令发送失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            btn.IsEnabled = true;
                            btn.Background = new SolidColorBrush(Colors.Goldenrod);
                            btn.Content = "修  复";
                            correctAnswers--;
                            return;
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"端口发送异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        btn.IsEnabled = true;
                        btn.Background = new SolidColorBrush(Colors.Goldenrod);
                        btn.Content = "修  复";
                        correctAnswers--;
                        return;
                    }
                }
            }
            else
            {
                btn.Content = "误修复";
                btn.Background = new SolidColorBrush(Colors.IndianRed);
                wrongAnswers++;
                MessageBox.Show("请仔细检查，该连接不需要修复！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 更新统计显示
            UpdateExamStats();
        }

        // 提交与退出按钮事件
        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            // 防止重复提交
            if (isExamSubmitted)
            {
                MessageBox.Show("考试已经提交过了！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确认提交对话框
            var result = MessageBox.Show("确定要提交考试吗？提交后将无法继续答题。", "确认提交",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            // 停止计时
            StopExamTimer();

            // 设置考试已提交状态
            isExamSubmitted = true;

            // 计算并更新考试分数
            int finalScore = CalculateExamScore();
            ExamScoreText.Text = $"考试得分：{finalScore}分";

            // 禁用所有修复按钮
            DisableAllRepairButtons();

            // 禁用提交按钮自身
            SubmitButton.IsEnabled = false;
            SubmitButton.Content = "已提交";
            SubmitButton.Background = new SolidColorBrush(Colors.Gray);

            // 更新最终统计
            UpdateExamStats();

            // 显示提交成功消息
            var elapsedTime = GetElapsedExamTime();
            MessageBox.Show($"提交成功！\n" +
                           $"考试得分：{finalScore}分\n" +
                           $"正确答题：{correctAnswers}题\n" +
                           $"误答题：{wrongAnswers}题\n" +
                           $"答题耗时：{elapsedTime:hh\\:mm\\:ss}",
                           "考试结果", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止计时
            StopExamTimer();

            // 设置对话框结果并关闭
            this.DialogResult = false;
            this.Close();
        }
    }
}