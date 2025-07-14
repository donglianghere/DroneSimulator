using DroneSimulator;
using RJCP.IO.Ports;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DroneSimulator
{
    public class SerialPortConfig
    {
        public string PortName { get; set; } = "";
        public int BaudRate { get; set; } = 9600;
        // *** 关键改动：将枚举类型统一为新库的类型 ***
        public RJCP.IO.Ports.Parity Parity { get; set; } = RJCP.IO.Ports.Parity.None;
        public RJCP.IO.Ports.StopBits StopBits { get; set; } = RJCP.IO.Ports.StopBits.One;
    }

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
            // 遍历Canvas中的所有子元素，找到修复按钮并禁用
            foreach (var child in ZoomCanvas.Children)
            {
                if (child is Button btn && btn.Content?.ToString() == "修  复")
                {
                    btn.IsEnabled = false;
                    btn.Background = new SolidColorBrush(Colors.Gray);
                    btn.Content = "已提交";
                }
            }
        }

        // 发送串口数据的Python脚本
        private string SendSerialByPython(string port, int baudrate, string parity, double stopbits, string data)
        {
            string pythonExe = "python"; // 或指定绝对路径
            string script = "serial_bridge.py";
            string args = $"send {port} {baudrate} {parity} {stopbits} \"{data.Replace("\"", "\\\"")}\"";

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"{script} {args}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
                throw new Exception("Python错误: " + error);

            return output;
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

        public MainWindow(UserInfo user)
        {
            InitializeComponent();
            ZoomCanvas.MouseWheel += ZoomCanvas_MouseWheel;
            MainScrollViewer.SizeChanged += MainScrollViewer_SizeChanged;

            // 初始化计时器
            InitializeExamTimer();

            this.Loaded += async(s, e) =>
            {
                UpdateCanvasScale();
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

                        // 设备初始化（如需异步，建议用Task.Run包裹耗时操作）
                        if (latestExam?.Questions != null)
                        {
                            var cfg = SerialConfig;
                            string parity = cfg.Parity switch
                            {
                                RJCP.IO.Ports.Parity.None => "N",
                                RJCP.IO.Ports.Parity.Even => "E",
                                RJCP.IO.Ports.Parity.Odd => "O",
                                RJCP.IO.Ports.Parity.Mark => "M",
                                RJCP.IO.Ports.Parity.Space => "S",
                                _ => "N"
                            };
                            double stopbits = cfg.StopBits switch
                            {
                                RJCP.IO.Ports.StopBits.One5 => 1.5,
                                RJCP.IO.Ports.StopBits.One => 1,
                                RJCP.IO.Ports.StopBits.Two => 2,
                                _ => 1
                            };

                            var checkedQuestions = latestExam.Questions.Where(q => q.IsChecked && !string.IsNullOrEmpty(q.CommandString)).ToList();
                            int total = checkedQuestions.Count;
                            int done = 0;
                            LoadingProgressBar.Visibility = Visibility.Visible;
                            LoadingProgressBar.Value = 0;

                            bool initializationSuccessful = true;
                            string errorMessage = "";

                            foreach (var question in checkedQuestions)
                            {
                                try
                                {
                                    string cmd = question.CommandString;

                                    // 在后台线程执行串口操作
                                    string result = await Task.Run(() =>
                                        SendSerialByPython(cfg.PortName, cfg.BaudRate, parity, stopbits, cmd));

                                    // 在UI线程处理结果
                                    if (IsSerialWriteSuccessful(result, out string error, out string received))
                                    {
                                        done++;
                                        LoadingProgressBar.Value = (double)done / total * 100;
                                    }
                                    else
                                    {
                                        // 记录错误信息，准备退出
                                        errorMessage = $"试题初始化失败：指令 {cmd} 未能成功发送。错误：{error}";
                                        initializationSuccessful = false;
                                        break; // 退出foreach循环
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errorMessage = $"串口发送异常：{ex.Message}";
                                    initializationSuccessful = false;
                                    break; // 退出foreach循环
                                }
                            }

                            // 根据初始化结果设置UI状态
                            if (initializationSuccessful)
                            {
                                LoadingProgressText.Text = "试题加载完毕，设备初始化完成！";
                                ExamMachineText.Text = "考试设备：正常";

                                // 试题初始化完成，开始计时
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
        
        private void MainScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCanvasScale();
        }

        private void UpdateCanvasScale()
        {
            // 获取ScrollViewer可视区域大小
            double viewWidth = MainScrollViewer.ViewportWidth;
            double viewHeight = MainScrollViewer.ViewportHeight;

            // 如果ScrollViewer还没布局好，直接返回
            if (viewWidth <= 0 || viewHeight <= 0) return;

            // Canvas原始大小
            double canvasWidth = ZoomCanvas.Width;
            double canvasHeight = ZoomCanvas.Height;

            // 计算缩放比例（宽高都适应，取较小值，防止变形）
            double scaleX = viewWidth / canvasWidth;
            double scaleY = viewHeight / canvasHeight;
            double scale = Math.Min(scaleX, scaleY);

            // 设置缩放
            CanvasScale.ScaleX = scale;
            CanvasScale.ScaleY = scale;
        }

        private void ZoomCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 获取当前缩放
            var scale = (ScaleTransform)ZoomCanvas.LayoutTransform;
            double zoom = e.Delta > 0 ? 1.1 : 0.9;
            double newScaleX = scale.ScaleX * zoom;
            double newScaleY = scale.ScaleY * zoom;

            // 限制缩放范围
            if (newScaleX < 0.2) newScaleX = newScaleY = 0.2;
            if (newScaleX > 5.0) newScaleX = newScaleY = 5.0;

            scale.ScaleX = newScaleX;
            scale.ScaleY = newScaleY;
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

        // 定义数据结构
        public class ExamStatItem
        {
            public string Name { get; set; } = "";
            public string Value { get; set; } = "";
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
            var duration = GetElapsedExamTime(); // 这里可以根据需要计算实际答题时间
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

                            // 回退正确答题计数
                            correctAnswers--;
                            return;
                        }

                            // 读取串口配置
                            var cfg = SerialConfig;
                        // parity 转换为 N/E/O/M/S
                        string parity = cfg.Parity switch
                        {
                            RJCP.IO.Ports.Parity.None => "N",
                            RJCP.IO.Ports.Parity.Even => "E",
                            RJCP.IO.Ports.Parity.Odd => "O",
                            RJCP.IO.Ports.Parity.Mark => "M",
                            RJCP.IO.Ports.Parity.Space => "S",
                            _ => "N"
                        };
                        double stopbits = cfg.StopBits switch
                        {
                            RJCP.IO.Ports.StopBits.One5 => 1.5,
                            RJCP.IO.Ports.StopBits.One => 1,
                            RJCP.IO.Ports.StopBits.Two => 2,
                            _ => 1
                        };

                        string result = SendSerialByPython(cfg.PortName, cfg.BaudRate, parity, stopbits, cmd);
                        if (IsSerialWriteSuccessful(result, out string error, out string received))
                        {
                            ;// MessageBox.Show($"修复指令发送成功：{cmd}\n接收数据：{received}", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show($"修复指令发送失败：{error}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            btn.IsEnabled = true;
                            btn.Background = new SolidColorBrush(Colors.Goldenrod);
                            btn.Content = "修  复";

                            // 回退正确答题计数
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
                        // 回退正确答题计数
                        correctAnswers--;
                        return;
                    }
                }
            }
            else
            {
                btn.Content = "误修复";
                btn.Background = new SolidColorBrush(Colors.IndianRed);
                // 增加误答题计数
                wrongAnswers++;
                MessageBox.Show("请仔细检查，该连接不需要修复！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 更新统计显示
            UpdateExamStats();
        }

        // 提交与退出按钮事件
        // 修改提交按钮事件
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
            this.Close();
        }
    }
}