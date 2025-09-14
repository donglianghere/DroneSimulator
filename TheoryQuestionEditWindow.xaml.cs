using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DroneSimulator
{
    public partial class TheoryQuestionEditWindow : Window
    {
        public TheoryQuestion? Question { get; private set; }
        private List<OptionEditControl> optionControls = new List<OptionEditControl>();
        private bool isEditMode = false;

        public TheoryQuestionEditWindow(TheoryQuestion? question = null, string? currentTeacher = null)
        {
            InitializeComponent();
            InitializeComboBoxes();
            
            if (question != null)
            {
                isEditMode = true;
                Question = question;
                LoadQuestionData(question);
                Title = "编辑理论题目";
            }
            else
            {
                isEditMode = false;
                Question = new TheoryQuestion();
                InitializeNewQuestion(currentTeacher);
                Title = "新增理论题目";
            }
        }

        private void InitializeComboBoxes()
        {
            // 题目类型
            QuestionTypeComboBox.ItemsSource = new[]
            {
                new { Value = TheoryQuestionType.SingleChoice, Display = "单选题" },
                new { Value = TheoryQuestionType.MultipleChoice, Display = "多选题" }
            };
            QuestionTypeComboBox.SelectedValuePath = "Value";
            QuestionTypeComboBox.DisplayMemberPath = "Display";

            // 分类
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

            // 难度
            DifficultyComboBox.ItemsSource = new[]
            {
                new { Value = QuestionDifficulty.Easy, Display = "简单" },
                new { Value = QuestionDifficulty.Medium, Display = "中等" },
                new { Value = QuestionDifficulty.Hard, Display = "困难" }
            };
            DifficultyComboBox.SelectedValuePath = "Value";
            DifficultyComboBox.DisplayMemberPath = "Display";
        }

        private void InitializeNewQuestion(string? currentTeacher)
        {
            QuestionIdTextBox.Text = Guid.NewGuid().ToString();
            QuestionTypeComboBox.SelectedIndex = 0; // 默认单选题
            CategoryComboBox.SelectedIndex = 0; // 默认飞行原理
            DifficultyComboBox.SelectedIndex = 1; // 默认中等难度
            PointsTextBox.Text = "2";
            IsActiveCheckBox.IsChecked = true;
            CreatedByTextBox.Text = currentTeacher ?? "";
            CreatedTimeTextBlock.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 添加默认的两个选项
            AddOptionControl();
            AddOptionControl();
        }

        private void LoadQuestionData(TheoryQuestion question)
        {
            QuestionIdTextBox.Text = question.Id;
            QuestionStatementTextBox.Text = question.QuestionStatement;
            QuestionTypeComboBox.SelectedValue = question.Type;
            CategoryComboBox.SelectedValue = question.Category;
            DifficultyComboBox.SelectedValue = question.Difficulty;
            PointsTextBox.Text = question.Points.ToString();
            IsActiveCheckBox.IsChecked = question.IsActive;
            ExplanationTextBox.Text = question.Explanation;
            CreatedByTextBox.Text = question.CreatedBy;
            CreatedTimeTextBlock.Text = question.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss");

            // 加载选项
            foreach (var option in question.Options)
            {
                AddOptionControl(option);
            }

            // 如果没有选项，至少添加两个空选项
            if (!question.Options.Any())
            {
                AddOptionControl();
                AddOptionControl();
            }
        }

        private void AddOption_Click(object sender, RoutedEventArgs e)
        {
            if (optionControls.Count >= 6)
            {
                MessageBox.Show("最多只能添加6个选项！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AddOptionControl();
        }

        private void RemoveOption_Click(object sender, RoutedEventArgs e)
        {
            if (optionControls.Count <= 2)
            {
                MessageBox.Show("至少需要保留2个选项！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lastControl = optionControls.LastOrDefault();
            if (lastControl != null)
            {
                OptionsPanel.Children.Remove(lastControl);
                optionControls.Remove(lastControl);
            }
        }

        private void AddOptionControl(TheoryOption? option = null)
        {
            var control = new OptionEditControl(option, optionControls.Count + 1);
            optionControls.Add(control);
            OptionsPanel.Children.Add(control);
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            var validationResult = ValidateQuestion();
            if (validationResult.IsValid)
            {
                MessageBox.Show("题目验证通过！", "验证结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"题目验证失败：\n{string.Join("\n", validationResult.Errors)}", 
                    "验证结果", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var validationResult = ValidateQuestion();
            if (!validationResult.IsValid)
            {
                MessageBox.Show($"题目验证失败，无法保存：\n{string.Join("\n", validationResult.Errors)}", 
                    "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                UpdateQuestionFromUI();
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
            DialogResult = false;
            Close();
        }

        private QuestionValidationResult ValidateQuestion()
        {
            var result = new QuestionValidationResult();

            // 验证题目陈述
            if (string.IsNullOrWhiteSpace(QuestionStatementTextBox.Text))
            {
                result.Errors.Add("题目陈述不能为空");
            }

            // 验证分值
            if (!int.TryParse(PointsTextBox.Text, out int points) || points <= 0)
            {
                result.Errors.Add("分值必须是大于0的整数");
            }

            // 验证选项
            var validOptions = optionControls
                .Where(c => !string.IsNullOrWhiteSpace(c.GetOptionText()))
                .ToList();

            if (validOptions.Count < 2)
            {
                result.Errors.Add("至少需要2个有效选项");
            }

            if (validOptions.Count > 6)
            {
                result.Errors.Add("最多只能有6个选项");
            }

            // 验证正确答案
            var correctOptions = validOptions.Where(c => c.IsCorrect()).ToList();
            if (!correctOptions.Any())
            {
                result.Errors.Add("至少需要设置一个正确答案");
            }

            // 单选题只能有一个正确答案
            if (QuestionTypeComboBox.SelectedValue is TheoryQuestionType type && 
                type == TheoryQuestionType.SingleChoice && correctOptions.Count > 1)
            {
                result.Errors.Add("单选题只能有一个正确答案");
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        private void UpdateQuestionFromUI()
        {
            if (Question == null) return;

            Question.Id = QuestionIdTextBox.Text;
            Question.QuestionStatement = QuestionStatementTextBox.Text.Trim();
            Question.Type = (TheoryQuestionType)QuestionTypeComboBox.SelectedValue;
            Question.Category = (TheoryQuestionCategory)CategoryComboBox.SelectedValue;
            Question.Difficulty = (QuestionDifficulty)DifficultyComboBox.SelectedValue;
            Question.Points = int.Parse(PointsTextBox.Text);
            Question.IsActive = IsActiveCheckBox.IsChecked == true;
            Question.Explanation = ExplanationTextBox.Text.Trim();
            Question.CreatedBy = CreatedByTextBox.Text.Trim();
            Question.LastModified = DateTime.Now;

            // 更新选项
            Question.Options.Clear();
            Question.CorrectAnswers.Clear();

            foreach (var control in optionControls)
            {
                var optionText = control.GetOptionText();
                if (!string.IsNullOrWhiteSpace(optionText))
                {
                    var option = new TheoryOption
                    {
                        Text = optionText.Trim(),
                        IsCorrect = control.IsCorrect()
                    };
                    Question.Options.Add(option);

                    if (option.IsCorrect)
                    {
                        Question.CorrectAnswers.Add(option.Text);
                    }
                }
            }
        }

        private class QuestionValidationResult
        {
            public bool IsValid { get; set; } = false;
            public List<string> Errors { get; set; } = new List<string>();
        }
    }

    /// <summary>
    /// 选项编辑控件
    /// </summary>
    public class OptionEditControl : UserControl
    {
        private TextBox optionTextBox;
        private CheckBox correctCheckBox;

        public OptionEditControl(TheoryOption? option = null, int optionNumber = 1)
        {
            InitializeControl(optionNumber);
            
            if (option != null)
            {
                optionTextBox.Text = option.Text;
                correctCheckBox.IsChecked = option.IsCorrect;
            }
        }

        private void InitializeControl(int optionNumber)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            // 选项编号
            var numberLabel = new TextBlock
            {
                Text = $"{(char)('A' + optionNumber - 1)}.",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(5)
            };
            Grid.SetColumn(numberLabel, 0);

            // 选项文本
            optionTextBox = new TextBox
            {
                Margin = new Thickness(5),
                Padding = new Thickness(5),
                FontSize = 12
            };
            Grid.SetColumn(optionTextBox, 1);

            // 正确答案复选框
            correctCheckBox = new CheckBox
            {
                Content = "正确答案",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5)
            };
            Grid.SetColumn(correctCheckBox, 2);

            grid.Children.Add(numberLabel);
            grid.Children.Add(optionTextBox);
            grid.Children.Add(correctCheckBox);

            Content = grid;
            Margin = new Thickness(0, 2, 0, 2);
        }

        public string GetOptionText() => optionTextBox.Text;
        public bool IsCorrect() => correctCheckBox.IsChecked == true;
    }
}