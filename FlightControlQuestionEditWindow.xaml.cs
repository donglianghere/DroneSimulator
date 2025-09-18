using System;
using System.Globalization;
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
        private ParameterService? _parameterService;

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

                TestParameterButton.IsEnabled = false;
                TestParameterButton.Content = "测试中...";

                var result = await TestParameterConnection();

                if (result.Success)
                {
                    MessageBox.Show($"参数测试成功！\n\n" +
                                   $"参数名称：{result.ParameterName}\n" +
                                   $"当前值：{result.CurrentValue}\n" +
                                   $"参数类型：{result.ParameterType}", 
                                   "测试结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"参数测试失败：\n\n{result.ErrorMessage}", 
                        "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取消操作失败：{ex.Message}");
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

        #region 参数测试

        private async Task<ParameterTestResult> TestParameterConnection()
        {
            var result = new ParameterTestResult();

            try
            {
                // 初始化参数服务
                if (_parameterService == null)
                {
                    _parameterService = new ParameterService();
                    
                    // 获取飞控通信配置
                    var flightControllerConfig = SerialPortManager.GetConfigByPurpose(SerialPortPurpose.FlightController);
                    
                    if (flightControllerConfig == null || !flightControllerConfig.IsEnabled)
                    {
                        result.ErrorMessage = "未配置飞控通信端口";
                        return result;
                    }

                    // 连接飞控
                    bool connected = await _parameterService.ConnectAsync(
                        flightControllerConfig.PortName, 
                        flightControllerConfig.BaudRate);

                    if (!connected)
                    {
                        result.ErrorMessage = "无法连接到飞控设备";
                        return result;
                    }
                }

                // 查找参数
                var parameter = _parameterService.Parameters.FirstOrDefault(p => 
                    p.Name.Equals(_question.ParameterName, StringComparison.OrdinalIgnoreCase));

                if (parameter == null)
                {
                    result.ErrorMessage = $"在飞控中未找到参数 {_question.ParameterName}";
                    return result;
                }

                // 获取参数信息
                result.Success = true;
                result.ParameterName = parameter.Name;
                result.CurrentValue = parameter.Value.ToString();
                result.ParameterType = parameter.Type.ToString(); // 转换为字符串
                result.Description = parameter.Description;

                // 自动填充参数描述（如果当前为空）
                if (string.IsNullOrWhiteSpace(_question.ParameterDescription) && 
                    !string.IsNullOrWhiteSpace(parameter.Description))
                {
                    Dispatcher.Invoke(() => {
                        ParameterDescriptionTextBox.Text = parameter.Description;
                    });
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"测试过程中发生错误：{ex.Message}";
            }

            return result;
        }

        #endregion

        #region 资源清理

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _parameterService?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理资源时发生错误：{ex.Message}");
            }
            
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

    public class ParameterTestResult
    {
        public bool Success { get; set; }
        public string ParameterName { get; set; } = "";
        public string CurrentValue { get; set; } = "";
        public string ParameterType { get; set; } = "";  // 改为 string 类型
        public string Description { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }

    #endregion
}