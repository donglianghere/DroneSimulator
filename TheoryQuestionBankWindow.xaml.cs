using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DroneSimulator
{
    public partial class TheoryQuestionBankWindow : Window
    {
        private List<TheoryQuestion> allQuestions = new();
        private List<TheoryQuestion> filteredQuestions = new();
        private string? currentTeacher;

        public TheoryQuestionBankWindow(string? teacherName = null)
        {
            try
            {
                InitializeComponent();
                currentTeacher = teacherName;

                // 延迟初始化，确保所有控件都已加载
                this.Loaded += TheoryQuestionBankWindow_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"窗口初始化失败：{ex.Message}\n\n堆栈跟踪：{ex.StackTrace}",
                    "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TheoryQuestionBankWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeFilters();
                LoadQuestions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败：{ex.Message}", "加载错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeFilters()
        {
            try
            {
                // 安全检查控件是否存在
                if (TypeFilterComboBox == null || CategoryFilterComboBox == null || DifficultyFilterComboBox == null)
                {
                    throw new InvalidOperationException("筛选控件未正确初始化");
                }

                // 题目类型筛选
                TypeFilterComboBox.ItemsSource = new[]
                {
                    new { Value = (TheoryQuestionType?)null, Display = "全部类型" },
                    new { Value = (TheoryQuestionType?)TheoryQuestionType.SingleChoice, Display = "单选题" },
                    new { Value = (TheoryQuestionType?)TheoryQuestionType.MultipleChoice, Display = "多选题" }
                };
                TypeFilterComboBox.SelectedValuePath = "Value";
                TypeFilterComboBox.DisplayMemberPath = "Display";
                TypeFilterComboBox.SelectedIndex = 0;

                // 分类筛选
                CategoryFilterComboBox.ItemsSource = new[]
                {
                    new { Value = (TheoryQuestionCategory?)null, Display = "全部分类" },
                    new { Value = (TheoryQuestionCategory?)TheoryQuestionCategory.FlightPrinciples, Display = "飞行原理" },
                    new { Value = (TheoryQuestionCategory?)TheoryQuestionCategory.Structure, Display = "结构组成" },
                    new { Value = (TheoryQuestionCategory?)TheoryQuestionCategory.ControlAlgorithm, Display = "控制算法" },
                    new { Value = (TheoryQuestionCategory?)TheoryQuestionCategory.SensorFusion, Display = "传感器融合" },
                    new { Value = (TheoryQuestionCategory?)TheoryQuestionCategory.FlightSafety, Display = "飞行安全" },
                    new { Value = (TheoryQuestionCategory?)TheoryQuestionCategory.LawsRegulations, Display = "法律法规" }
                };
                CategoryFilterComboBox.SelectedValuePath = "Value";
                CategoryFilterComboBox.DisplayMemberPath = "Display";
                CategoryFilterComboBox.SelectedIndex = 0;

                // 难度筛选
                DifficultyFilterComboBox.ItemsSource = new[]
                {
                    new { Value = (QuestionDifficulty?)null, Display = "全部难度" },
                    new { Value = (QuestionDifficulty?)QuestionDifficulty.Easy, Display = "简单" },
                    new { Value = (QuestionDifficulty?)QuestionDifficulty.Medium, Display = "中等" },
                    new { Value = (QuestionDifficulty?)QuestionDifficulty.Hard, Display = "困难" }
                };
                DifficultyFilterComboBox.SelectedValuePath = "Value";
                DifficultyFilterComboBox.DisplayMemberPath = "Display";
                DifficultyFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                throw new Exception($"初始化筛选器失败：{ex.Message}", ex);
            }
        }

        private void LoadQuestions()
        {
            try
            {
                // 安全检查状态文本控件
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "正在加载题库...";
                }

                allQuestions = TheoryQuestionBankManager.GetAllQuestions();

                ApplyFilters();

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "题库加载完成";
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"加载题库失败：{ex.Message}";

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "加载失败";
                }

                MessageBox.Show(errorMessage, "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            try
            {
                filteredQuestions = allQuestions.ToList();

                // 安全检查搜索框
                if (SearchTextBox != null)
                {
                    var searchText = SearchTextBox.Text?.Trim().ToLower();
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        filteredQuestions = filteredQuestions.Where(q =>
                            q.QuestionStatement.ToLower().Contains(searchText) ||
                            q.Options.Any(o => o.Text.ToLower().Contains(searchText))
                        ).ToList();
                    }
                }

                // 类型筛选 - 安全检查
                if (TypeFilterComboBox?.SelectedValue is TheoryQuestionType selectedType)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.Type == selectedType).ToList();
                }

                // 分类筛选 - 安全检查
                if (CategoryFilterComboBox?.SelectedValue is TheoryQuestionCategory selectedCategory)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.Category == selectedCategory).ToList();
                }

                // 难度筛选 - 安全检查
                if (DifficultyFilterComboBox?.SelectedValue is QuestionDifficulty selectedDifficulty)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.Difficulty == selectedDifficulty).ToList();
                }

                // 仅显示启用的题目 - 安全检查
                if (OnlyActiveCheckBox?.IsChecked == true)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.IsActive).ToList();
                }

                // 更新数据网格 - 安全检查
                if (QuestionsDataGrid != null)
                {
                    QuestionsDataGrid.ItemsSource = filteredQuestions;
                }

                // 更新统计文本 - 安全检查
                if (CountTextBlock != null)
                {
                    CountTextBlock.Text = $"总计: {filteredQuestions.Count} 题 (共 {allQuestions.Count} 题)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"筛选题目时发生错误：{ex.Message}", "筛选错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddQuestion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new TheoryQuestionEditWindow(null, currentTeacher);
                if (dialog.ShowDialog() == true && dialog.Question != null)
                {
                    if (TheoryQuestionBankManager.SaveQuestion(dialog.Question))
                    {
                        if (StatusTextBlock != null)
                        {
                            StatusTextBlock.Text = "题目添加成功";
                        }
                        LoadQuestions();
                    }
                    else
                    {
                        MessageBox.Show("保存题目失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditQuestion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (QuestionsDataGrid?.SelectedItem is not TheoryQuestion selectedQuestion)
                {
                    MessageBox.Show("请先选择要编辑的题目！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new TheoryQuestionEditWindow(selectedQuestion, currentTeacher);
                if (dialog.ShowDialog() == true && dialog.Question != null)
                {
                    if (TheoryQuestionBankManager.SaveQuestion(dialog.Question))
                    {
                        if (StatusTextBlock != null)
                        {
                            StatusTextBlock.Text = "题目编辑成功";
                        }
                        LoadQuestions();
                    }
                    else
                    {
                        MessageBox.Show("保存题目失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteQuestion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (QuestionsDataGrid?.SelectedItem is not TheoryQuestion selectedQuestion)
                {
                    MessageBox.Show("请先选择要删除的题目！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"确定要删除题目\n\n\"{selectedQuestion.QuestionStatement}\"\n\n这个操作不能撤销！",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (TheoryQuestionBankManager.DeleteQuestion(selectedQuestion.Id))
                    {
                        if (StatusTextBlock != null)
                        {
                            StatusTextBlock.Text = "题目删除成功";
                        }
                        LoadQuestions();
                    }
                    else
                    {
                        MessageBox.Show("删除题目失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Title = "导入题库文件",
                    Filter = "JSON文件 (*.json)|*.json|CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (openDialog.ShowDialog() == true)
                {
                    MessageBox.Show("导入功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入操作失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "导出题库",
                    Filter = "JSON文件 (*.json)|*.json|CSV文件 (*.csv)|*.csv",
                    DefaultExt = "json",
                    FileName = $"理论题库导出_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    MessageBox.Show("导出功能开发中...", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出操作失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"搜索筛选失败：{ex.Message}");
            }
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"筛选器更改失败：{ex.Message}");
            }
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SearchTextBox != null) SearchTextBox.Text = "";
                if (TypeFilterComboBox != null) TypeFilterComboBox.SelectedIndex = 0;
                if (CategoryFilterComboBox != null) CategoryFilterComboBox.SelectedIndex = 0;
                if (DifficultyFilterComboBox != null) DifficultyFilterComboBox.SelectedIndex = 0;
                if (OnlyActiveCheckBox != null) OnlyActiveCheckBox.IsChecked = true;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除筛选时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TheoryQuestionBankManager.ReloadQuestions();
                LoadQuestions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新题库时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QuestionsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                EditQuestion_Click(sender, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"双击编辑失败：{ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭窗口失败：{ex.Message}");
                // 强制关闭
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    this.Close();
                }));
            }
        }
    }
}