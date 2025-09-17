using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DroneSimulator
{
    /// <summary>
    /// 理论题目编辑窗口
    /// </summary>
    public partial class TheoryQuestionEditWindow : Window
    {
        private TheoryQuestion _question;
        private readonly string? _currentUser;
        private readonly bool _isEditMode;

        /// <summary>
        /// 编辑完成的题目，外部通过此属性获取
        /// </summary>
        public TheoryQuestion? Question { get; private set; }

        public TheoryQuestionEditWindow(TheoryQuestion? question = null, string? currentUser = null)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _isEditMode = question != null;

            // 初始化或复制题目数据
            _question = question != null ? CloneQuestion(question) : CreateNewQuestion();

            InitializeControls();
            LoadQuestionData();
        }

        #region 初始化方法

        /// <summary>
        /// 初始化控件数据源
        /// </summary>
        private void InitializeControls()
        {
            // 初始化题目类型下拉框
            QuestionTypeComboBox.ItemsSource = new[]
            {
                new { Value = TheoryQuestionType.SingleChoice, Display = "单选题" },
                new { Value = TheoryQuestionType.MultipleChoice, Display = "多选题" }
            };
            QuestionTypeComboBox.SelectedValuePath = "Value";
            QuestionTypeComboBox.DisplayMemberPath = "Display";
            QuestionTypeComboBox.SelectionChanged += QuestionTypeComboBox_SelectionChanged;

            // 初始化分类下拉框
            CategoryComboBox.ItemsSource = new[]
            {
                new { Value = TheoryQuestionCategory.FlightPrinciples, Display = "飞行原理" },
                new { Value = TheoryQuestionCategory.Structure, Display = "结构组成" },
                new { Value = TheoryQuestionCategory.ControlAlgorithm, Display = "控制算法" },
                new { Value = TheoryQuestionCategory.SensorFusion, Display = "传感器融合" },
                new { Value = TheoryQuestionCategory.FlightSafety, Display = "飞行安全" },
                new { Value = TheoryQuestionCategory.LawsRegulations, Display = "法律法规" }
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
        }

        /// <summary>
        /// 创建新题目
        /// </summary>
        private TheoryQuestion CreateNewQuestion()
        {
            return new TheoryQuestion
            {
                Type = TheoryQuestionType.SingleChoice,
                Category = TheoryQuestionCategory.FlightPrinciples,
                Difficulty = QuestionDifficulty.Medium,
                Points = 2,
                IsActive = true,
                Options = new List<TheoryOption>
                {
                    new TheoryOption { Text = "选项A" },
                    new TheoryOption { Text = "选项B" }
                },
                CorrectAnswers = new List<string>(),
                CreatedBy = _currentUser ?? "未知用户",
                CreatedTime = DateTime.Now,
                LastModified = DateTime.Now,
                LastModifiedBy = _currentUser ?? "未知用户"
            };
        }

        /// <summary>
        /// 克隆题目（深拷贝）
        /// </summary>
        private TheoryQuestion CloneQuestion(TheoryQuestion original)
        {
            return new TheoryQuestion
            {
                Id = original.Id,
                QuestionStatement = original.QuestionStatement,
                Type = original.Type,
                Category = original.Category,
                Difficulty = original.Difficulty,
                Points = original.Points,
                IsActive = original.IsActive,
                Options = original.Options.Select(o => new TheoryOption
                {
                    Text = o.Text,
                    IsCorrect = o.IsCorrect,
                    Explanation = o.Explanation
                }).ToList(),
                CorrectAnswers = new List<string>(original.CorrectAnswers),
                Explanation = original.Explanation,
                CreatedBy = original.CreatedBy,
                CreatedTime = original.CreatedTime,
                LastModified = DateTime.Now,
                LastModifiedBy = _currentUser ?? original.LastModifiedBy,
                UsageCount = original.UsageCount,
                AverageScore = original.AverageScore
            };
        }

        #endregion

        #region 数据加载和更新

        /// <summary>
        /// 加载题目数据到控件
        /// </summary>
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
                ExplanationTextBox.Text = _question.Explanation;
                CreatedByTextBox.Text = _question.CreatedBy;
                CreatedTimeTextBlock.Text = _question.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss");

                // 编辑模式下，确保选项的 IsCorrect 状态与 CorrectAnswers 列表一致
                if (_isEditMode)
                {
                    foreach (var option in _question.Options)
                    {
                        option.IsCorrect = _question.CorrectAnswers.Contains(option.Text);
                    }
                }

                // 加载选项
                RefreshOptionsPanel();

                // 设置窗口标题
                Title = _isEditMode ? $"编辑题目 - {_question.Id}" : "新增题目";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载题目数据失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新选项面板
        /// </summary>
        private void RefreshOptionsPanel()
        {
            OptionsPanel.Children.Clear();

            for (int i = 0; i < _question.Options.Count; i++)
            {
                var optionPanel = CreateOptionPanel(_question.Options[i], i);
                OptionsPanel.Children.Add(optionPanel);
            }

            // 修复：编辑模式下需要更新正确答案的选中状态
            if (_isEditMode)
            {
                UpdateCorrectAnswerSelection();
            }
        }

        /// <summary>
        /// 创建单个选项面板
        /// </summary>
        private StackPanel CreateOptionPanel(TheoryOption option, int index)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };

            // 选项编号
            var label = new TextBlock
            {
                Text = $"{(char)('A' + index)}:",
                Width = 20,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold
            };

            // 选项文本框
            var textBox = new TextBox
            {
                Text = option.Text,
                Width = 300,
                Margin = new Thickness(5, 0, 5, 0),
                Tag = option // 将选项对象存储在Tag中
            };
            textBox.TextChanged += OptionTextBox_TextChanged;

            // 正确答案复选框
            var checkBox = new CheckBox
            {
                Content = "正确答案",
                IsChecked = option.IsCorrect,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0),
                Tag = option
            };
            checkBox.Checked += CorrectAnswerCheckBox_Changed;
            checkBox.Unchecked += CorrectAnswerCheckBox_Changed;

            // 删除按钮
            var deleteButton = new Button
            {
                Content = "删除",
                Width = 50,
                Height = 25,
                Background = System.Windows.Media.Brushes.LightCoral,
                Margin = new Thickness(5, 0, 0, 0),
                Tag = option
            };
            deleteButton.Click += DeleteOption_Click;

            panel.Children.Add(label);
            panel.Children.Add(textBox);
            panel.Children.Add(checkBox);

            // 至少保留2个选项，不能删除
            if (_question.Options.Count > 2)
            {
                panel.Children.Add(deleteButton);
            }

            return panel;
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 题目类型改变事件
        /// </summary>
        private void QuestionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuestionTypeComboBox.SelectedValue is TheoryQuestionType selectedType)
            {
                _question.Type = selectedType;

                // 根据题目类型调整分值
                if (selectedType == TheoryQuestionType.MultipleChoice)
                {
                    _question.Points = 3;
                    PointsTextBox.Text = "3";
                }
                else
                {
                    _question.Points = 2;
                    PointsTextBox.Text = "2";
                }

                // 刷新选项面板以更新复选框行为
                RefreshOptionsPanel();
            }
        }

        /// <summary>
        /// 选项文本改变事件
        /// </summary>
        private void OptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TheoryOption option)
            {
                option.Text = textBox.Text;
                // 实时更新正确答案列表（如果选项文本改变）
                UpdateCorrectAnswersFromUI();
            }
        }

        /// <summary>
        /// 正确答案复选框改变事件 - 修复版
        /// </summary>
        private void CorrectAnswerCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is TheoryOption option)
            {
                // 单选题只允许选择一个答案
                if (_question.Type == TheoryQuestionType.SingleChoice && checkBox.IsChecked == true)
                {
                    // 清除其他选项的选中状态（在界面和数据模型中都清除）
                    ClearOtherCorrectAnswers(option);
                }

                // 立即同步复选框状态到数据模型
                option.IsCorrect = checkBox.IsChecked == true;

                // 立即更新正确答案列表
                UpdateCorrectAnswersFromUI();
            }
        }

        /// <summary>
        /// 清除除指定选项外的所有正确答案状态
        /// </summary>
        private void ClearOtherCorrectAnswers(TheoryOption exceptOption)
        {
            // 清除数据模型中的状态
            foreach (var opt in _question.Options)
            {
                if (opt != exceptOption)
                {
                    opt.IsCorrect = false;
                }
            }

            // 清除界面中的复选框状态
            foreach (StackPanel panel in OptionsPanel.Children)
            {
                var checkBox = panel.Children.OfType<CheckBox>().FirstOrDefault();
                if (checkBox?.Tag is TheoryOption panelOption && panelOption != exceptOption)
                {
                    // 临时移除事件处理器，避免递归调用
                    checkBox.Checked -= CorrectAnswerCheckBox_Changed;
                    checkBox.Unchecked -= CorrectAnswerCheckBox_Changed;

                    checkBox.IsChecked = false;

                    // 重新添加事件处理器
                    checkBox.Checked += CorrectAnswerCheckBox_Changed;
                    checkBox.Unchecked += CorrectAnswerCheckBox_Changed;
                }
            }
        }

        /// <summary>
        /// 从界面实时更新正确答案列表 - 新增方法
        /// </summary>
        private void UpdateCorrectAnswersFromUI()
        {
            _question.CorrectAnswers.Clear();

            // 从界面复选框读取实际状态
            foreach (StackPanel panel in OptionsPanel.Children)
            {
                var checkBox = panel.Children.OfType<CheckBox>().FirstOrDefault();
                var textBox = panel.Children.OfType<TextBox>().FirstOrDefault();

                if (checkBox?.IsChecked == true && textBox?.Tag is TheoryOption option)
                {
                    // 确保数据模型同步
                    option.IsCorrect = true;
                    _question.CorrectAnswers.Add(option.Text);
                }
            }
        }

        /// <summary>
        /// 更新正确答案列表 - 保持向后兼容
        /// </summary>
        private void UpdateCorrectAnswers()
        {
            UpdateCorrectAnswersFromUI();
        }

        /// <summary>
        /// 更新正确答案的选中状态 - 改进版
        /// </summary>
        private void UpdateCorrectAnswerSelection()
        {
            // 首先根据 CorrectAnswers 列表更新 Options 的 IsCorrect 状态
            foreach (var option in _question.Options)
            {
                option.IsCorrect = _question.CorrectAnswers.Contains(option.Text);
            }

            // 然后同步界面复选框状态
            foreach (StackPanel panel in OptionsPanel.Children)
            {
                var checkBox = panel.Children.OfType<CheckBox>().FirstOrDefault();
                if (checkBox?.Tag is TheoryOption option)
                {
                    // 临时移除事件处理器，避免触发事件
                    checkBox.Checked -= CorrectAnswerCheckBox_Changed;
                    checkBox.Unchecked -= CorrectAnswerCheckBox_Changed;

                    checkBox.IsChecked = option.IsCorrect;

                    // 重新添加事件处理器
                    checkBox.Checked += CorrectAnswerCheckBox_Changed;
                    checkBox.Unchecked += CorrectAnswerCheckBox_Changed;
                }
            }
        }

        /// <summary>
        /// 添加选项
        /// </summary>
        private void AddOption_Click(object sender, RoutedEventArgs e)
        {
            if (_question.Options.Count >= 6)
            {
                MessageBox.Show("最多只能添加6个选项！", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newOption = new TheoryOption
            {
                Text = $"选项{(char)('A' + _question.Options.Count)}"
            };

            _question.Options.Add(newOption);
            RefreshOptionsPanel();
        }

        /// <summary>
        /// 删除选项
        /// </summary>
        private void RemoveOption_Click(object sender, RoutedEventArgs e)
        {
            if (_question.Options.Count <= 2)
            {
                MessageBox.Show("至少需要保留2个选项！", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 删除最后一个选项
            var lastOption = _question.Options.Last();

            // 如果删除的是正确答案，需要更新正确答案列表
            if (lastOption.IsCorrect)
            {
                _question.CorrectAnswers.Remove(lastOption.Text);
            }

            _question.Options.Remove(lastOption);
            RefreshOptionsPanel();
        }

        /// <summary>
        /// 删除特定选项
        /// </summary>
        private void DeleteOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TheoryOption option)
            {
                if (_question.Options.Count <= 2)
                {
                    MessageBox.Show("至少需要保留2个选项！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 如果删除的是正确答案，需要更新正确答案列表
                if (option.IsCorrect)
                {
                    _question.CorrectAnswers.Remove(option.Text);
                }

                _question.Options.Remove(option);
                RefreshOptionsPanel();
            }
        }

        #endregion

        #region 验证和保存

        /// <summary>
        /// 验证题目按钮点击事件
        /// </summary>
        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            var validationResult = ValidateQuestion();

            if (validationResult.IsValid)
            {
                MessageBox.Show("题目验证通过！✓", "验证成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"题目验证失败：\n\n{string.Join("\n", validationResult.Errors)}",
                    "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 验证题目合规性 - 修复版
        /// </summary>
        private ValidationResult ValidateQuestion()
        {
            var result = new ValidationResult();

            try
            {
                // 收集当前界面数据
                CollectFormData();

                // 1. 检查题目陈述
                if (string.IsNullOrWhiteSpace(_question.QuestionStatement))
                {
                    result.Errors.Add("• 题目陈述不能为空");
                }
                else if (_question.QuestionStatement.Length < 5)
                {
                    result.Errors.Add("• 题目陈述长度不能少于5个字符");
                }

                // 2. 检查选项数量
                if (_question.Options.Count < 2)
                {
                    result.Errors.Add("• 至少需要2个选项");
                }
                else if (_question.Options.Count > 6)
                {
                    result.Errors.Add("• 最多只能有6个选项");
                }

                // 3. 检查选项内容
                var emptyOptions = _question.Options.Where(o => string.IsNullOrWhiteSpace(o.Text)).Count();
                if (emptyOptions > 0)
                {
                    result.Errors.Add($"• 有 {emptyOptions} 个选项内容为空");
                }

                // 检查选项重复
                var duplicateOptions = _question.Options.GroupBy(o => o.Text.Trim())
                    .Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key))
                    .Select(g => g.Key).ToList();
                if (duplicateOptions.Any())
                {
                    result.Errors.Add($"• 存在重复选项：{string.Join(", ", duplicateOptions)}");
                }

                // 4. 检查正确答案 - 关键修复：从界面实时读取复选框状态
                UpdateCorrectAnswersFromUI(); // 确保从界面获取最新状态

                var correctCount = _question.Options.Count(o => o.IsCorrect);

                if (correctCount == 0)
                {
                    result.Errors.Add("• 必须至少选择一个正确答案");
                }
                else if (_question.Type == TheoryQuestionType.SingleChoice && correctCount > 1)
                {
                    result.Errors.Add("• 单选题只能有一个正确答案");
                }
                else if (_question.Type == TheoryQuestionType.MultipleChoice && correctCount < 1)
                {
                    result.Errors.Add("• 多选题必须至少有一个正确答案");
                }

                // 5. 检查分值
                if (_question.Points <= 0)
                {
                    result.Errors.Add("• 题目分值必须大于0");
                }

                // 6. 检查出题人
                if (string.IsNullOrWhiteSpace(_question.CreatedBy))
                {
                    result.Errors.Add("• 出题人不能为空");
                }

                result.IsValid = !result.Errors.Any();
            }
            catch (Exception ex)
            {
                result.Errors.Add($"• 验证过程中发生错误：{ex.Message}");
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// 收集表单数据 - 修复版
        /// </summary>
        private void CollectFormData()
        {
            _question.QuestionStatement = QuestionStatementTextBox.Text?.Trim() ?? "";

            if (QuestionTypeComboBox.SelectedValue is TheoryQuestionType type)
                _question.Type = type;

            if (CategoryComboBox.SelectedValue is TheoryQuestionCategory category)
                _question.Category = category;

            if (DifficultyComboBox.SelectedValue is QuestionDifficulty difficulty)
                _question.Difficulty = difficulty;

            if (int.TryParse(PointsTextBox.Text, out int points))
                _question.Points = points;

            _question.IsActive = IsActiveCheckBox.IsChecked == true;
            _question.Explanation = ExplanationTextBox.Text?.Trim() ?? "";
            _question.CreatedBy = CreatedByTextBox.Text?.Trim() ?? "";

            // 关键修复：从界面实时读取复选框状态
            UpdateCorrectAnswersFromUI();
        }

        /// <summary>
        /// 保存按钮点击事件
        /// </summary>
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 先验证题目
                var validationResult = ValidateQuestion();
                if (!validationResult.IsValid)
                {
                    var message = "题目验证失败，无法保存：\n\n" + string.Join("\n", validationResult.Errors);
                    MessageBox.Show(message, "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 收集最新数据
                CollectFormData();

                // 如果是新增题目，分配系统ID
                if (!_isEditMode)
                {
                    TheoryQuestionBankManager.AssignSystemId(_question);
                }

                // 设置最后修改信息
                _question.LastModified = DateTime.Now;
                _question.LastModifiedBy = _currentUser ?? "未知用户";

                // 将结果题目赋值给公共属性
                Question = _question;

                // 设置对话框结果并关闭
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存题目失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region 辅助类

        /// <summary>
        /// 验证结果类
        /// </summary>
        private class ValidationResult
        {
            public bool IsValid { get; set; } = true;
            public List<string> Errors { get; set; } = new();
        }

        #endregion
    }
}