using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AutoPilot.Parameters;

namespace DroneSimulator
{
    public partial class FlightControlQuestionEditWindow : Window
    {
        private FlightControlQuestion _question;
        private readonly bool _isEditMode;
        private readonly string? _currentUser;

        // 🔧 简化：只保留必要的服务实例
        private ArduPilotParameterService? _parameterService;

        // 🚀 新增：轻量级参数查询缓存
        private static readonly Dictionary<string, ParameterTestResult> _quickCache = new();
        private static readonly object _cacheLock = new object();
        private const int QUICK_CACHE_MINUTES = 5; // 短期缓存5分钟

        public FlightControlQuestion? Question => _question;

        public FlightControlQuestionEditWindow(FlightControlQuestion? question = null, string? currentUser = null)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _isEditMode = question != null;
            _question = question != null ? CloneQuestion(question) : CreateNewQuestion();

            InitializeControls();
            LoadQuestionData();

            // 设置窗口标题
            Title = _isEditMode ? "编辑飞控实操题目" : "新增飞控实操题目";
        }

        #region 初始化方法

        private void InitializeControls()
        {
            try
            {
                // 初始化题目类型下拉框
                QuestionTypeComboBox.ItemsSource = new[]
                {
                    new { Value = FCQuestionType.ParameterSetting, Display = "参数设置" },
                    new { Value = FCQuestionType.ParameterVerify, Display = "参数验证" },
                    new { Value = FCQuestionType.ParameterCalculation, Display = "参数计算" }
                };
                QuestionTypeComboBox.SelectedValuePath = "Value";
                QuestionTypeComboBox.DisplayMemberPath = "Display";

                // 初始化分类下拉框
                CategoryComboBox.ItemsSource = new[]
                {
                    new { Value = FCQuestionCategory.BasicParameters, Display = "基础参数" },
                    new { Value = FCQuestionCategory.PIDTuning, Display = "PID调节" },
                    new { Value = FCQuestionCategory.SensorCalibration, Display = "传感器校准" },
                    new { Value = FCQuestionCategory.FlightModes, Display = "飞行模式" },
                    new { Value = FCQuestionCategory.SafetySettings, Display = "安全设置" },
                    new { Value = FCQuestionCategory.AdvancedFeatures, Display = "高级功能" }
                };
                CategoryComboBox.SelectedValuePath = "Value";
                CategoryComboBox.DisplayMemberPath = "Display";

                // 初始化难度下拉框
                DifficultyComboBox.ItemsSource = new[]
                {
                    new { Value = QuestionDifficulty.Easy, Display = "简单" },
                    new { Value = QuestionDifficulty.Medium, Display = "中等" },
                    new { Value = QuestionDifficulty.Hard, Display = "困难" }
                };
                DifficultyComboBox.SelectedValuePath = "Value";
                DifficultyComboBox.DisplayMemberPath = "Display";

                // 初始化数据类型下拉框
                DataTypeComboBox.ItemsSource = new[]
                {
                    new { Value = ParameterDataType.Float, Display = "浮点数" },
                    new { Value = ParameterDataType.Integer, Display = "整数" },
                    new { Value = ParameterDataType.Boolean, Display = "布尔值" },
                    new { Value = ParameterDataType.String, Display = "字符串" }
                };
                DataTypeComboBox.SelectedValuePath = "Value";
                DataTypeComboBox.DisplayMemberPath = "Display";

                // 初始化验证方法下拉框
                VerifyMethodComboBox.ItemsSource = new[]
                {
                    new { Value = ParameterVerifyMethod.ExactMatch, Display = "精确匹配" },
                    new { Value = ParameterVerifyMethod.NumericRange, Display = "数值范围" },
                    new { Value = ParameterVerifyMethod.FloatTolerance, Display = "浮点容差" }
                };
                VerifyMethodComboBox.SelectedValuePath = "Value";
                VerifyMethodComboBox.DisplayMemberPath = "Display";

                // 为数据类型变化添加事件处理
                DataTypeComboBox.SelectionChanged += DataTypeComboBox_SelectionChanged;

                // 为验证方法变化添加事件处理
                VerifyMethodComboBox.SelectionChanged += VerifyMethodComboBox_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化控件失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private FlightControlQuestion CreateNewQuestion()
        {
            return new FlightControlQuestion
            {
                Type = FCQuestionType.ParameterSetting,
                Category = FCQuestionCategory.BasicParameters,
                Difficulty = QuestionDifficulty.Medium,
                Points = 5,
                IsActive = true,
                DataType = ParameterDataType.Float,
                VerifyMethod = ParameterVerifyMethod.FloatTolerance,
                RequireFlightControllerRead = true,
                Tolerance = 0.001,
                CreatedBy = _currentUser ?? "未知用户",
                CreatedTime = DateTime.Now,
                LastModified = DateTime.Now,
                LastModifiedBy = _currentUser ?? "未知用户"
            };
        }

        private FlightControlQuestion CloneQuestion(FlightControlQuestion original)
        {
            return new FlightControlQuestion
            {
                Id = original.Id,
                QuestionStatement = original.QuestionStatement,
                Type = original.Type,
                Category = original.Category,
                Difficulty = original.Difficulty,
                Points = original.Points,
                IsActive = original.IsActive,
                ParameterName = original.ParameterName,
                ParameterDescription = original.ParameterDescription,
                DataType = original.DataType,
                CorrectValue = original.CorrectValue,
                Tolerance = original.Tolerance,
                MinValue = original.MinValue,
                MaxValue = original.MaxValue,
                Unit = original.Unit,
                RequireFlightControllerRead = original.RequireFlightControllerRead,
                VerifyMethod = original.VerifyMethod,
                Explanation = original.Explanation,
                CreatedBy = original.CreatedBy,
                CreatedTime = original.CreatedTime,
                LastModified = DateTime.Now,
                LastModifiedBy = _currentUser ?? original.LastModifiedBy,
                UsageCount = original.UsageCount,
                AverageCorrectRate = original.AverageCorrectRate
            };
        }

        #endregion

        #region 数据加载和更新

        private void LoadQuestionData()
        {
            try
            {
                // 设置基本信息
                QuestionIdTextBox.Text = _isEditMode ? _question.Id : "系统自动分配";
                QuestionStatementTextBox.Text = _question.QuestionStatement;
                QuestionTypeComboBox.SelectedValue = _question.Type;
                CategoryComboBox.SelectedValue = _question.Category;
                DifficultyComboBox.SelectedValue = _question.Difficulty;
                PointsTextBox.Text = _question.Points.ToString();
                IsActiveCheckBox.IsChecked = _question.IsActive;

                // 设置参数信息
                ParameterNameTextBox.Text = _question.ParameterName;
                ParameterDescriptionTextBox.Text = _question.ParameterDescription;
                DataTypeComboBox.SelectedValue = _question.DataType;
                CorrectValueTextBox.Text = _question.CorrectValue;
                UnitTextBox.Text = _question.Unit;
                MinValueTextBox.Text = _question.MinValue;
                MaxValueTextBox.Text = _question.MaxValue;
                ToleranceTextBox.Text = _question.Tolerance.ToString(CultureInfo.InvariantCulture);
                VerifyMethodComboBox.SelectedValue = _question.VerifyMethod;

                // 设置验证设置
                RequireFlightControllerReadCheckBox.IsChecked = _question.RequireFlightControllerRead;

                // 设置解析信息
                ExplanationTextBox.Text = _question.Explanation;

                // 设置出题信息
                CreatedByTextBox.Text = _question.CreatedBy;
                CreatedTimeTextBlock.Text = _question.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss");

                // 更新控件状态
                UpdateControlStates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载题目数据失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateQuestionFromControls()
        {
            try
            {
                // 基本信息
                _question.QuestionStatement = QuestionStatementTextBox.Text?.Trim() ?? "";
                _question.Type = (FCQuestionType)(QuestionTypeComboBox.SelectedValue ?? FCQuestionType.ParameterSetting);
                _question.Category = (FCQuestionCategory)(CategoryComboBox.SelectedValue ?? FCQuestionCategory.BasicParameters);
                _question.Difficulty = (QuestionDifficulty)(DifficultyComboBox.SelectedValue ?? QuestionDifficulty.Medium);

                if (int.TryParse(PointsTextBox.Text, out int points))
                    _question.Points = Math.Max(1, points);
                else
                    _question.Points = 5;

                _question.IsActive = IsActiveCheckBox.IsChecked ?? true;

                // 参数信息
                _question.ParameterName = ParameterNameTextBox.Text?.Trim() ?? "";
                _question.ParameterDescription = ParameterDescriptionTextBox.Text?.Trim() ?? "";
                _question.DataType = (ParameterDataType)(DataTypeComboBox.SelectedValue ?? ParameterDataType.Float);
                _question.CorrectValue = CorrectValueTextBox.Text?.Trim() ?? "";
                _question.Unit = UnitTextBox.Text?.Trim() ?? "";
                _question.MinValue = MinValueTextBox.Text?.Trim() ?? "";
                _question.MaxValue = MaxValueTextBox.Text?.Trim() ?? "";

                if (double.TryParse(ToleranceTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double tolerance))
                    _question.Tolerance = Math.Max(0, tolerance);
                else
                    _question.Tolerance = 0.001;

                _question.VerifyMethod = (ParameterVerifyMethod)(VerifyMethodComboBox.SelectedValue ?? ParameterVerifyMethod.ExactMatch);

                // 验证设置
                _question.RequireFlightControllerRead = RequireFlightControllerReadCheckBox.IsChecked ?? true;

                // 解析信息
                _question.Explanation = ExplanationTextBox.Text?.Trim() ?? "";

                // 出题信息
                _question.CreatedBy = CreatedByTextBox.Text?.Trim() ?? _currentUser ?? "未知用户";
                _question.LastModified = DateTime.Now;
                _question.LastModifiedBy = _currentUser ?? "未知用户";
            }
            catch (Exception ex)
            {
                throw new Exception($"更新题目数据失败：{ex.Message}");
            }
        }

        #endregion

        #region 控件状态管理

        private void UpdateControlStates()
        {
            try
            {
                // 根据数据类型启用/禁用相关控件
                var isNumeric = _question.DataType == ParameterDataType.Float ||
                               _question.DataType == ParameterDataType.Integer;

                ToleranceTextBox.IsEnabled = isNumeric && _question.VerifyMethod == ParameterVerifyMethod.FloatTolerance;
                MinValueTextBox.IsEnabled = isNumeric;
                MaxValueTextBox.IsEnabled = isNumeric;

                // 根据验证方法更新提示
                UpdateVerifyMethodHints();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新控件状态失败：{ex.Message}");
            }
        }

        private void UpdateVerifyMethodHints()
        {
            try
            {
                string hint = _question.VerifyMethod switch
                {
                    ParameterVerifyMethod.ExactMatch => "学生答案必须与正确答案完全一致",
                    ParameterVerifyMethod.NumericRange => "学生答案必须在指定的数值范围内",
                    ParameterVerifyMethod.FloatTolerance => "学生答案与正确答案的差值必须在容差范围内",
                    _ => ""
                };

                VerifyMethodComboBox.ToolTip = hint;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新验证方法提示失败：{ex.Message}");
            }
        }

        #endregion

        #region 事件处理

        private void DataTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_question != null && DataTypeComboBox.SelectedValue is ParameterDataType dataType)
                {
                    _question.DataType = dataType;
                    UpdateControlStates();

                    // 根据数据类型设置默认验证方法
                    if (dataType == ParameterDataType.Float)
                    {
                        VerifyMethodComboBox.SelectedValue = ParameterVerifyMethod.FloatTolerance;
                        ToleranceTextBox.Text = "0.001";
                    }
                    else if (dataType == ParameterDataType.Integer)
                    {
                        VerifyMethodComboBox.SelectedValue = ParameterVerifyMethod.ExactMatch;
                        ToleranceTextBox.Text = "0";
                    }
                    else if (dataType == ParameterDataType.Boolean)
                    {
                        VerifyMethodComboBox.SelectedValue = ParameterVerifyMethod.ExactMatch;
                        CorrectValueTextBox.Text = "true";
                    }
                    else // String
                    {
                        VerifyMethodComboBox.SelectedValue = ParameterVerifyMethod.ExactMatch;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据类型变化处理失败：{ex.Message}");
            }
        }

        private void VerifyMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_question != null && VerifyMethodComboBox.SelectedValue is ParameterVerifyMethod method)
                {
                    _question.VerifyMethod = method;
                    UpdateControlStates();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"验证方法变化处理失败：{ex.Message}");
            }
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateQuestionFromControls();

                var validationResult = ValidateQuestion();

                if (validationResult.IsValid)
                {
                    MessageBox.Show("题目验证通过！", "验证结果",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"题目验证失败：\n\n{string.Join("\n", validationResult.Errors)}",
                        "验证结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"验证题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 🚀 全新高性能参数测试功能

        /// <summary>
        /// 🔧 修复版：稳定的参数测试 - 解决连接和读取问题
        /// </summary>
        private async void TestParameter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateQuestionFromControls();

                if (string.IsNullOrWhiteSpace(_question.ParameterName))
                {
                    MessageBox.Show("请先输入参数名称！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 禁用按钮并显示进度
                TestParameterButton.IsEnabled = false;
                TestParameterButton.Content = "连接中...";

                var result = await TestParameterWithRetry(_question.ParameterName);

                if (result.Success)
                {
                    // 显示成功结果
                    var message = $"✅ 参数测试成功！\n\n" +
                                 $"📋 参数名称：{result.ParameterName}\n" +
                                 $"🔢 当前值：{result.CurrentValue}\n" +
                                 $"🏷️ 参数类型：{result.ParameterType}\n" +
                                 $"📝 参数描述：{result.Description}\n" +
                                 $"📏 单位：{result.Unit}\n" +
                                 $"⚖️ 取值范围：{result.Range}";

                    MessageBox.Show(message, "测试成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 自动填充参数信息
                    AutoFillParameterInfo(result);
                }
                else
                {
                    MessageBox.Show($"❌ 参数测试失败：\n\n{result.ErrorMessage}",
                        "测试失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试参数时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestParameterButton.IsEnabled = true;
                TestParameterButton.Content = "测试参数";
            }
        }

        /// <summary>
        /// 🔧 带重试的参数测试 - 提高成功率
        /// </summary>
        private async Task<ParameterTestResult> TestParameterWithRetry(string parameterName, int maxRetries = 2)
        {
            ParameterTestResult lastResult = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 第 {attempt} 次尝试测试参数: {parameterName}");

                    // 更新按钮状态
                    TestParameterButton.Content = attempt == 1 ? "连接中..." : $"重试中({attempt})...";

                    var result = await PerformParameterTest(parameterName);

                    if (result.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 第 {attempt} 次尝试成功");
                        return result;
                    }
                    else
                    {
                        lastResult = result;
                        System.Diagnostics.Debug.WriteLine($"❌ 第 {attempt} 次尝试失败: {result.ErrorMessage}");

                        // 如果不是最后一次尝试，稍等片刻再重试
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(1000);

                            // 清理现有连接，为重试做准备
                            CleanupConnection();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 第 {attempt} 次尝试异常: {ex.Message}");
                    lastResult = new ParameterTestResult
                    {
                        Success = false,
                        ErrorMessage = $"第 {attempt} 次尝试异常: {ex.Message}"
                    };

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(1000);
                        CleanupConnection();
                    }
                }
            }

            return lastResult ?? new ParameterTestResult
            {
                Success = false,
                ErrorMessage = "所有重试都失败了"
            };
        }

        /// <summary>
        /// 🔧 执行参数测试 - 核心逻辑
        /// </summary>
        private async Task<ParameterTestResult> PerformParameterTest(string parameterName)
        {
            var result = new ParameterTestResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Step 1: 建立飞控连接
                if (!await EstablishFlightControllerConnection())
                {
                    result.ErrorMessage = "无法连接到飞控。请检查：\n" +
                                         "• 飞控是否已连接并上电\n" +
                                         "• USB驱动是否正确安装\n" +
                                         "• 串口是否被其他软件占用\n" +
                                         "• 串口配置是否正确";
                    return result;
                }

                TestParameterButton.Content = "读取参数...";

                // Step 2: 读取参数列表
                System.Diagnostics.Debug.WriteLine("📖 开始读取飞控参数列表...");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30秒超时
                var readSuccess = await _parameterService.ReadParametersAsync(cts.Token);

                if (!readSuccess)
                {
                    result.ErrorMessage = "无法从飞控读取参数列表。可能原因：\n" +
                                         "• 飞控通信超时\n" +
                                         "• MAVLink协议版本不兼容\n" +
                                         "• 飞控固件问题";
                    return result;
                }

                var totalParams = _parameterService.Parameters.Count;
                System.Diagnostics.Debug.WriteLine($"📋 成功读取 {totalParams} 个参数");

                if (totalParams == 0)
                {
                    result.ErrorMessage = "飞控返回的参数列表为空。请检查飞控固件是否正常。";
                    return result;
                }

                TestParameterButton.Content = "查找参数...";

                // Step 3: 查找指定参数
                var parameter = _parameterService.Parameters.FirstOrDefault(p =>
                    p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));

                if (parameter == null)
                {
                    // 提供相似参数建议
                    var similarParams = _parameterService.Parameters
                        .Where(p => p.Name.IndexOf(parameterName, StringComparison.OrdinalIgnoreCase) >= 0)
                        .Take(5)
                        .Select(p => p.Name)
                        .ToList();

                    result.ErrorMessage = $"在飞控中未找到参数 '{parameterName}'";

                    if (similarParams.Any())
                    {
                        result.ErrorMessage += $"\n\n相似的参数名称：\n• {string.Join("\n• ", similarParams)}";
                    }
                    else
                    {
                        result.ErrorMessage += $"\n\n建议检查：\n• 参数名称拼写是否正确\n• 参数是否存在于当前固件版本\n• 当前共找到 {totalParams} 个参数";
                    }

                    return result;
                }

                // Step 4: 成功找到参数
                result.Success = true;
                result.ParameterName = parameter.Name;
                result.CurrentValue = parameter.FormatValue();
                result.ParameterType = GetParameterTypeDescription(parameter.Type);
                result.Description = parameter.Description ?? "无描述信息";
                result.Unit = parameter.Units ?? "";
                result.Range = parameter.GetRangeString();
                result.Group = parameter.Group ?? "未分组";

                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"✅ 参数测试成功，总耗时: {stopwatch.ElapsedMilliseconds}ms");

                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                result.ErrorMessage = "参数读取超时，请检查飞控连接";
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"❌ 参数测试异常: {ex.Message}");
                result.ErrorMessage = $"测试过程异常：{ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 🔧 建立飞控连接 - 智能端口检测
        /// </summary>
        private async Task<bool> EstablishFlightControllerConnection()
        {
            // 如果已经连接，先断开
            if (_parameterService != null)
            {
                try
                {
                    if (_parameterService.IsConnected)
                    {
                        await _parameterService.DisconnectAsync();
                    }
                    _parameterService.Dispose();
                }
                catch { }
                _parameterService = null;
            }

            try
            {
                _parameterService = new ArduPilotParameterService();

                // 订阅状态事件
                _parameterService.StatusChanged += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"📊 飞控状态: {e.Type} - {e.Message}");
                };

                // 🔧 智能获取飞控端口配置
                var portConfigs = GetFlightControllerPortConfigs();

                System.Diagnostics.Debug.WriteLine($"🔍 尝试连接飞控，候选端口: {string.Join(", ", portConfigs.Select(p => $"{p.Port}@{p.BaudRate}"))}");

                foreach (var config in portConfigs)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"🔌 尝试连接 {config.Port} @ {config.BaudRate} bps");

                        var connected = await _parameterService.ConnectAsync(config.Port, config.BaudRate);

                        if (connected)
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ 成功连接到 {config.Port} @ {config.BaudRate} bps");

                            // 等待连接稳定
                            await Task.Delay(1000);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ 连接 {config.Port} 失败: {ex.Message}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 建立飞控连接异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 🔧 获取飞控端口配置 - 使用系统串口管理器
        /// </summary>
        private List<(string Port, int BaudRate)> GetFlightControllerPortConfigs()
        {
            var configs = new List<(string Port, int BaudRate)>();

            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 使用串口管理器获取A3飞控连接配置...");

                // 🔧 方法1: 从串口管理器获取A3飞控连接端口配置
                var flightControllerConfig = SerialPortManager.GetConfigByPurpose(SerialPortPurpose.FlightController);

                if (flightControllerConfig != null && flightControllerConfig.IsEnabled)
                {
                    configs.Add((flightControllerConfig.PortName, flightControllerConfig.BaudRate));
                    System.Diagnostics.Debug.WriteLine($"✅ 找到配置的A3飞控端口: {flightControllerConfig.PortName} @ {flightControllerConfig.BaudRate}");
                }                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 获取端口配置失败: {ex.Message}");

                // 🔧 降级方案：直接使用可用端口
                try
                {
                    var availablePorts = ArduPilotParameterService.GetAvailablePorts();
                    if (availablePorts.Contains("COM7"))
                    {
                        configs.Add(("COM7", 115200));
                        System.Diagnostics.Debug.WriteLine("🆘 使用降级方案: COM7@115200");
                    }
                    else if (availablePorts.Any())
                    {
                        var firstPort = availablePorts.First();
                        configs.Add((firstPort, 115200));
                        System.Diagnostics.Debug.WriteLine($"🆘 使用降级方案: {firstPort}@115200");
                    }
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 降级方案也失败: {fallbackEx.Message}");
                }
            }

            return configs;
        }

        /// <summary>
        /// 🔧 清理连接
        /// </summary>
        private void CleanupConnection()
        {
            try
            {
                if (_parameterService != null)
                {
                    if (_parameterService.IsConnected)
                    {
                        _parameterService.DisconnectAsync().Wait(2000);
                    }
                    _parameterService.Dispose();
                    _parameterService = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 🚀 执行快速参数测试 - 多级缓存策略
        /// </summary>
        private async Task<ParameterTestResult> PerformQuickParameterTest(string parameterName)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 🚀 Level 1: 检查快速缓存（最快）
                lock (_cacheLock)
                {
                    if (_quickCache.TryGetValue(parameterName, out var cachedResult))
                    {
                        var age = DateTime.Now - cachedResult.CacheTime;
                        if (age.TotalMinutes < QUICK_CACHE_MINUTES)
                        {
                            stopwatch.Stop();
                            System.Diagnostics.Debug.WriteLine($"⚡ 快速缓存命中，耗时: {stopwatch.ElapsedMilliseconds}ms");
                            return cachedResult;
                        }
                        else
                        {
                            _quickCache.Remove(parameterName);
                        }
                    }
                }

                // 🚀 Level 2: 单参数查询（避免读取全部参数）
                var result = await QuerySingleParameter(parameterName);

                // 🚀 Level 3: 缓存结果
                if (result.Success)
                {
                    lock (_cacheLock)
                    {
                        result.CacheTime = DateTime.Now;
                        _quickCache[parameterName] = result;

                        // 清理过期缓存
                        CleanExpiredCache();
                    }
                }

                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"✅ 参数测试完成，总耗时: {stopwatch.ElapsedMilliseconds}ms");

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"❌ 参数测试异常，耗时: {stopwatch.ElapsedMilliseconds}ms - {ex.Message}");

                return new ParameterTestResult
                {
                    Success = false,
                    ErrorMessage = $"测试过程中发生错误：{ex.Message}"
                };
            }
        }

        /// <summary>
        /// 🚀 单参数查询 - 最小化飞控交互
        /// </summary>
        private async Task<ParameterTestResult> QuerySingleParameter(string parameterName)
        {
            var result = new ParameterTestResult();

            try
            {
                // 确保有可用的服务连接
                if (!await EnsureQuickConnection())
                {
                    result.ErrorMessage = "无法建立飞控连接";
                    return result;
                }

                // 🚀 优化：只请求指定参数，不读取全部参数列表
                var parameter = await RequestSpecificParameter(parameterName);

                if (parameter != null)
                {
                    result.Success = true;
                    result.ParameterName = parameter.Name;
                    result.CurrentValue = parameter.FormatValue();
                    result.ParameterType = GetParameterTypeDescription(parameter.Type);
                    result.Description = parameter.Description ?? "无描述信息";
                    result.Unit = parameter.Units ?? "";
                    result.Range = parameter.GetRangeString();
                    result.Group = parameter.Group ?? "未分组";
                }
                else
                {
                    result.ErrorMessage = $"未找到参数 '{parameterName}'";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"查询参数失败：{ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 🚀 确保快速连接 - 复用现有连接或快速建立新连接
        /// </summary>
        private async Task<bool> EnsureQuickConnection()
        {
            // 如果已有连接，直接使用
            if (_parameterService != null && _parameterService.IsConnected)
            {
                return true;
            }

            // 快速重新连接
            try
            {
                _parameterService?.Dispose();
                _parameterService = new ArduPilotParameterService();

                // 🚀 直接尝试最可能的端口，不遍历所有端口
                var quickPorts = new[] { "COM7" };
                var availablePorts = ArduPilotParameterService.GetAvailablePorts();

                foreach (var port in quickPorts)
                {
                    if (availablePorts.Contains(port))
                    {
                        if (await _parameterService.ConnectAsync(port, 115200))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 🚀 请求特定参数 - 避免读取全部参数
        /// </summary>
        private async Task<ParameterInfo?> RequestSpecificParameter(string parameterName)
        {
            try
            {
                // 如果服务中已有参数缓存，先查找
                if (_parameterService.Parameters.Count > 0)
                {
                    var existing = _parameterService.Parameters.FirstOrDefault(p =>
                        p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        return existing;
                    }
                }

                // 🚀 关键优化：使用超时的参数读取，避免长时间等待
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                // 读取参数列表（但设置较短超时）
                var success = await _parameterService.ReadParametersAsync(cts.Token);

                if (success)
                {
                    return _parameterService.Parameters.FirstOrDefault(p =>
                        p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("参数读取超时");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"请求特定参数失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 🚀 清理过期缓存
        /// </summary>
        private void CleanExpiredCache()
        {
            var expiredKeys = _quickCache
                .Where(kvp => DateTime.Now - kvp.Value.CacheTime > TimeSpan.FromMinutes(QUICK_CACHE_MINUTES))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _quickCache.Remove(key);
            }
        }

        /// <summary>
        /// 将 MAVLink 参数类型转换为可读描述
        /// </summary>
        /// <summary>
        /// 将 MAVLink 参数类型转换为可读描述
        /// </summary>
        private string GetParameterTypeDescription(MAV_PARAM_TYPE mavType)
        {
            return mavType switch
            {
                MAV_PARAM_TYPE.UINT8 => "8位无符号整数 (0-255)",
                MAV_PARAM_TYPE.INT8 => "8位有符号整数 (-128-127)",
                MAV_PARAM_TYPE.UINT16 => "16位无符号整数 (0-65535)",
                MAV_PARAM_TYPE.INT16 => "16位有符号整数 (-32768-32767)",
                MAV_PARAM_TYPE.UINT32 => "32位无符号整数",
                MAV_PARAM_TYPE.INT32 => "32位有符号整数",
                MAV_PARAM_TYPE.REAL32 => "32位浮点数",
                MAV_PARAM_TYPE.REAL64 => "64位浮点数",
                _ => mavType.ToString()
            };
        }

        /// <summary>
        /// 根据测试结果自动填充参数信息
        /// </summary>
        private void AutoFillParameterInfo(ParameterTestResult result)
        {
            try
            {
                // 自动填充参数描述（如果当前为空）
                if (string.IsNullOrWhiteSpace(ParameterDescriptionTextBox.Text) &&
                    !string.IsNullOrWhiteSpace(result.Description))
                {
                    ParameterDescriptionTextBox.Text = result.Description;
                }

                // 自动填充参数单位（如果当前为空）
                if (string.IsNullOrWhiteSpace(UnitTextBox.Text) &&
                    !string.IsNullOrWhiteSpace(result.Unit))
                {
                    UnitTextBox.Text = result.Unit;
                }

                // 如果从飞控读取到了范围信息，解析并填充
                if (!string.IsNullOrWhiteSpace(result.Range) &&
                    string.IsNullOrWhiteSpace(MinValueTextBox.Text) &&
                    string.IsNullOrWhiteSpace(MaxValueTextBox.Text))
                {
                    // 尝试解析范围字符串 [min ~ max] unit
                    if (result.Range.Contains('[') && result.Range.Contains('~'))
                    {
                        var rangePart = result.Range.Substring(result.Range.IndexOf('[') + 1);
                        if (rangePart.Contains(']'))
                        {
                            rangePart = rangePart.Substring(0, rangePart.IndexOf(']'));
                            var parts = rangePart.Split('~');
                            if (parts.Length == 2)
                            {
                                if (float.TryParse(parts[0].Trim(), out float min))
                                    MinValueTextBox.Text = min.ToString(CultureInfo.InvariantCulture);
                                if (float.TryParse(parts[1].Trim(), out float max))
                                    MaxValueTextBox.Text = max.ToString(CultureInfo.InvariantCulture);
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("🔧 已自动填充参数信息");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"自动填充参数信息失败: {ex.Message}");
            }
        }

        #endregion

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateQuestionFromControls();

                var validationResult = ValidateQuestion();

                if (!validationResult.IsValid)
                {
                    MessageBox.Show($"保存失败，题目验证未通过：\n\n{string.Join("\n", validationResult.Errors)}",
                        "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 🔧 修复版资源清理

        /// <summary>
        /// 🔧 快速关闭 - 优化版
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 立即设置窗口结果
                DialogResult = false;

                // 异步清理，不阻塞关闭
                _ = Task.Run(() =>
                {
                    try
                    {
                        CleanupConnection();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"异步清理失败: {ex.Message}");
                    }
                });

                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭失败：{ex.Message}");
                Close();
            }
        }

        #endregion

        #region 验证方法

        private ValidationResult ValidateQuestion()
        {
            var result = new ValidationResult();

            try
            {
                // 验证基本信息
                if (string.IsNullOrWhiteSpace(_question.QuestionStatement))
                    result.Errors.Add("题目陈述不能为空");

                if (string.IsNullOrWhiteSpace(_question.ParameterName))
                    result.Errors.Add("参数名称不能为空");

                if (string.IsNullOrWhiteSpace(_question.CorrectValue))
                    result.Errors.Add("正确答案不能为空");

                if (_question.Points <= 0)
                    result.Errors.Add("分值必须大于0");

                // 验证参数值的有效性
                if (!string.IsNullOrWhiteSpace(_question.CorrectValue))
                {
                    if (!ValidateValueForDataType(_question.CorrectValue, _question.DataType))
                        result.Errors.Add($"正确答案格式不符合{_question.DataType}类型要求");
                }

                // 验证数值范围
                if (_question.DataType == ParameterDataType.Float || _question.DataType == ParameterDataType.Integer)
                {
                    if (!string.IsNullOrWhiteSpace(_question.MinValue) &&
                        !ValidateValueForDataType(_question.MinValue, _question.DataType))
                        result.Errors.Add($"最小值格式不符合{_question.DataType}类型要求");

                    if (!string.IsNullOrWhiteSpace(_question.MaxValue) &&
                        !ValidateValueForDataType(_question.MaxValue, _question.DataType))
                        result.Errors.Add($"最大值格式不符合{_question.DataType}类型要求");

                    // 验证范围逻辑
                    if (!string.IsNullOrWhiteSpace(_question.MinValue) &&
                        !string.IsNullOrWhiteSpace(_question.MaxValue))
                    {
                        if (double.TryParse(_question.MinValue, out double min) &&
                            double.TryParse(_question.MaxValue, out double max))
                        {
                            if (min >= max)
                                result.Errors.Add("最小值必须小于最大值");
                        }
                    }
                }

                // 验证容差设置
                if (_question.VerifyMethod == ParameterVerifyMethod.FloatTolerance)
                {
                    if (_question.Tolerance <= 0)
                        result.Errors.Add("使用浮点容差验证时，容差必须大于0");
                }

                // 验证布尔值设置
                if (_question.DataType == ParameterDataType.Boolean)
                {
                    if (!string.IsNullOrWhiteSpace(_question.CorrectValue))
                    {
                        var value = _question.CorrectValue.ToLower();
                        if (value != "true" && value != "false" && value != "1" && value != "0")
                            result.Errors.Add("布尔类型的正确答案应为 true/false 或 1/0");
                    }
                }

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"验证过程中发生错误：{ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        private bool ValidateValueForDataType(string value, ParameterDataType dataType)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return dataType switch
            {
                ParameterDataType.Float => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                ParameterDataType.Integer => int.TryParse(value, out _),
                ParameterDataType.Boolean => bool.TryParse(value, out _) || value == "1" || value == "0",
                ParameterDataType.String => true, // 字符串总是有效的
                _ => false
            };
        }

        #endregion

        #region 🚀 优化的资源清理

        protected override void OnClosed(EventArgs e)
        {
            // 异步清理，不阻塞
            _ = Task.Run(() =>
            {
                try
                {
                    CleanupConnection();
                    System.Diagnostics.Debug.WriteLine("🧹 已清理参数服务资源");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"清理资源失败：{ex.Message}");
                }
            });

            base.OnClosed(e);
        }

        #endregion
    }

    #region 辅助类

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 🚀 优化的参数测试结果类 - 添加缓存时间
    /// </summary>
    public class ParameterTestResult
    {
        public bool Success { get; set; }
        public string ParameterName { get; set; } = "";
        public string CurrentValue { get; set; } = "";
        public string ParameterType { get; set; } = "";
        public string Description { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Range { get; set; } = "";
        public string Group { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        // 🚀 新增：缓存时间戳
        public DateTime CacheTime { get; set; } = DateTime.Now;
    }
}

    #endregion
#endregion