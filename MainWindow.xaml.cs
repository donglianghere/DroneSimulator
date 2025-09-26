using AutoPilot.Parameters;
using DroneSimulator;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DroneSimulator
{   

    public partial class MainWindow : Window
    {
        // 在 MainWindow 类中添加字段来跟踪题目状态
        private Dictionary<string, bool> _questionRepairStatus = new Dictionary<string, bool>();
        // ========== 新增：理论试卷和飞控实操相关字段 ==========
        private Dictionary<string, string> _theoryAnswers = new Dictionary<string, string>();
        private Dictionary<string, string> _fcAnswers = new Dictionary<string, string>();
        private List<TheoryQuestion> _theoryQuestions = new List<TheoryQuestion>();
        private List<FlightControlQuestion> _fcQuestions = new List<FlightControlQuestion>();

        // 在 MainWindow 类中添加新的字段来存储试卷分数配置
        private ExamScoreConfig _currentExamScoreConfig = new ExamScoreConfig();
        private MixedExamData _currentMixedExam;

        // ========== 使用 FcuOperate 提供的 ParameterService 重写电机测试 ==========
        private ParameterService? _fcService;

        // 电机测试初始化
        // 修改初始化方法支持取消令牌
        // 修改 InitFlightControllerAsync 方法
        private async Task<bool> InitFlightControllerAsync(CancellationToken cancellationToken = default)
        {
            if (_fcService != null && _fcService.IsConnected) return true;

            try
            {
                _fcService = new ParameterService();

                // 注册状态事件
                _fcService.StatusChanged += (s, e) =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (MotorTestStatusText != null)
                        {
                            MotorTestStatusText.Text = $"状态：{e.Message}";
                        }
                    });
                };

                // ========== 使用飞控通信端口而不是硬编码COM7 ==========
                var flightControllerConfig = SerialPortManager.GetConfigByPurpose(SerialPortPurpose.FlightController);

                if (flightControllerConfig != null && flightControllerConfig.IsEnabled)
                {
                    bool ok = await _fcService.ConnectAsync(flightControllerConfig.PortName, flightControllerConfig.BaudRate).ConfigureAwait(false);

                    if (ok)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            MotorTestStatusText.Text = $"状态：已连接到{flightControllerConfig.PortName} - 飞控通信";
                            MotorTestStatusText.Foreground = new SolidColorBrush(Colors.Green);
                        });
                    }

                    return ok;
                }
                else
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        MotorTestStatusText.Text = "状态：未配置飞控通信端口";
                        MotorTestStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    });
                    return false;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    MotorTestStatusText.Text = $"状态：初始化失败 - {ex.Message}";
                });
                return false;
            }
        }

        // 发送单个电机测试
        private async Task<bool> SendMotorTestAsync(byte motorNumber, int throttlePercent = 50, int durationSeconds = 5)
        {
            if (_fcService == null || !_fcService.IsConnected)
            {
                await InitFlightControllerAsync();
                if (_fcService == null || !_fcService.IsConnected)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MotorTestStatusText.Text = "状态：未连接飞控，无法测试";
                        MotorTestStatusText.Foreground = new SolidColorBrush(Colors.Crimson);
                    });
                    return false;
                }
            }

            // 可在此处提示确保已解锁（如需要自动解锁可调用 _fcService.ArmAsync(true)）
            return await _fcService.MotorTestAsync(motorNumber, throttlePercent, durationSeconds);
        }

        // 顺序测试全部电机
        private async Task RunAllMotorsSequentialAsync(int throttlePercent = 60, int durationSeconds = 5, int gapMs = 500)
        {
            for (byte m = 1; m <= 4; m++)
            {
                bool ok = await SendMotorTestAsync(m, throttlePercent, durationSeconds);
                if (!ok) break;
                await Task.Delay(gapMs);
            }
            Dispatcher.Invoke(() =>
            {
                MotorTestStatusText.Text += " | 全部完成";
            });
        }

        // 按钮事件——测试单个电机
        private async void MotorTestButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && byte.TryParse(btn.Tag?.ToString(), out byte motor))
            {
                btn.IsEnabled = false;

                try
                {
                    // 添加超时保护
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    // 先确保已连接
                    if (_fcService == null || !_fcService.IsConnected)
                    {
                        MotorTestStatusText.Text = "状态：正在连接...";
                        bool connected = await InitFlightControllerAsync(cts.Token);
                        if (!connected)
                        {
                            MotorTestStatusText.Text = "状态：连接失败";
                            return;
                        }
                    }

                    MotorTestStatusText.Text = $"状态：正在测试电机{motor}...";
                    bool ok = await _fcService.MotorTestAsync(motor, 35, 4).ConfigureAwait(false);
                    MotorTestStatusText.Text = ok ? $"状态：电机{motor}测试完成" : $"状态：电机{motor}测试失败";
                }
                catch (OperationCanceledException)
                {
                    MotorTestStatusText.Text = "状态：操作超时";
                }
                catch (Exception ex)
                {
                    MotorTestStatusText.Text = $"状态：错误 - {ex.Message}";
                }
                finally
                {
                    await Task.Delay(500);
                    btn.IsEnabled = true;
                }
            }
        }

        // 按钮事件——测试全部电机
        private async void MotorTestAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.IsEnabled = false;
                if (_fcService == null || !_fcService.IsConnected)
                    await InitFlightControllerAsync();

                // await _fcService.ArmAsync(true);

                for (byte m = 1; m <= 4; m++)
                {
                    await _fcService.MotorTestAsync(m, 35, 3);
                    await Task.Delay(700);
                }
                MotorTestStatusText.Text += " | 顺序测试完成";
                btn.IsEnabled = true;
            }
        }
        
        // ========== 新增：理论试卷事件处理程序 ==========
        /// <summary>
        /// 理论题选项点击事件
        /// </summary>
        private void TheoryOption_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is RadioButton radioButton && radioButton.Tag is TheoryOption option)
                {
                    // 处理单选题
                    var question = GetQuestionFromOption(option);
                    if (question != null)
                    {
                        // 记录学生答案
                        _theoryAnswers[question.Id] = option.Text;
                        System.Diagnostics.Debug.WriteLine($"单选题答案: 题目ID={question.Id}, 选择={option.Text}");
                    }
                }
                else if (sender is CheckBox checkBox && checkBox.Tag is TheoryOption option2)
                {
                    // 处理多选题
                    var question = GetQuestionFromOption(option2);
                    if (question != null)
                    {
                        // 获取当前题目的所有选中答案
                        var selectedOptions = GetSelectedOptionsForQuestion(question.Id);
                        _theoryAnswers[question.Id] = string.Join(";", selectedOptions);
                        System.Diagnostics.Debug.WriteLine($"多选题答案: 题目ID={question.Id}, 选择={_theoryAnswers[question.Id]}");
                    }
                }

                // 更新考试统计
                UpdateExamStats();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理理论题目选项点击失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从选项获取对应的题目
        /// </summary>
        private TheoryQuestion GetQuestionFromOption(TheoryOption option)
        {
            return _theoryQuestions?.FirstOrDefault(q => q.Options.Contains(option));
        }

        /// <summary>
        /// 获取指定题目的所有选中选项
        /// </summary>
        private List<string> GetSelectedOptionsForQuestion(string questionId)
        {
            var selectedOptions = new List<string>();

            // 遍历界面上的CheckBox控件，找到选中的选项
            var checkBoxes = new List<CheckBox>();
            FindVisualChildren<CheckBox>(TheoryQuestionsPanel, checkBoxes);

            foreach (var checkBox in checkBoxes)
            {
                if (checkBox.IsChecked == true && checkBox.Tag is TheoryOption option)
                {
                    var question = GetQuestionFromOption(option);
                    if (question?.Id == questionId)
                    {
                        selectedOptions.Add(option.Text);
                    }
                }
            }

            return selectedOptions;
        }
        // ========== 新增：飞控实操事件处理程序 ==========
        /// <summary>
        /// 连接飞控按钮点击事件
        /// </summary>
        private async void ConnectFC_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.IsEnabled = false;
                btn.Content = "连接中...";

                try
                {
                    FCConnectionStatusText.Text = "连接中...";
                    FCConnectionStatusText.Foreground = new SolidColorBrush(Colors.Orange);

                    bool connected = await InitFlightControllerAsync();

                    if (connected)
                    {
                        FCConnectionStatusText.Text = "已连接";
                        FCConnectionStatusText.Foreground = new SolidColorBrush(Colors.Green);
                        btn.Content = "重新连接";
                    }
                    else
                    {
                        FCConnectionStatusText.Text = "连接失败";
                        FCConnectionStatusText.Foreground = new SolidColorBrush(Colors.Red);
                        btn.Content = "连接飞控";
                    }
                }
                catch (Exception ex)
                {
                    FCConnectionStatusText.Text = "连接异常";
                    FCConnectionStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    btn.Content = "连接飞控";

                    MessageBox.Show($"连接飞控失败：{ex.Message}", "连接错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// 🚀 修改：飞控实操验证，提交后禁止操作
        /// </summary>
        private async void VerifyFCAnswer_Click(object sender, RoutedEventArgs e)
        {
            // 如果考试已提交，禁止继续答题
            if (isExamSubmitted)
            {
                MessageBox.Show("考试已提交，无法继续答题！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is Button btn && btn.Tag is FlightControlQuestion question)
            {
                btn.IsEnabled = false;
                btn.Content = "验证中...";

                try
                {
                    // 查找同一行的输入框
                    var parent = btn.Parent as StackPanel;
                    var inputStackPanel = parent?.Children.OfType<StackPanel>()
                        .FirstOrDefault(sp => sp.Children.OfType<TextBox>().Any());
                    var textBox = inputStackPanel?.Children.OfType<TextBox>().FirstOrDefault();

                    if (textBox == null)
                    {
                        MessageBox.Show("找不到参数输入框！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string studentAnswer = textBox.Text?.Trim() ?? "";

                    if (string.IsNullOrEmpty(studentAnswer))
                    {
                        MessageBox.Show("请先输入参数值！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 保存学生答案
                    _fcAnswers[question.Id] = studentAnswer;

                    // 验证答案
                    bool isCorrect = await ValidateFCAnswer(question, studentAnswer);

                    // 更新按钮状态
                    if (isCorrect)
                    {
                        btn.Background = new SolidColorBrush(Colors.LightGreen);
                        btn.Content = "验证通过";
                        textBox.IsEnabled = false; // 验证通过后禁用输入框
                    }
                    else
                    {
                        btn.Background = new SolidColorBrush(Colors.LightCoral);
                        btn.Content = "验证失败";
                    }

                    // 更新统计（不包含实时得分）
                    UpdateExamStats();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"验证飞控参数时发生错误：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);

                    btn.Background = new SolidColorBrush(Colors.LightCoral);
                    btn.Content = "验证失败";
                }
                finally
                {
                    btn.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// 🚀 新增：禁用所有理论题选项按钮
        /// </summary>
        private void DisableAllTheoryOptions()
        {
            try
            {
                var toggleButtons = new List<ToggleButton>();
                FindVisualChildren<ToggleButton>(TheoryQuestionsPanel, toggleButtons);

                foreach (var toggleButton in toggleButtons)
                {
                    toggleButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"禁用理论题选项失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 新增：禁用所有飞控实操输入框和验证按钮
        /// </summary>
        private void DisableAllFCControls()
        {
            try
            {
                var textBoxes = new List<TextBox>();
                var buttons = new List<Button>();

                FindVisualChildren<TextBox>(FCQuestionsPanel, textBoxes);
                FindVisualChildren<Button>(FCQuestionsPanel, buttons);

                foreach (var textBox in textBoxes)
                {
                    if (textBox.Name == "ParameterValueTextBox" || textBox.Tag != null)
                    {
                        textBox.IsEnabled = false;
                    }
                }

                foreach (var button in buttons)
                {
                    if (button.Content?.ToString() == "验证" || button.Content?.ToString() == "验证中..." ||
                        button.Content?.ToString() == "验证通过" || button.Content?.ToString() == "验证失败")
                    {
                        button.IsEnabled = false;
                        button.Background = new SolidColorBrush(Colors.Gray);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"禁用飞控控件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 修改：LoadMixedExamToUI中也调用隐藏零分值页面
        /// </summary>
        private async Task LoadMixedExamToUI(MixedExamData mixedExam)
        {
            try
            {
                Debug.WriteLine("开始加载混合试卷到UI...");

                // 🚀 新增：保存试卷分数配置
                _currentMixedExam = mixedExam;
                _currentExamScoreConfig = mixedExam.ScoreConfig ?? new ExamScoreConfig();

                // 🔧 修复：理论题目加载逻辑
                if (mixedExam.Content.TheoryQuestions.Any())
                {
                    try
                    {
                        Debug.WriteLine($"发现 {mixedExam.Content.TheoryQuestions.Count} 道理论题目");

                        // 🔧 修复：不再过滤IsSelected，显示所有理论题目
                        _theoryQuestions = mixedExam.Content.TheoryQuestions.ToList();

                        // 🔧 如果所有题目的IsSelected都是false，强制设置为true
                        if (_theoryQuestions.Any() && _theoryQuestions.All(q => !q.IsSelected))
                        {
                            Debug.WriteLine("所有理论题目的IsSelected都是false，强制设置为true");
                            foreach (var q in _theoryQuestions)
                            {
                                q.IsSelected = true;
                            }
                        }

                        Debug.WriteLine($"将要显示 {_theoryQuestions.Count} 道理论题目");

                        // 🔧 确保在UI线程上加载
                        await Dispatcher.InvokeAsync(() =>
                        {
                            LoadTheoryQuestionsToUI(_theoryQuestions);
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"加载理论题目失败：{ex.Message}");
                        MessageBox.Show($"理论题目加载失败：{ex.Message}", "警告",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Debug.WriteLine("⚠️ 混合试卷中没有理论题目");
                    _theoryQuestions = new List<TheoryQuestion>();
                    await Dispatcher.InvokeAsync(() => LoadTheoryQuestionsToUI(_theoryQuestions));
                }

                // 🔧 修复：安全加载飞控实操题目
                if (mixedExam.Content.FCQuestions.Any())
                {
                    try
                    {
                        Debug.WriteLine($"开始转换 {mixedExam.Content.FCQuestions.Count} 道飞控题目...");

                        _fcQuestions = mixedExam.Content.FCQuestions
                            .Select(fcq => new FlightControlQuestion
                            {
                                Id = fcq.Id ?? "",
                                QuestionStatement = fcq.QuestionStatement ?? "",
                                Type = Enum.TryParse<FCQuestionType>(fcq.Type, out var type) ? type : FCQuestionType.ParameterSetting,
                                Category = Enum.TryParse<FCQuestionCategory>(fcq.Category, out var category) ? category : FCQuestionCategory.BasicParameters,
                                Difficulty = Enum.TryParse<QuestionDifficulty>(fcq.Difficulty, out var difficulty) ? difficulty : QuestionDifficulty.Medium,
                                Points = fcq.Points,
                                ParameterName = fcq.ParameterName ?? "",
                                ParameterDescription = fcq.ParameterDescription ?? "",
                                DataType = Enum.TryParse<ParameterDataType>(fcq.DataType, out var dataType) ? dataType : ParameterDataType.Float,
                                CorrectValue = fcq.CorrectValue ?? "",
                                IsActive = fcq.IsActive,
                                CreatedBy = fcq.CreatedBy ?? "",
                                CreatedTime = fcq.CreatedTime,
                                RequireFlightControllerRead = true,
                                VerifyMethod = ParameterVerifyMethod.FloatTolerance,
                                IsSelected = true,
                                Explanation = $"请设置参数 {fcq.ParameterName} 的值"
                            }).ToList();

                        Debug.WriteLine($"飞控题目转换成功：{_fcQuestions.Count} 道");

                        // 🔧 重要：在UI线程上加载飞控题目
                        await Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                LoadFCQuestionsToUI(_fcQuestions);
                                Debug.WriteLine("飞控题目UI加载成功");
                            }
                            catch (Exception uiEx)
                            {
                                Debug.WriteLine($"飞控题目UI加载失败：{uiEx.Message}");
                                MessageBox.Show($"飞控题目界面加载失败：{uiEx.Message}", "UI错误",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"飞控题目转换失败：{ex.Message}");
                        Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");

                        // 创建一个空的飞控题目列表，避免崩溃
                        _fcQuestions = new List<FlightControlQuestion>();
                        await Dispatcher.InvokeAsync(() => LoadFCQuestionsToUI(_fcQuestions));
                    }
                }
                else
                {
                    // 没有飞控题目时，创建空列表
                    _fcQuestions = new List<FlightControlQuestion>();
                    await Dispatcher.InvokeAsync(() => LoadFCQuestionsToUI(_fcQuestions));
                }

                // 🚀 新增：在UI线程上调用隐藏零分值页面
                await Dispatcher.InvokeAsync(() =>
                {
                    HideZeroScoreTabPages();
                });

                Debug.WriteLine($"混合试卷加载完成：理论题 {_theoryQuestions.Count} 道，飞控题 {_fcQuestions.Count} 道");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载混合试卷UI失败：{ex.Message}");
                Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");

                MessageBox.Show($"加载混合试卷UI失败：{ex.Message}\n\n程序将继续运行，但飞控实操功能可能不可用。", "加载错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 🚀 新增：调试方法，显示当前分数配置
        /// </summary>
        private void DebugScoreConfiguration()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 当前分数配置调试信息 ===");
                if (_currentExamScoreConfig != null)
                {
                    System.Diagnostics.Debug.WriteLine($"总分: {_currentExamScoreConfig.TotalScore}");
                    System.Diagnostics.Debug.WriteLine($"理论题: {_currentExamScoreConfig.TheoryPercentage}% ({_currentExamScoreConfig.TheoryScore}分)");
                    System.Diagnostics.Debug.WriteLine($"电路题: {_currentExamScoreConfig.CircuitPercentage}% ({_currentExamScoreConfig.CircuitScore}分)");
                    System.Diagnostics.Debug.WriteLine($"飞控题: {_currentExamScoreConfig.FCPercentage}% ({_currentExamScoreConfig.FCScore}分)");
                    System.Diagnostics.Debug.WriteLine($"配置是否有效: {_currentExamScoreConfig.IsValid}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("分数配置为null，使用默认配置");
                }
                System.Diagnostics.Debug.WriteLine("================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调试分数配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 新增：根据分数配置隐藏分值为零的标签页
        /// </summary>
        private void HideZeroScoreTabPages()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 开始检查并隐藏零分值页面 ===");

                if (_currentExamScoreConfig == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ 分数配置为空，使用默认显示");
                    return;
                }

                // 检查理论题标签页
                if (_currentExamScoreConfig.TheoryScore == 0 || _currentExamScoreConfig.TheoryPercentage == 0)
                {
                    HideTabItemByHeader("理论题");
                    System.Diagnostics.Debug.WriteLine($"✅ 理论题标签页已隐藏 (分值: {_currentExamScoreConfig.TheoryScore}分, 占比: {_currentExamScoreConfig.TheoryPercentage}%)");
                }

                // 检查电路检测标签页
                if (_currentExamScoreConfig.CircuitScore == 0 || _currentExamScoreConfig.CircuitPercentage == 0)
                {
                    HideTabItemByHeader("电路检测");
                    System.Diagnostics.Debug.WriteLine($"✅ 电路检测标签页已隐藏 (分值: {_currentExamScoreConfig.CircuitScore}分, 占比: {_currentExamScoreConfig.CircuitPercentage}%)");
                }

                // 检查飞控实操标签页
                if (_currentExamScoreConfig.FCScore == 0 || _currentExamScoreConfig.FCPercentage == 0)
                {
                    HideTabItemByHeader("飞控实操");
                    System.Diagnostics.Debug.WriteLine($"✅ 飞控实操标签页已隐藏 (分值: {_currentExamScoreConfig.FCScore}分, 占比: {_currentExamScoreConfig.FCPercentage}%)");
                }

                System.Diagnostics.Debug.WriteLine("=== 零分值页面隐藏检查完成 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"隐藏零分值页面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 新增：根据标题隐藏标签页的辅助方法
        /// </summary>
        private void HideTabItemByHeader(string headerText)
        {
            try
            {
                // 查找主标签控件
                var mainTabControl = FindName("MainTabControl") as TabControl;
                if (mainTabControl == null)
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ 未找到MainTabControl");
                    return;
                }

                // 查找匹配的标签页
                TabItem? targetTab = null;
                foreach (TabItem tabItem in mainTabControl.Items)
                {
                    string tabHeader = tabItem.Header?.ToString() ?? "";

                    // 检查多种可能的标题匹配
                    if (tabHeader.Contains(headerText) ||
                        (headerText == "理论题" && tabHeader.Contains("理论")) ||
                        (headerText == "电路检测" && (tabHeader.Contains("电路") || tabHeader.Contains("检测"))) ||
                        (headerText == "飞控实操" && (tabHeader.Contains("飞控") || tabHeader.Contains("实操"))))
                    {
                        targetTab = tabItem;
                        break;
                    }
                }

                if (targetTab != null)
                {
                    targetTab.Visibility = Visibility.Collapsed;
                    System.Diagnostics.Debug.WriteLine($"✅ 成功隐藏标签页: {targetTab.Header}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 未找到匹配的标签页: {headerText}");

                    // 调试：列出所有可用的标签页
                    System.Diagnostics.Debug.WriteLine("📋 所有可用标签页:");
                    foreach (TabItem tabItem in mainTabControl.Items)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {tabItem.Header}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"隐藏标签页失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 新增：显示所有标签页（重置用）
        /// </summary>
        private void ShowAllTabPages()
        {
            try
            {
                var mainTabControl = FindName("MainTabControl") as TabControl;
                if (mainTabControl == null) return;

                foreach (TabItem tabItem in mainTabControl.Items)
                {
                    tabItem.Visibility = Visibility.Visible;
                }

                System.Diagnostics.Debug.WriteLine("✅ 所有标签页已显示");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示所有标签页失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 新增：计算理论试卷得分
        /// </summary>
        private double CalculateTheoryScore()
        {
            try
            {
                if (_theoryQuestions == null || !_theoryQuestions.Any())
                    return 0.0;

                double totalScore = 0.0;
                double maxPossibleScore = 0.0;

                foreach (var question in _theoryQuestions)
                {
                    maxPossibleScore += question.Points;

                    // 检查学生是否作答
                    if (!_theoryAnswers.ContainsKey(question.Id) || string.IsNullOrWhiteSpace(_theoryAnswers[question.Id]))
                    {
                        // 未作答，得0分
                        continue;
                    }

                    string studentAnswer = _theoryAnswers[question.Id];
                    List<string> studentSelectedOptions = studentAnswer.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                    // 判断答案是否正确
                    bool isCorrect = IsTheoryAnswerCorrect(question, studentSelectedOptions);

                    if (isCorrect)
                    {
                        totalScore += question.Points;
                        System.Diagnostics.Debug.WriteLine($"题目 {question.Id} 答对了，得分：{question.Points}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"题目 {question.Id} 答错了，得分：0");
                    }
                }

                // 计算得分率
                double scoreRate = maxPossibleScore > 0 ? totalScore / maxPossibleScore : 0.0;

                System.Diagnostics.Debug.WriteLine($"理论试卷原始得分：{totalScore}/{maxPossibleScore}，得分率：{scoreRate:P2}");

                return scoreRate;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算理论试卷得分失败：{ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// 🚀 新增：判断理论题答案是否正确
        /// </summary>
        private bool IsTheoryAnswerCorrect(TheoryQuestion question, List<string> studentSelectedOptions)
        {
            try
            {
                // 获取正确答案列表
                var correctAnswers = question.CorrectAnswers ?? new List<string>();

                if (question.Type == TheoryQuestionType.SingleChoice)
                {
                    // 单选题：只能选择一个选项，且必须正确
                    if (studentSelectedOptions.Count != 1)
                        return false;

                    return correctAnswers.Contains(studentSelectedOptions[0]);
                }
                else if (question.Type == TheoryQuestionType.MultipleChoice)
                {
                    // 多选题：选择的选项必须与正确答案完全匹配
                    if (studentSelectedOptions.Count != correctAnswers.Count)
                        return false;

                    // 检查每个选中的选项是否都在正确答案中
                    foreach (var selected in studentSelectedOptions)
                    {
                        if (!correctAnswers.Contains(selected))
                            return false;
                    }

                    // 检查每个正确答案是否都被选中
                    foreach (var correct in correctAnswers)
                    {
                        if (!studentSelectedOptions.Contains(correct))
                            return false;
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"判断理论题答案失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 🚀 修改：电路题数量只计算被勾选的题目
        /// </summary>
        private double CalculateCircuitScore()
        {
            try
            {
                // 🔧 修改：只计算被勾选的电路题数量
                int circuitTotal = latestExam?.Questions?.Count(q => q.IsChecked && !string.IsNullOrEmpty(q.CommandString)) ?? 0;

                if (circuitTotal == 0)
                    return 0.0;

                // 电路题得分率 = 正确修复数 / 被勾选的题数
                double scoreRate = (double)correctAnswers / circuitTotal;

                System.Diagnostics.Debug.WriteLine($"电路检测得分：{correctAnswers}/{circuitTotal}，得分率：{scoreRate:P2}");

                return Math.Max(0.0, scoreRate); // 确保得分率不为负数
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算电路检测得分失败：{ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// 🚀 修改：根据分值配置计算总分，使用扣分机制
        /// </summary>
        private int CalculateExamScore()
        {
            // 🔧 修改：只有在考试提交后才计算分数
            if (!isExamSubmitted)
            {
                return 0; // 考试未提交时返回0分，不显示实时得分
            }

            try
            {
                // 计算理论试卷得分率
                double theoryScoreRate = CalculateTheoryScore();

                // 🚀 重要修改：使用包含扣分机制的电路检测得分计算
                double circuitScoreRate = CalculateCircuitScoreWithPenalty();

                // 根据分值配置计算各部分得分
                double theoryScore = _currentExamScoreConfig.TheoryScore * theoryScoreRate;
                double circuitScore = _currentExamScoreConfig.CircuitScore * circuitScoreRate;

                // 🚀 暂时不计算飞控实操分数
                double fcScore = 0.0; // _currentExamScoreConfig.FCScore * fcScoreRate;

                // 计算总分
                double totalScore = theoryScore + circuitScore + fcScore;

                // 更新分数显示
                var theoryScoreInt = (int)Math.Round(theoryScore);
                var circuitScoreInt = (int)Math.Round(circuitScore);
                var fcScoreInt = (int)Math.Round(fcScore);
                var totalScoreInt = (int)Math.Round(totalScore);

                System.Diagnostics.Debug.WriteLine($"=== 考试得分详情 ===");
                System.Diagnostics.Debug.WriteLine($"理论题得分：{theoryScoreInt}/{_currentExamScoreConfig.TheoryScore}分 (得分率：{theoryScoreRate:P2})");
                System.Diagnostics.Debug.WriteLine($"电路题得分：{circuitScoreInt}/{_currentExamScoreConfig.CircuitScore}分 (得分率：{circuitScoreRate:P2})");
                System.Diagnostics.Debug.WriteLine($"飞控题得分：{fcScoreInt}/{_currentExamScoreConfig.FCScore}分 (暂不计分)");
                System.Diagnostics.Debug.WriteLine($"考试总分：{totalScoreInt}/{_currentExamScoreConfig.TotalScore}分");

                // 更新UI显示
                Dispatcher.Invoke(() =>
                {
                    ExamScoreText.Text = $"考试得分：总分为 {totalScoreInt} 分 \n" +
                                         $"  理论卷:{theoryScoreInt}分\n  电路卷:{circuitScoreInt}分 \n  飞控卷:{fcScoreInt}分";
                });

                return totalScoreInt;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算考试得分失败：{ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 🚀 修改：计算电路检测得分，只统计被勾选的题目，误修复不扣分机制
        /// </summary>
        private double CalculateCircuitScoreWithPenalty()
        {
            try
            {
                // 🔧 修改：只计算被勾选的电路题数量
                int circuitTotal = latestExam?.Questions?.Count(q => q.IsChecked && !string.IsNullOrEmpty(q.CommandString)) ?? 0;

                if (circuitTotal == 0)
                    return 0.0;

                int correctRepairs = 0;  // 正确修复数
                int wrongRepairs = 0;    // 误修复数
                int missedRepairs = 0;   // 漏修复数

                // 🔧 修改：统计各种修复情况 - 只处理被勾选的题目
                if (latestExam?.Questions != null)
                {
                    foreach (var question in latestExam.Questions.Where(q => q.IsChecked && !string.IsNullOrEmpty(q.CommandString)))
                    {
                        if (_questionRepairStatus.ContainsKey(question.Name))
                        {
                            bool studentRepaired = _questionRepairStatus[question.Name];

                            // 由于这里只处理被勾选的题目，所以都是需要修复的
                            if (studentRepaired)
                            {
                                correctRepairs++; // 正确修复
                            }
                            else
                            {
                                // 这种情况不应该发生，因为 _questionRepairStatus 记录的是点击了按钮的情况
                                System.Diagnostics.Debug.WriteLine($"⚠️ 异常状态：{question.Name} 标记为需要修复但记录为未正确修复");
                            }
                        }
                        else
                        {
                            // 学生没有点击按钮 - 漏修复
                            missedRepairs++; // 漏修复（应该修复但没有修复）
                        }
                    }

                    // 🔧 新增：单独统计误修复的题目（未勾选但被点击的题目）
                    foreach (var question in latestExam.Questions.Where(q => !q.IsChecked && !string.IsNullOrEmpty(q.CommandString)))
                    {
                        if (_questionRepairStatus.ContainsKey(question.Name))
                        {
                            bool studentRepaired = _questionRepairStatus[question.Name];
                            if (!studentRepaired) // studentRepaired=false 表示误修复
                            {
                                wrongRepairs++; // 误修复
                            }
                        }
                    }
                }

                // 🚀 新的计分规则：
                // - 正确修复：+1分
                // - 误修复：-0.5分  
                // - 漏修复：0分（不得分但也不扣分）
                double rawScore = correctRepairs - (wrongRepairs * 0.5);

                // 🚀 确保得分不低于0分
                double adjustedScore = Math.Max(0.0, rawScore);

                // 计算得分率，但不能超过100%
                double scoreRate = Math.Min(1.0, adjustedScore / circuitTotal);

                // 📊 详细的调试信息
                System.Diagnostics.Debug.WriteLine($"======== 电路检测详细计分统计（修改版）========");
                System.Diagnostics.Debug.WriteLine($"  📋 被勾选的题数：{circuitTotal}");
                System.Diagnostics.Debug.WriteLine($"  ✅ 正确修复：{correctRepairs} 题 (+{correctRepairs} 分)");
                System.Diagnostics.Debug.WriteLine($"  ❌ 误修复：{wrongRepairs} 题 (-{wrongRepairs * 0.5} 分)");
                System.Diagnostics.Debug.WriteLine($"  ⏸️ 漏修复：{missedRepairs} 题 (0 分)");
                System.Diagnostics.Debug.WriteLine($"  📊 原始得分：{rawScore} 分");
                System.Diagnostics.Debug.WriteLine($"  ⬆️ 调整后得分：{adjustedScore} 分 (不低于0分)");
                System.Diagnostics.Debug.WriteLine($"  📈 最终得分率：{scoreRate:P2}");
                System.Diagnostics.Debug.WriteLine($"==========================================");

                return scoreRate;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算电路检测得分失败：{ex.Message}");
                return 0.0;
            }
        }

        /// <summary>
        /// 🚀 修改：UpdateExamStats方法，只统计被勾选的电路题
        /// </summary>
        private void UpdateExamStats()
        {
            try
            {
                // 🔧 修改：只统计被勾选的电路题数量
                int circuitTotal = latestExam?.Questions?.Count(q => q.IsChecked && !string.IsNullOrEmpty(q.CommandString)) ?? 0;
                int circuitAnswered = _questionRepairStatus.Count(kvp =>
                {
                    var question = latestExam?.Questions?.FirstOrDefault(q => q.Name == kvp.Key);
                    return question != null && question.IsChecked && !string.IsNullOrEmpty(question.CommandString);
                }); // 只计算被勾选且已操作的题目数

                // 📊 详细统计各种修复情况
                int correctRepairs = 0;
                int wrongRepairs = 0;
                int missedRepairs = 0;

                if (latestExam?.Questions != null)
                {
                    // 统计被勾选题目的修复情况
                    foreach (var question in latestExam.Questions.Where(q => q.IsChecked && !string.IsNullOrEmpty(q.CommandString)))
                    {
                        if (_questionRepairStatus.ContainsKey(question.Name))
                        {
                            bool repairResult = _questionRepairStatus[question.Name];
                            if (repairResult)
                            {
                                correctRepairs++; // 正确修复
                            }
                        }
                        else
                        {
                            missedRepairs++; // 漏修复
                        }
                    }

                    // 统计未勾选题目的误修复情况
                    foreach (var question in latestExam.Questions.Where(q => !q.IsChecked && !string.IsNullOrEmpty(q.CommandString)))
                    {
                        if (_questionRepairStatus.ContainsKey(question.Name))
                        {
                            bool repairResult = _questionRepairStatus[question.Name];
                            if (!repairResult)
                            {
                                wrongRepairs++; // 误修复
                            }
                        }
                    }
                }

                // 理论题统计
                int theoryTotal = _theoryQuestions?.Count ?? 0;
                int theoryAnswered = _theoryAnswers.Count;

                // 飞控实操题统计
                int fcTotal = _fcQuestions?.Count ?? 0;
                int fcAnswered = _fcAnswers.Count;

                var duration = GetElapsedExamTime();

                // 🚀 修改：只有在提交后才显示得分
                string currentScoreDisplay = isExamSubmitted ? $"{CalculateExamScore()}分" : "提交后显示";

                // 🚀 增强的统计信息，包含详细的修复情况
                var stats = new List<ExamStatItem>
        {
            // 题目和分值配置信息
            new ExamStatItem { Name = "试卷总分", Value = $"{_currentExamScoreConfig.TotalScore}分" },
            new ExamStatItem { Name = "理论题权重", Value = $"{_currentExamScoreConfig.TheoryPercentage:F0}%({_currentExamScoreConfig.TheoryScore}分)" },
            new ExamStatItem { Name = "电路题权重", Value = $"{_currentExamScoreConfig.CircuitPercentage:F0}%({_currentExamScoreConfig.CircuitScore}分)" },
            new ExamStatItem { Name = "飞控题权重", Value = $"{_currentExamScoreConfig.FCPercentage:F0}%({_currentExamScoreConfig.FCScore}分)" },
            
            // 题目数量统计
            new ExamStatItem { Name = "理论题", Value = $"{theoryTotal}题" },
            new ExamStatItem { Name = "电路检测", Value = $"{circuitTotal}题 (勾选)" }, // 🔧 明确标示只显示勾选的题目
            new ExamStatItem { Name = "飞控实操", Value = $"{fcTotal}题" },
            new ExamStatItem { Name = "题目总数", Value = $"{circuitTotal + theoryTotal + fcTotal}题" },
            
            // 完成情况统计（增强版）
            new ExamStatItem { Name = "理论已答", Value = $"{theoryAnswered}/{theoryTotal}" },
            new ExamStatItem { Name = "电路已操作", Value = $"{circuitAnswered}/{circuitTotal}" },
            new ExamStatItem { Name = "正确修复", Value = $"{correctRepairs}题" },
            new ExamStatItem { Name = "误修复", Value = $"{wrongRepairs}题" },
            new ExamStatItem { Name = "漏修复", Value = $"{missedRepairs}题" },
            new ExamStatItem { Name = "飞控已验", Value = $"{fcAnswered}/{fcTotal}" },
            
            // 分数显示
            new ExamStatItem { Name = "当前总分", Value = currentScoreDisplay },
            
            // 时间统计
            new ExamStatItem { Name = "答题耗时", Value = duration.ToString(@"hh\:mm\:ss") }
        };

                ExamStatListView.ItemsSource = stats;

                // 更新分值配置显示
                UpdateScoreConfigurationDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新考试统计失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 🔧 修改：ShowStudentAndExamInfoAsync中调用隐藏零分值页面
        /// </summary>
        private async Task ShowStudentAndExamInfoAsync()
        {
            // 显示学生信息
            StudentNameText.Text = $"考生姓名：{currentUser.Name}";
            StudentIdText.Text = $"身份证号：{currentUser.IdNumber}";

            // 🚀 修正：支持混合试卷加载
            string examFile = QuestionPanel.GetActiveExamForStudent();

            if (!string.IsNullOrEmpty(examFile) && File.Exists(examFile))
            {
                try
                {
                    // 🚀 判断是否为混合试卷格式
                    if (examFile.EndsWith("_mixed.json"))
                    {
                        // 加载混合试卷
                        var json = await File.ReadAllTextAsync(examFile);
                        var mixedExam = JsonSerializer.Deserialize<MixedExamData>(json);

                        if (mixedExam != null)
                        {
                            // 🚀 新增：保存分数配置
                            _currentMixedExam = mixedExam;
                            _currentExamScoreConfig = mixedExam.ScoreConfig ?? new ExamScoreConfig();

                            // 转换为兼容的ExamData格式
                            latestExam = new ExamData
                            {
                                ExamName = mixedExam.ExamName,
                                TeacherName = mixedExam.TeacherName,
                                TeacherId = mixedExam.TeacherId,
                                CreationTime = mixedExam.CreationTime,
                                Questions = mixedExam.Content.GetAllQuestionsAsGeneric()
                            };

                            // 🚀 加载各类型题目到UI
                            await LoadMixedExamToUI(mixedExam);

                            // 🚀 新增：根据分数配置隐藏零分值页面
                            HideZeroScoreTabPages();
                        }
                    }
                    else
                    {
                        // 加载传统格式试卷 - 使用默认分数配置
                        var json = await File.ReadAllTextAsync(examFile);
                        latestExam = JsonSerializer.Deserialize<ExamData>(json);
                        _currentExamScoreConfig = new ExamScoreConfig(); // 使用默认配置

                        // 传统格式试卷通常只有电路题，隐藏其他页面
                        HideTabItemByHeader("理论题");
                        HideTabItemByHeader("飞控实操");
                    }

                    if (latestExam != null)
                    {
                        ExamTitleText.Text = $"试题名称：{latestExam.ExamName}";
                        ExamTeacherText.Text = $"出题老师：{latestExam.TeacherName}";

                        // 🚀 更新分值配置显示
                        UpdateScoreConfigurationDisplay();

                        // 🔧 修改：只统计被勾选且有CommandString的题目（电路实测题）
                        int totalQuestions = latestExam.Questions?.Count(q =>
                            q.IsChecked && !string.IsNullOrEmpty(q.CommandString)) ?? 0;

                        ShowExamStats(totalQuestions, 0, 0, TimeSpan.Zero);

                        // 🚀 调用独立的电路题目初始化方法
                        await InitializeCircuitQuestions();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载试卷失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("未找到试卷目录（Exams），请联系管理员！", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                ExamTitleText.Text = "试题名称：";
                ExamTeacherText.Text = "出题老师：";

                // 🚀 显示默认分值配置
                UpdateScoreConfigurationDisplay();
            }
        }

        /// <summary>
        /// 🚀 新增：更新分值配置信息显示
        /// </summary>
        private void UpdateScoreConfigurationDisplay()
        {
            try
            {
                if (_currentExamScoreConfig != null)
                {
                    // 更新总分显示
                    ExamTotalScoreText.Text = $"试卷总分：{_currentExamScoreConfig.TotalScore}分";

                    // 更新各类题目分值显示
                    TheoryScoreConfigText.Text = $"📚 理论题：{_currentExamScoreConfig.TheoryPercentage:F0}%({_currentExamScoreConfig.TheoryScore}分)";
                    CircuitScoreConfigText.Text = $"🔧 电路题：{_currentExamScoreConfig.CircuitPercentage:F0}%({_currentExamScoreConfig.CircuitScore}分)";
                    FCScoreConfigText.Text = $"🎮 飞控题：{_currentExamScoreConfig.FCPercentage:F0}%({_currentExamScoreConfig.FCScore}分)";

                    // 更新配置状态
                    if (_currentExamScoreConfig.IsValid)
                    {
                        ScoreConfigStatusText.Text = "✅ 配置有效";
                        ScoreConfigStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    else
                    {
                        ScoreConfigStatusText.Text = "⚠️ 配置异常";
                        ScoreConfigStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                }
                else
                {
                    // 使用默认配置显示
                    ExamTotalScoreText.Text = "试卷总分：100分 (默认)";
                    TheoryScoreConfigText.Text = "📚 理论题：40%(40分)";
                    CircuitScoreConfigText.Text = "🔧 电路题：40%(40分)";
                    FCScoreConfigText.Text = "🎮 飞控题：20%(20分)";
                    ScoreConfigStatusText.Text = "📋 默认配置";
                    ScoreConfigStatusText.Foreground = new SolidColorBrush(Colors.Blue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新分值配置显示失败：{ex.Message}");
            }
        }
        /// <summary>
        /// 加载理论题目到UI - 修改版本支持从混合试卷加载
        /// </summary>
        private void LoadTheoryQuestionsToUI(List<TheoryQuestion> questions)
        {
            try
            {
                Debug.WriteLine($"=== 开始加载理论题目到UI ===");
                Debug.WriteLine($"收到 {questions?.Count ?? 0} 道理论题目");

                if (questions == null || !questions.Any())
                {
                    Debug.WriteLine("⚠️ 理论题目列表为空，创建测试题目");
                    var testQuestions = CreateTestTheoryQuestions();
                    questions = testQuestions;
                    Debug.WriteLine($"创建了 {testQuestions.Count} 道测试题目");
                }

                // 🚀 为题目设置序号
                for (int i = 0; i < questions.Count; i++)
                {
                    questions[i].QuestionNumber = i + 1;
                    Debug.WriteLine($"设置题目序号：第{i + 1}题 - {questions[i].Id}");
                }

                // 为每个题目的选项设置编号（A、B、C、D等）
                foreach (var question in questions)
                {
                    Debug.WriteLine($"处理题目：第{question.QuestionNumber}题 {question.Id} - {question.QuestionStatement}");
                    Debug.WriteLine($"  类型：{question.Type}({(int)question.Type})，选项数：{question.Options?.Count ?? 0}");

                    if (question.Options != null)
                    {
                        for (int i = 0; i < question.Options.Count; i++)
                        {
                            question.Options[i].OptionCode = ((char)('A' + i)).ToString();
                            Debug.WriteLine($"    选项{question.Options[i].OptionCode}：{question.Options[i].Text}");
                        }
                    }
                }

                // 🔧 确保在UI线程上执行
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        Debug.WriteLine("正在绑定数据到UI...");

                        // 🚀 更新题目总数显示
                        TheoryQuestionsCountText.Text = $"共 {questions.Count} 道题目";

                        TheoryQuestionsList.ItemsSource = null;
                        TheoryQuestionsList.UpdateLayout();
                        TheoryQuestionsList.ItemsSource = questions;
                        TheoryQuestionsList.UpdateLayout();

                        Debug.WriteLine($"✅ 理论题目已绑定到UI：{questions.Count} 道");
                        Debug.WriteLine($"UI控件状态：ItemsSource = {TheoryQuestionsList.ItemsSource != null}");
                        Debug.WriteLine($"UI控件项目数：{TheoryQuestionsList.Items.Count}");

                        // 🔧 强制刷新UI
                        InvalidateVisual();
                        UpdateLayout();
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"UI绑定异常：{uiEx.Message}");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 加载理论题目到UI失败：{ex.Message}");
                Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");

                MessageBox.Show($"加载理论题目到UI失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 调试：手动测试理论题目加载
        /// </summary>
        public void TestTheoryQuestionsLoad()
        {
            Debug.WriteLine("=== 手动测试理论题目加载 ===");
            var testQuestions = CreateTestTheoryQuestions();
            LoadTheoryQuestionsToUI(testQuestions);
        }

        /// <summary>
        /// 创建测试理论题目（用于调试）
        /// </summary>
        private List<TheoryQuestion> CreateTestTheoryQuestions()
        {
            var testQuestions = new List<TheoryQuestion>();

            // 测试单选题
            var singleChoiceQuestion = new TheoryQuestion
            {
                Id = "TEST001",
                QuestionStatement = "这是一道测试单选题，用于验证UI显示功能",
                Type = TheoryQuestionType.SingleChoice,
                Category = TheoryQuestionCategory.FlightPrinciples,
                Difficulty = QuestionDifficulty.Easy,
                Points = 2,
                Options = new List<TheoryOption>
        {
            new TheoryOption { Text = "选项A - 这是错误答案", IsCorrect = false },
            new TheoryOption { Text = "选项B - 这是正确答案", IsCorrect = true },
            new TheoryOption { Text = "选项C - 这是错误答案", IsCorrect = false },
            new TheoryOption { Text = "选项D - 这是错误答案", IsCorrect = false }
        },
                CorrectAnswers = new List<string> { "选项B - 这是正确答案" },
                IsSelected = true,
                IsActive = true
            };

            testQuestions.Add(singleChoiceQuestion);

            // 测试多选题
            var multipleChoiceQuestion = new TheoryQuestion
            {
                Id = "TEST002",
                QuestionStatement = "这是一道测试多选题，用于验证UI显示功能",
                Type = TheoryQuestionType.MultipleChoice,
                Category = TheoryQuestionCategory.ControlAlgorithm,
                Difficulty = QuestionDifficulty.Medium,
                Points = 3,
                Options = new List<TheoryOption>
        {
            new TheoryOption { Text = "选项A - 正确答案之一", IsCorrect = true },
            new TheoryOption { Text = "选项B - 错误答案", IsCorrect = false },
            new TheoryOption { Text = "选项C - 正确答案之一", IsCorrect = true },
            new TheoryOption { Text = "选项D - 错误答案", IsCorrect = false }
        },
                CorrectAnswers = new List<string> { "选项A - 正确答案之一", "选项C - 正确答案之一" },
                IsSelected = true,
                IsActive = true
            };

            testQuestions.Add(multipleChoiceQuestion);

            // 🚀 添加第三道题目
            var anotherSingleChoice = new TheoryQuestion
            {
                Id = "TEST003",
                QuestionStatement = "这是第三道测试题目，用于验证序号功能",
                Type = TheoryQuestionType.SingleChoice,
                Category = TheoryQuestionCategory.FlightSafety,
                Difficulty = QuestionDifficulty.Hard,
                Points = 3,
                Options = new List<TheoryOption>
        {
            new TheoryOption { Text = "选项A - 错误", IsCorrect = false },
            new TheoryOption { Text = "选项B - 错误", IsCorrect = false },
            new TheoryOption { Text = "选项C - 正确", IsCorrect = true }
        },
                CorrectAnswers = new List<string> { "选项C - 正确" },
                IsSelected = true,
                IsActive = true
            };

            testQuestions.Add(anotherSingleChoice);

            return testQuestions;
        }

        // ========== 新增：加载飞控题目到UI ==========
        private void LoadFCQuestionsToUI(List<FlightControlQuestion> questions)
        {
            try
            {
                FCQuestionsList.ItemsSource = questions;
                Debug.WriteLine($"飞控题目已加载到UI：{questions.Count} 道");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载飞控题目到UI失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        /// <summary>
        /// 🚀 确保DisableCanvasButtons方法也能处理误修复按钮
        /// </summary>
        private void DisableCanvasButtons(Canvas canvas)
        {
            if (canvas == null) return;

            foreach (var child in canvas.Children)
            {
                if (child is Button btn)
                {
                    // 检查是否是修复相关的按钮
                    string content = btn.Content?.ToString() ?? "";
                    if (content == "修  复" || content == "已修复" || content == "误修复")
                    {
                        btn.IsEnabled = false;

                        // 根据当前状态设置颜色
                        if (content == "误修复")
                        {
                            btn.Background = new SolidColorBrush(Colors.DarkRed); // 误修复按钮变为深红色
                        }
                        else if (content == "已修复")
                        {
                            btn.Background = new SolidColorBrush(Colors.DarkGreen); // 正确修复按钮变为深绿色
                        }
                        else
                        {
                            btn.Background = new SolidColorBrush(Colors.Gray); // 未操作的按钮变为灰色
                        }
                    }
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
                // 尝试从配置管理器获取配置
                var config = SerialPortManager.GetConfigByPort(portName);
                if (config != null)
                {
                    // 标记端口为使用中
                    SerialPortManager.SetPortInUse(portName, true);

                    using var serialPort = config.CreateSerialPort();
                    serialPort.Open();
                    serialPort.Write(data);

                    Thread.Sleep(100);
                    string receivedData = "";
                    if (serialPort.BytesToRead > 0)
                    {
                        receivedData = serialPort.ReadExisting();
                    }

                    serialPort.Close();

                    // 标记端口使用完毕
                    SerialPortManager.SetPortInUse(portName, false);

                    return JsonSerializer.Serialize(new { result = "ok", recv = receivedData });
                }
                else
                {
                    // 使用传统方式（向后兼容）
                    using var serialPort = new SerialPort(portName, baudRate, parity, 8, stopBits)
                    {
                        ReadTimeout = 3000,
                        WriteTimeout = 3000
                    };

                    serialPort.Open();
                    serialPort.Write(data);
                    Thread.Sleep(100);

                    string receivedData = "";
                    if (serialPort.BytesToRead > 0)
                    {
                        receivedData = serialPort.ReadExisting();
                    }

                    serialPort.Close();
                    return JsonSerializer.Serialize(new { result = "ok", recv = receivedData });
                }
            }
            catch (Exception ex)
            {
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

            // ========== 检查所有串口状态 ==========
            CheckSerialPortAvailability();

            // 修改：不再仅限制学生用户，所有以学生身份登录的用户都可以使用
            // 因为管理员和教师也可能以学生身份登录
            InitializeSerialPort();

            // 在窗口标题中显示当前登录身份信息
            UpdateWindowTitle();
        }

        // 修改 CheckSerialPortAvailability 方法，同时检查两个端口
        private void CheckSerialPortAvailability()
        {
            // 检查飞控通信端口
            try
            {
                var flightControllerConfig = SerialPortManager.GetConfigByPurpose(SerialPortPurpose.FlightController);

                if (flightControllerConfig != null)
                {
                    var availablePorts = SerialPort.GetPortNames();
                    if (!availablePorts.Contains(flightControllerConfig.PortName))
                    {
                        // 更新UI显示飞控端口不可用
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (MotorTestStatusText != null)
                            {
                                MotorTestStatusText.Text = $"状态：飞控通信端口{flightControllerConfig.PortName}不可用";
                                MotorTestStatusText.Foreground = new SolidColorBrush(Colors.Red);
                            }
                        });
                    }
                }
                else
                {
                    // 没有配置飞控通信端口
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (MotorTestStatusText != null)
                        {
                            MotorTestStatusText.Text = "状态：未配置飞控通信端口";
                            MotorTestStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查飞控端口失败: {ex.Message}");
            }

            // ========== 检查检修端口 ==========
            CheckRepairPortAvailability();
        }

        private void UpdateWindowTitle()
        {
            string roleInfo = "";
            if (currentUser.Type != UserType.Student)
            {
                roleInfo = $" - {GetUserTypeDisplayName(currentUser.Type)}以学生身份登录";
            }
            this.Title = $"康鹤多旋翼无人机检修平台-V2.0{roleInfo}";
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

        /// <summary>
        /// 电路检测子选项卡选择变化时自动缩放对应的Canvas
        /// </summary>
        private void CircuitSubTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == CircuitSubTabControl)
            {
                // 延迟执行，确保子TabItem切换完成
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitSubCanvasToView();
                }), DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 自动缩放电路检测子页面的Canvas到视窗大小并居中
        /// </summary>
        private void FitSubCanvasToView()
        {
            var selectedSubTabItem = CircuitSubTabControl.SelectedItem as TabItem;
            if (selectedSubTabItem == null) return;

            ScrollViewer? scrollViewer = null;
            Canvas? canvas = null;
            ScaleTransform? scaleTransform = null;

            // 根据选中的子TabItem确定对应的ScrollViewer、Canvas和ScaleTransform
            if (selectedSubTabItem.Header.ToString() == "电机-电调-飞控")
            {
                scrollViewer = MotorScrollViewer;
                canvas = MotorCanvas;
                scaleTransform = MotorCanvasScale;
            }
            else if (selectedSubTabItem.Header.ToString() == "GPS-飞控-接收机")
            {
                scrollViewer = MainScrollViewer;
                canvas = ZoomCanvas;
                scaleTransform = CanvasScale;
            }
            else if (selectedSubTabItem.Header.ToString() == "扩展-飞控-电源")
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

        // 自动缩放Canvas到视窗大小并居中
        private void FitCanvasToView()
        {
            var selectedTabItem = MainTabControl.SelectedItem as TabItem;
            if (selectedTabItem == null) return;

            // 如果选中的是电路检测页面，则缩放当前子页面
            if (selectedTabItem.Header.ToString() == "电路检测")
            {
                // 延迟执行子页面缩放，确保电路检测页面已经完全加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FitSubCanvasToView();
                }), DispatcherPriority.Background);
                return;
            }

            // 原有的其他页面缩放逻辑保持不变
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

        /// <summary>
        /// 添加公共方法，允许手动调用子页面缩放
        /// </summary>
        public void ResetSubCanvasScale()
        {
            FitSubCanvasToView();
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

        // 修改 InitializeSerialPort 方法，添加检修端口状态检测
        private void InitializeSerialPort()
        {
            try
            {
                // 确保串口配置已加载
                SerialPortManager.LoadConfigurations();

                // ========== 检查飞控通信端口状态（用于电机测试） ==========
                var flightControllerConfig = SerialPortManager.GetConfigByPurpose(SerialPortPurpose.FlightController);

                if (flightControllerConfig != null && flightControllerConfig.IsEnabled)
                {
                    // 更新状态显示为飞控通信端口
                    if (MotorTestStatusText != null)
                    {
                        MotorTestStatusText.Text = $"状态：已配置{flightControllerConfig.PortName} - 飞控通信端口";
                        MotorTestStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    }
                }
                else
                {
                    // 飞控通信端口未配置，显示警告
                    if (MotorTestStatusText != null)
                    {
                        MotorTestStatusText.Text = "状态：未配置飞控通信端口，电机测试不可用";
                        MotorTestStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                }

                // ========== 检查检修端口状态（用于修复功能） ==========
                var droneConfig = SerialPortManager.GetDroneRepairConfig();

                if (droneConfig != null && droneConfig.IsEnabled)
                {
                    // 更新全局配置以保持向后兼容（用于修复按钮）
                    SerialConfig = droneConfig;

                    // 更新检修端口状态显示
                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = $"检修端口：{droneConfig.PortName} - 就绪";
                        RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    }
                }
                else
                {
                    // 检修端口未配置，显示警告
                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = "检修端口：未配置，修复功能不可用";
                        RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    }

                    // 尝试加载旧配置文件
                    if (File.Exists("config_serialport.json"))
                    {
                        var json = File.ReadAllText("config_serialport.json");
                        SerialConfig = JsonSerializer.Deserialize<SerialPortConfig>(json) ?? new SerialPortConfig();

                        if (!string.IsNullOrEmpty(SerialConfig.PortName) && RepairPortStatusText != null)
                        {
                            RepairPortStatusText.Text = $"检修端口：{SerialConfig.PortName} - 旧配置";
                            RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                        }
                    }
                    else
                    {
                        // 显示配置对话框
                        ShowAdminDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化串口配置失败: {ex.Message}", "串口错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加检修端口状态检测方法
        private void CheckRepairPortAvailability()
        {
            try
            {
                var droneRepairConfig = SerialPortManager.GetConfigByPurpose(SerialPortPurpose.DroneRepair);

                if (droneRepairConfig != null)
                {
                    var availablePorts = SerialPort.GetPortNames();
                    if (!availablePorts.Contains(droneRepairConfig.PortName))
                    {
                        // 检修端口不可用
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (RepairPortStatusText != null)
                            {
                                RepairPortStatusText.Text = $"检修端口：{droneRepairConfig.PortName} - 不可用";
                                RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                            }
                        });
                    }
                    else
                    {
                        // 检修端口可用
                        Dispatcher.BeginInvoke(() =>
                        {
                            if (RepairPortStatusText != null)
                            {
                                RepairPortStatusText.Text = $"检修端口：{droneRepairConfig.PortName} - 就绪";
                                RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Green);
                            }
                        });
                    }
                }
                else
                {
                    // 没有配置检修端口
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (RepairPortStatusText != null)
                        {
                            RepairPortStatusText.Text = "检修端口：未配置";
                            RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查检修端口失败: {ex.Message}");
                Dispatcher.BeginInvoke(() =>
                {
                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = "检修端口：检测失败";
                        RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    }
                });
            }
        }


        private async Task InitializeCircuitQuestions()
        {
            System.Diagnostics.Debug.WriteLine("=== 开始初始化电路题目（修复版） ===");

            if (latestExam?.Questions != null)
            {
                // 🚀 新增：打印所有题目的状态
                System.Diagnostics.Debug.WriteLine("=== 题目状态调试信息 ===");
                int index = 0;
                foreach (var q in latestExam.Questions.Where(qu => !string.IsNullOrEmpty(qu.CommandString)))
                {
                    System.Diagnostics.Debug.WriteLine($"题目[{++index}]：Name={q.Name}, IsChecked={q.IsChecked}, Content={q.Content}");
                }
                System.Diagnostics.Debug.WriteLine("========================");

                var cfg = SerialConfig;
                System.Diagnostics.Debug.WriteLine($"串口配置：PortName={cfg.PortName}, BaudRate={cfg.BaudRate}");

                // 🚀 重要修复：处理所有有CommandString的题目（不论是否勾选）
                var allCircuitQuestions = latestExam.Questions
                    .Where(q => !string.IsNullOrEmpty(q.CommandString))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"总电路题目数量：{allCircuitQuestions.Count}");
                System.Diagnostics.Debug.WriteLine($"其中需要设置故障的题目：{allCircuitQuestions.Count(q => q.IsChecked)}");
                System.Diagnostics.Debug.WriteLine($"其中需要消除故障的题目：{allCircuitQuestions.Count(q => !q.IsChecked)}");

                // ========== 以下是原有的完整初始化逻辑 ==========
                int total = allCircuitQuestions.Count;
                int done = 0;
                LoadingProgressBar.Visibility = Visibility.Visible;
                LoadingProgressBar.Value = 0;

                bool initializationSuccessful = true;
                string errorMessage = "";

                foreach (var question in allCircuitQuestions)
                {
                    try
                    {
                        string cmd = question.CommandString;

                        // 🔧 关键修复：对所有题目发送指令
                        if (question.IsChecked)
                        {
                            // 需要修复的题目：发送原始指令（设置故障）
                            System.Diagnostics.Debug.WriteLine($"设置故障：{question.Name} -> {cmd}");
                        }
                        else
                        {
                            // 不需要修复的题目：指令第10位（下标9）改为 '0'（消除故障）
                            if (!string.IsNullOrEmpty(cmd) && cmd.Length >= 10)
                            {
                                var sb = new StringBuilder(cmd);
                                sb[9] = '0';
                                cmd = sb.ToString();
                            }
                            System.Diagnostics.Debug.WriteLine($"消除故障：{question.Name} -> {cmd}");
                        }

                        // 在后台线程执行串口操作
                        string result = await Task.Run(() =>
                            SendSerialData(cfg.PortName, cfg.BaudRate, cfg.Parity, cfg.StopBits, cmd));

                        // 在UI线程处理结果
                        if (IsSerialWriteSuccessful(result, out string error, out string received))
                        {
                            done++;
                            LoadingProgressBar.Value = (double)done / total * 100;
                            System.Diagnostics.Debug.WriteLine($"初始化成功：{question.Name}");
                        }
                        else
                        {
                            errorMessage = $"试题初始化失败：指令 {cmd} 未能成功发送。错误：{error}";
                            initializationSuccessful = false;
                            System.Diagnostics.Debug.WriteLine($"初始化失败：{question.Name} - {error}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessage = $"串口发送异常：{ex.Message}";
                        initializationSuccessful = false;
                        System.Diagnostics.Debug.WriteLine($"初始化异常：{question.Name} - {ex.Message}");
                        break;
                    }
                }

                // 根据初始化结果设置UI状态
                if (initializationSuccessful)
                {
                    LoadingProgressText.Text = "试题加载完毕，设备初始化完成！";
                    ExamMachineText.Text = "考试设备：正常";
                    StartExamTimer();
                    System.Diagnostics.Debug.WriteLine("✅ 所有题目初始化完成");
                }
                else
                {
                    LoadingProgressText.Text = "试题加载失败，设备初始化未完成！";
                    ExamMachineText.Text = "考试设备：异常！";
                    System.Diagnostics.Debug.WriteLine($"❌ 题目初始化失败：{errorMessage}");
                    MessageBox.Show(errorMessage, "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ latestExam?.Questions为空");
                MessageBox.Show("试卷文件可能损坏，请联系管理员！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // 修改统计显示方法，适配混合试卷
        private void ShowExamStats(int circuitTotal, int theoryTotal, int fcTotal, int circuitCorrect, int theoryCorrect, int fcCorrect, TimeSpan duration)
        {
            var stats = new List<ExamStatItem>
    {
        // 题目数量统计
        new ExamStatItem { Name = "理论题", Value = $"{theoryTotal}题" },
        new ExamStatItem { Name = "电路检测", Value = $"{circuitTotal}题" },
        new ExamStatItem { Name = "飞控实操", Value = $"{fcTotal}题" },
        new ExamStatItem { Name = "题目总数", Value = $"{circuitTotal + theoryTotal + fcTotal}题" },
        
        // 完成情况统计
        new ExamStatItem { Name = "理论已答", Value = $"{_theoryAnswers.Count}/{theoryTotal}" },
        new ExamStatItem { Name = "电路已修", Value = $"{circuitCorrect}/{circuitTotal}" },
        new ExamStatItem { Name = "飞控已验", Value = $"{_fcAnswers.Count}/{fcTotal}" },
        
        // 时间统计
        new ExamStatItem { Name = "答题耗时", Value = duration.ToString(@"mm\:ss") }
    };

            ExamStatListView.ItemsSource = stats;
        }

        // 重载方法，保持向后兼容
        private void ShowExamStats(int total, int correct, int wrong, TimeSpan duration)
        {
            // 计算混合试卷的各类题目数量
            int theoryTotal = _theoryQuestions?.Count ?? 0;
            int fcTotal = _fcQuestions?.Count ?? 0;
            int circuitTotal = latestExam?.Questions?.Count(q => !string.IsNullOrEmpty(q.CommandString)) ?? 0;

            ShowExamStats(circuitTotal, theoryTotal, fcTotal, correct, 0, 0, duration);
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

        /// <summary>
        /// 🚀 修改：修复按钮点击 - 误修复题不发送串口指令
        /// </summary>
        private void RepairButton_Click(object sender, RoutedEventArgs e)
        {
            // 🔍 最基本的调试信息
            System.Diagnostics.Debug.WriteLine("=== RepairButton_Click 方法被调用（修改版）===");
            Console.WriteLine("=== RepairButton_Click 方法被调用（修改版）===");

            // 如果考试已提交，禁止继续答题
            if (isExamSubmitted)
            {
                System.Diagnostics.Debug.WriteLine("❌ 考试已提交，禁止答题");
                MessageBox.Show("考试已提交，无法继续答题！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 🔍 检查sender类型和Tag
            System.Diagnostics.Debug.WriteLine($"🔍 Sender类型：{sender?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"🔍 Sender内容：{(sender as Button)?.Content}");

            if (sender is not Button btn)
            {
                System.Diagnostics.Debug.WriteLine("❌ sender不是Button类型");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔍 按钮Tag：{btn.Tag}");
            System.Diagnostics.Debug.WriteLine($"🔍 按钮Tag类型：{btn.Tag?.GetType().Name}");

            if (btn.Tag is not string questionName)
            {
                System.Diagnostics.Debug.WriteLine("❌ 按钮Tag不是string类型或为空");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔍 题目名称：{questionName}");

            if (latestExam == null)
            {
                System.Diagnostics.Debug.WriteLine("❌ latestExam为空");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"🔍 latestExam.Questions数量：{latestExam.Questions?.Count ?? 0}");

            // 💡 重要：禁用按钮，防止重复点击
            btn.IsEnabled = false;
            System.Diagnostics.Debug.WriteLine("🔍 按钮已禁用");

            // 🔧 修复：查找题目时不限制CommandString
            var question = latestExam.Questions?.Find(q => q.Name == questionName);

            if (question == null)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 找不到题目：{questionName}");

                // 🔍 列出所有题目名称用于调试
                System.Diagnostics.Debug.WriteLine("📋 所有可用题目名称：");
                if (latestExam.Questions != null)
                {
                    foreach (var q in latestExam.Questions)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {q.Name} (IsChecked: {q.IsChecked}, HasCommand: {!string.IsNullOrEmpty(q.CommandString)})");
                    }
                }

                // 恢复按钮状态
                btn.IsEnabled = true;
                return;
            }

            // 🚀 详细的调试信息
            System.Diagnostics.Debug.WriteLine($"🔍 找到题目：{questionName}");
            System.Diagnostics.Debug.WriteLine($"🔍 题目详细信息：");
            System.Diagnostics.Debug.WriteLine($"  - Name：{question.Name}");
            System.Diagnostics.Debug.WriteLine($"  - IsChecked：{question.IsChecked}");
            System.Diagnostics.Debug.WriteLine($"  - Content：{question.Content}");
            System.Diagnostics.Debug.WriteLine($"  - CommandString：{question.CommandString}");

            bool shouldSendCommand = false; // 🔧 新增：控制是否发送串口指令

            if (question.IsChecked) // 需要修复的题目
            {
                // ✅ 正确修复
                System.Diagnostics.Debug.WriteLine("✅ 进入正确修复分支");

                btn.Background = new SolidColorBrush(Colors.LightGreen);
                btn.Content = "已修复";
                btn.Foreground = new SolidColorBrush(Colors.DarkGreen);

                // 记录正确修复
                _questionRepairStatus[questionName] = true;
                correctAnswers++; // 用于统计显示

                shouldSendCommand = true; // 🔧 正确修复需要发送串口指令
                System.Diagnostics.Debug.WriteLine($"✅ 正确修复：{questionName}，correctAnswers={correctAnswers}，需要发送串口指令");
            }
            else // 不需要修复的题目
            {
                // ❌ 误修复
                System.Diagnostics.Debug.WriteLine($"🚨 进入误修复分支：{questionName}");

                btn.Content = "误修复";
                btn.Background = new SolidColorBrush(Colors.White);
                btn.Foreground = new SolidColorBrush(Colors.IndianRed);

                System.Diagnostics.Debug.WriteLine($"🚨 按钮状态已设置：Content={btn.Content}, Background=IndianRed");

                // 🎯 关键：记录误修复状态
                _questionRepairStatus[questionName] = false;
                wrongAnswers++; // 用于统计显示

                shouldSendCommand = false; // 🔧 误修复不发送串口指令
                System.Diagnostics.Debug.WriteLine($"❌ 误修复记录：{questionName}，wrongAnswers={wrongAnswers}，不发送串口指令");
                System.Diagnostics.Debug.WriteLine($"❌ _questionRepairStatus[{questionName}] = {_questionRepairStatus[questionName]}");

                // 显示警告信息
                MessageBox.Show("⚠️ 误修复！\n\n该连接点不需要修复，此操作将被记录为扣分项。",
                                "误修复提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 🔌 修改：只有在需要发送指令时才发送串口指令
            if (shouldSendCommand && !string.IsNullOrEmpty(question.CommandString))
            {
                try
                {
                    string cmd = question.CommandString;
                    System.Diagnostics.Debug.WriteLine($"🔌 准备发送串口指令：{cmd}");

                    // 根据修复类型设置指令（正确修复设置为'0'）
                    if (!string.IsNullOrEmpty(cmd) && cmd.Length >= 10)
                    {
                        var sb = new StringBuilder(cmd);
                        sb[9] = '0'; // 正确修复设置为'0'
                        cmd = sb.ToString();
                        System.Diagnostics.Debug.WriteLine($"🔌 修改后的指令：{cmd}");
                    }

                    var cfg = SerialConfig;
                    System.Diagnostics.Debug.WriteLine($"🔌 串口配置：PortName={cfg.PortName}, BaudRate={cfg.BaudRate}");

                    if (string.IsNullOrEmpty(cfg.PortName))
                    {
                        System.Diagnostics.Debug.WriteLine("❌ 串口配置无效，PortName为空");
                        MessageBox.Show("串口未配置，请检查设置！", "配置错误", MessageBoxButton.OK, MessageBoxImage.Error);

                        // 恢复按钮状态
                        RestoreButtonToInitialState(btn, questionName, question);
                        return;
                    }

                    // 更新端口状态
                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = $"检修端口：{cfg.PortName} - 发送指令中...";
                    }

                    string result = SendSerialData(cfg.PortName, cfg.BaudRate, cfg.Parity, cfg.StopBits, cmd);
                    System.Diagnostics.Debug.WriteLine($"🔌 串口发送结果：{result}");

                    if (IsSerialWriteSuccessful(result, out string error, out string received))
                    {
                        // 指令发送成功
                        if (RepairPortStatusText != null)
                        {
                            RepairPortStatusText.Text = $"检修端口：{cfg.PortName} - 指令发送成功";
                        }

                        System.Diagnostics.Debug.WriteLine($"🔌 串口指令发送成功：{cmd}");
                        System.Diagnostics.Debug.WriteLine($"✅ 最终按钮状态：Content={btn.Content}, IsEnabled={btn.IsEnabled}");
                    }
                    else
                    {
                        // 指令发送失败，需要恢复按钮状态
                        System.Diagnostics.Debug.WriteLine($"❌ 串口指令发送失败：{error}，准备恢复按钮状态");

                        if (RepairPortStatusText != null)
                        {
                            RepairPortStatusText.Text = $"检修端口：{cfg.PortName} - 指令发送失败";
                            RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                        }

                        MessageBox.Show($"修复指令发送失败：{error}", "串口错误",
                                       MessageBoxButton.OK, MessageBoxImage.Error);

                        // 🔄 恢复按钮到初始状态
                        RestoreButtonToInitialState(btn, questionName, question);
                        System.Diagnostics.Debug.WriteLine($"🔄 按钮状态已恢复，误修复记录已撤销");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // 异常处理，恢复按钮状态
                    System.Diagnostics.Debug.WriteLine($"❌ 串口发送异常：{ex.Message}，准备恢复按钮状态");

                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = $"检修端口：发送异常";
                        RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    }

                    MessageBox.Show($"端口发送异常: {ex.Message}", "系统错误",
                                   MessageBoxButton.OK, MessageBoxImage.Error);

                    // 🔄 恢复按钮到初始状态
                    RestoreButtonToInitialState(btn, questionName, question);
                    System.Diagnostics.Debug.WriteLine($"🔄 按钮状态已恢复，误修复记录已撤销");
                    return;
                }
            }
            else if (!shouldSendCommand)
            {
                System.Diagnostics.Debug.WriteLine($"🚫 误修复题目，跳过串口发送");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 题目没有CommandString，跳过串口发送");
            }

            // 🚀 更新统计显示（不包含实时得分）
            UpdateExamStats();
            System.Diagnostics.Debug.WriteLine($"📊 统计已更新，当前wrongAnswers={wrongAnswers}");
            System.Diagnostics.Debug.WriteLine("=== RepairButton_Click 方法执行完毕（修改版）===");
        }

        /// <summary>
        /// 🔄 恢复按钮到初始状态（当串口操作失败时使用）
        /// </summary>
        private void RestoreButtonToInitialState(Button btn, string questionName, Question question)
        {
            try
            {
                // 恢复按钮外观
                btn.IsEnabled = true;
                btn.Background = new SolidColorBrush(Colors.Goldenrod);
                btn.Content = "修  复";
                btn.Foreground = new SolidColorBrush(Colors.DarkSlateGray);

                // 撤销状态记录
                _questionRepairStatus.Remove(questionName);

                // 撤销统计计数
                if (question.IsChecked)
                {
                    correctAnswers = Math.Max(0, correctAnswers - 1);
                }
                else
                {
                    wrongAnswers = Math.Max(0, wrongAnswers - 1);
                }

                System.Diagnostics.Debug.WriteLine($"🔄 按钮状态已恢复：{questionName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复按钮状态失败：{ex.Message}");
            }
        }

        // 添加检修端口连接测试方法
        private async Task<bool> TestRepairPortConnection()
        {
            try
            {
                var droneConfig = SerialPortManager.GetDroneRepairConfig();
                if (droneConfig == null)
                {
                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = "检修端口：未配置";
                        RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    }
                    return false;
                }

                if (RepairPortStatusText != null)
                {
                    RepairPortStatusText.Text = $"检修端口：{droneConfig.PortName} - 测试连接中...";
                    RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Yellow);
                }

                bool success = await Task.Run(() => SerialPortManager.TestPortConnection(droneConfig));

                if (success)
                {
                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = $"检修端口：{droneConfig.PortName} - 连接正常";
                        RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Green);
                    }
                    return true;
                }
                else
                {
                    if (RepairPortStatusText != null)
                    {
                        RepairPortStatusText.Text = $"检修端口：{droneConfig.PortName} - 连接失败";
                        RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (RepairPortStatusText != null)
                {
                    RepairPortStatusText.Text = "检修端口：测试异常";
                    RepairPortStatusText.Foreground = new SolidColorBrush(Colors.Red);
                }
                Debug.WriteLine($"测试检修端口连接失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 🚀 修复：提交按钮中也使用正确的扣分机制
        /// </summary>
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

            // 🚀 重要：计算并显示最终分数
            int finalScore = CalculateExamScore();

            // 🚀 禁用所有答题控件
            DisableAllRepairButtons();      // 禁用电路检测修复按钮
            DisableAllTheoryOptions();      // 禁用理论题选项按钮
            DisableAllFCControls();         // 禁用飞控实操控件

            // 禁用提交按钮自身
            SubmitButton.IsEnabled = false;
            SubmitButton.Content = "已提交";
            SubmitButton.Background = new SolidColorBrush(Colors.Gray);

            // 更新最终统计（现在会显示分数）
            UpdateExamStats();

            // 保存考试记录
            SaveExamRecord(finalScore);

            // 显示提交成功消息
            var elapsedTime = GetElapsedExamTime();

            // 🚀 计算各部分详细得分 - 使用正确的扣分机制
            double theoryScoreRate = CalculateTheoryScore();
            double circuitScoreRate = CalculateCircuitScoreWithPenalty(); // 使用扣分机制

            var theoryScoreInt = (int)Math.Round(_currentExamScoreConfig.TheoryScore * theoryScoreRate);
            var circuitScoreInt = (int)Math.Round(_currentExamScoreConfig.CircuitScore * circuitScoreRate);
            var fcScoreInt = 0; // 暂不计分

            // 🚀 获取详细的修复统计信息
            string repairStatsMessage = GetRepairStatsMessage();

            MessageBox.Show($"提交成功！\n\n" +
                           $"📊 考试得分详情：\n" +
                           $"总分：{finalScore}/{_currentExamScoreConfig.TotalScore}分\n\n" +
                           $"📚 理论题：{theoryScoreInt}/{_currentExamScoreConfig.TheoryScore}分 (得分率：{theoryScoreRate:P2})\n" +
                           $"🔧 电路题：{circuitScoreInt}/{_currentExamScoreConfig.CircuitScore}分 (得分率：{circuitScoreRate:P2})\n" +
                           $"🎮 飞控题：{fcScoreInt}/{_currentExamScoreConfig.FCScore}分 (暂不计分)\n\n" +
                           repairStatsMessage +
                           $"⏱️ 答题耗时：{elapsedTime:hh\\:mm\\:ss}",
                           "考试结果", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 🚀 新增：获取修复统计信息的详细消息
        /// </summary>
        private string GetRepairStatsMessage()
        {
            try
            {
                int circuitTotal = latestExam?.Questions?.Count(q => !string.IsNullOrEmpty(q.CommandString)) ?? 0;

                if (circuitTotal == 0)
                    return "";

                int correctRepairs = 0;
                int wrongRepairs = 0;
                int missedRepairs = 0;

                if (latestExam?.Questions != null)
                {
                    foreach (var question in latestExam.Questions.Where(q => !string.IsNullOrEmpty(q.CommandString)))
                    {
                        if (_questionRepairStatus.ContainsKey(question.Name))
                        {
                            bool repairResult = _questionRepairStatus[question.Name];
                            if (question.IsChecked && repairResult)
                            {
                                correctRepairs++;
                            }
                            else if (!question.IsChecked && !repairResult)
                            {
                                wrongRepairs++;
                            }
                        }
                        else if (question.IsChecked)
                        {
                            missedRepairs++;
                        }
                    }
                }

                string statsMessage = "🔧 电路检测详情：\n";
                statsMessage += $"   ✅ 正确修复：{correctRepairs}题 (+{correctRepairs}分)\n";

                if (wrongRepairs > 0)
                {
                    statsMessage += $"   ❌ 误修复：{wrongRepairs}题 (-{wrongRepairs * 0.5}分)\n";
                }

                if (missedRepairs > 0)
                {
                    statsMessage += $"   ⏸️ 漏修复：{missedRepairs}题 (0分)\n";
                }

                statsMessage += "\n";

                return statsMessage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取修复统计信息失败：{ex.Message}");
                return "";
            }
        }


        // 新增：保存考试记录方法
        private void SaveExamRecord(int finalScore)
        {
            try
            {
                var examRecord = new DetailedExamRecord // 改为 DetailedExamRecord
                {
                    StudentName = currentUser.Name,
                    StudentId = currentUser.IdNumber,
                    Score = finalScore,
                    CorrectAnswers = correctAnswers,
                    WrongAnswers = wrongAnswers,
                    ElapsedTime = GetElapsedExamTime(),
                    StartTime = examStartTime,
                    SubmitTime = DateTime.Now
                };

                // 设置考试信息
                if (latestExam != null)
                {
                    examRecord.ExamInfo = new ExamInfo
                    {
                        ExamName = latestExam.ExamName,
                        TeacherName = latestExam.TeacherName,
                        TeacherId = latestExam.TeacherId,
                        CreationTime = latestExam.CreationTime,
                        TotalQuestions = latestExam.Questions?.Count(q => q.IsChecked) ?? 0
                    };

                    // 分析所有题目状态
                    AnalyzeQuestionStatuses(examRecord);
                }

                // 保存记录
                bool saveSuccess = ExamRecordManager.SaveExamRecord(examRecord);

                if (saveSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"考试记录已保存，序号：{examRecord.ExamSequence}");
                }
                else
                {
                    MessageBox.Show("考试记录保存失败，但成绩已记录！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存考试记录时发生错误: {ex.Message}");
                MessageBox.Show("考试记录保存时发生错误，但成绩已记录！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 新增：分析题目状态方法
        // 修改 AnalyzeQuestionStatuses 方法的参数类型
        private void AnalyzeQuestionStatuses(DetailedExamRecord examRecord)
        {
            // 方法内容保持不变
            if (latestExam?.Questions == null) return;

            foreach (var question in latestExam.Questions)
            {
                var questionStatus = new QuestionStatus
                {
                    QuestionName = question.Name,
                    QuestionContent = question.Content,
                    ShouldRepair = question.IsChecked,
                    CommandString = question.CommandString
                };

                // 检查学生是否修复了这个问题
                bool studentRepaired = _questionRepairStatus.ContainsKey(question.Name);
                questionStatus.StudentRepaired = studentRepaired;

                // 确定修复状态
                if (!studentRepaired)
                {
                    if (question.IsChecked)
                    {
                        // 应该修复但没修复
                        questionStatus.Status = RepairStatus.Unrepaired;
                        examRecord.UnrepairedQuestions.Add(question.Name);
                    }
                    else
                    {
                        // 不需要修复也没修复，正确
                        questionStatus.Status = RepairStatus.NotTouched;
                    }
                }
                else
                {
                    if (question.IsChecked)
                    {
                        // 应该修复并且修复了
                        bool repairedCorrectly = _questionRepairStatus[question.Name];
                        if (repairedCorrectly)
                        {
                            questionStatus.Status = RepairStatus.CorrectlyRepaired;
                            examRecord.CorrectlyRepairedQuestions.Add(question.Name);
                        }
                        else
                        {
                            // 这种情况理论上不应该发生，因为IsChecked=true意味着应该修复
                            questionStatus.Status = RepairStatus.WronglyRepaired;
                            examRecord.WronglyRepairedQuestions.Add(question.Name);
                        }
                    }
                    else
                    {
                        // 不应该修复但修复了，误修复
                        questionStatus.Status = RepairStatus.WronglyRepaired;
                        examRecord.WronglyRepairedQuestions.Add(question.Name);
                    }
                }

                examRecord.QuestionStatuses.Add(questionStatus);
            }
        }

        /// <summary>
        /// 验证飞控实操题目答案
        /// </summary>
        private async Task<bool> ValidateFCAnswer(FlightControlQuestion question, string studentAnswer)
        {
            try
            {
                if (_fcService == null || !_fcService.IsConnected)
                {
                    MessageBox.Show("飞控未连接，无法验证参数！", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                var result = await FlightControlQuestionBankManager.ValidateAnswerWithFlightController(
                    question, studentAnswer, _fcService);

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    MessageBox.Show($"验证失败：{result.ErrorMessage}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (result.IsCorrect)
                {
                    correctAnswers++;
                    MessageBox.Show($"正确！参数 {question.ParameterName} 值为 {result.ActualValue}",
                        "验证结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    wrongAnswers++;
                    MessageBox.Show($"错误！参数 {question.ParameterName} 期望值：{result.CorrectValue}，实际值：{result.ActualValue}",
                        "验证结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return result.IsCorrect;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"验证过程出错：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止计时
            StopExamTimer();

            // 设置对话框结果并关闭
            this.DialogResult = false;
            this.Close();
        }

        // 添加一个ExamStatItem类用于统计显示
        public class ExamStatItem
        {
            public string Name { get; set; } = "";
            public string Value { get; set; } = "";
        }        

        // 在MainWindow类中添加FindVisualChildren方法（如果不存在的话）
        private void FindVisualChildren<T>(DependencyObject depObj, List<T> children) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        children.Add((T)child);
                    }

                    FindVisualChildren<T>(child, children);
                }
            }
        }

        /// <summary>
        /// 🚀 修改：理论题选项事件，不实时显示得分，提交后禁用
        /// </summary>
        private void TheoryOption_Checked(object sender, RoutedEventArgs e)
        {
            // 如果考试已提交，禁止继续答题
            if (isExamSubmitted)
            {
                if (sender is ToggleButton toggleButton)
                {
                    toggleButton.IsChecked = false; // 恢复未选中状态
                }
                MessageBox.Show("考试已提交，无法继续答题！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (sender is ToggleButton toggleButton && toggleButton.Tag is TheoryOption option)
                {
                    var question = GetQuestionFromOption(option);
                    if (question != null)
                    {
                        // 🔧 修复：如果是单选题，先取消同组其他选项
                        if (question.Type == TheoryQuestionType.SingleChoice)
                        {
                            ClearOtherOptionsInGroup(question.Id, option);
                        }

                        // 记录学生答案（不计算实时得分）
                        UpdateTheoryAnswer(question);

                        System.Diagnostics.Debug.WriteLine($"题目类型：{question.Type}, 题目ID={question.Id}, 选择={option.Text}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理理论题目选项选中失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 修改：理论题取消选中事件，提交后禁用
        /// </summary>
        private void TheoryOption_Unchecked(object sender, RoutedEventArgs e)
        {
            // 如果考试已提交，禁止继续答题
            if (isExamSubmitted)
            {
                if (sender is ToggleButton toggleButton)
                {
                    toggleButton.IsChecked = true; // 恢复选中状态
                }
                MessageBox.Show("考试已提交，无法继续答题！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (sender is ToggleButton toggleButton && toggleButton.Tag is TheoryOption option)
                {
                    var question = GetQuestionFromOption(option);
                    if (question != null)
                    {
                        // 更新答案（不计算实时得分）
                        UpdateTheoryAnswer(question);

                        System.Diagnostics.Debug.WriteLine($"取消选择：题目ID={question.Id}, 选项={option.Text}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理理论题目选项取消选中失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除同组其他选项（单选题用）
        /// </summary>
        private void ClearOtherOptionsInGroup(string questionId, TheoryOption currentOption)
        {
            try
            {
                var toggleButtons = new List<ToggleButton>();
                FindVisualChildren<ToggleButton>(TheoryQuestionsPanel, toggleButtons);

                foreach (var toggleButton in toggleButtons)
                {
                    if (toggleButton.Tag is TheoryOption option && option != currentOption)
                    {
                        var question = GetQuestionFromOption(option);
                        if (question?.Id == questionId)
                        {
                            // 🔧 修复：设置为false而不是调用IsChecked
                            toggleButton.IsChecked = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除同组选项失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新理论题答案
        /// </summary>
        private void UpdateTheoryAnswer(TheoryQuestion question)
        {
            try
            {
                var selectedOptions = new List<string>();

                var toggleButtons = new List<ToggleButton>();
                FindVisualChildren<ToggleButton>(TheoryQuestionsPanel, toggleButtons);

                foreach (var toggleButton in toggleButtons)
                {
                    if (toggleButton.IsChecked == true && toggleButton.Tag is TheoryOption option)
                    {
                        var optionQuestion = GetQuestionFromOption(option);
                        if (optionQuestion?.Id == question.Id)
                        {
                            selectedOptions.Add(option.Text);
                        }
                    }
                }

                _theoryAnswers[question.Id] = string.Join(";", selectedOptions);
                UpdateExamStats();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新理论题答案失败: {ex.Message}");
            }
        }

    }
    
}