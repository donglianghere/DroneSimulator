using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DroneSimulator
{
    public partial class FlightControlQuestionBankWindow : Window
    {
        private List<FlightControlQuestion> allQuestions = new();
        private List<FlightControlQuestion> filteredQuestions = new();
        private string? currentTeacher;
        private CancellationTokenSource _importCancellationTokenSource;

        public FlightControlQuestionBankWindow(string? teacherName = null)
        {
            try
            {
                InitializeComponent();
                currentTeacher = teacherName;

                this.ShowInTaskbar = true;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                this.Loaded += FlightControlQuestionBankWindow_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"窗口初始化失败：{ex.Message}", "初始化错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FlightControlQuestionBankWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeFilters();
                LoadQuestions();
                EnsureNoQuestionsSelected();
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
                // 题目类型筛选
                TypeFilterComboBox.ItemsSource = new[]
                {
                    new { Value = (FCQuestionType?)null, Display = "全部类型" },
                    new { Value = (FCQuestionType?)FCQuestionType.ParameterSetting, Display = "参数设置" },
                    new { Value = (FCQuestionType?)FCQuestionType.ParameterVerify, Display = "参数验证" },
                    new { Value = (FCQuestionType?)FCQuestionType.ParameterCalculation, Display = "参数计算" }
                };
                TypeFilterComboBox.SelectedValuePath = "Value";
                TypeFilterComboBox.DisplayMemberPath = "Display";
                TypeFilterComboBox.SelectedIndex = 0;

                // 分类筛选
                CategoryFilterComboBox.ItemsSource = new[]
                {
                    new { Value = (FCQuestionCategory?)null, Display = "全部分类" },
                    new { Value = (FCQuestionCategory?)FCQuestionCategory.BasicParameters, Display = "基础参数" },
                    new { Value = (FCQuestionCategory?)FCQuestionCategory.PIDTuning, Display = "PID调节" },
                    new { Value = (FCQuestionCategory?)FCQuestionCategory.SensorCalibration, Display = "传感器校准" },
                    new { Value = (FCQuestionCategory?)FCQuestionCategory.FlightModes, Display = "飞行模式" },
                    new { Value = (FCQuestionCategory?)FCQuestionCategory.SafetySettings, Display = "安全设置" },
                    new { Value = (FCQuestionCategory?)FCQuestionCategory.AdvancedFeatures, Display = "高级功能" }
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

                // 数据类型筛选
                DataTypeFilterComboBox.ItemsSource = new[]
                {
                    new { Value = (ParameterDataType?)null, Display = "全部类型" },
                    new { Value = (ParameterDataType?)ParameterDataType.Float, Display = "浮点数" },
                    new { Value = (ParameterDataType?)ParameterDataType.Integer, Display = "整数" },
                    new { Value = (ParameterDataType?)ParameterDataType.Boolean, Display = "布尔值" },
                    new { Value = (ParameterDataType?)ParameterDataType.String, Display = "字符串" }
                };
                DataTypeFilterComboBox.SelectedValuePath = "Value";
                DataTypeFilterComboBox.DisplayMemberPath = "Display";
                DataTypeFilterComboBox.SelectedIndex = 0;
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
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "正在加载数据...";
                }

                // 安全获取所有题目
                var loadedQuestions = FlightControlQuestionBankManager.GetAllQuestions();
                allQuestions = loadedQuestions ?? new List<FlightControlQuestion>();

                // 验证加载的题目数据完整性
                int invalidCount = 0;
                var validQuestions = new List<FlightControlQuestion>();

                foreach (var question in allQuestions)
                {
                    if (question != null && !string.IsNullOrEmpty(question.Id))
                    {
                        // 确保关键属性不为空
                        if (string.IsNullOrEmpty(question.QuestionStatement))
                            question.QuestionStatement = "[题目陈述为空]";
                        if (string.IsNullOrEmpty(question.ParameterName))
                            question.ParameterName = "[参数名称为空]";
                        if (string.IsNullOrEmpty(question.ParameterDescription))
                            question.ParameterDescription = "";

                        validQuestions.Add(question);
                    }
                    else
                    {
                        invalidCount++;
                        System.Diagnostics.Debug.WriteLine($"发现无效题目: {question?.Id ?? "null"}");
                    }
                }

                allQuestions = validQuestions;

                if (invalidCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"过滤了 {invalidCount} 个无效题目");
                }

                ApplyFilters();

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = invalidCount > 0
                        ? $"数据加载完成 (过滤了 {invalidCount} 个无效题目)"
                        : "数据加载完成";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadQuestions 失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");

                // 发生异常时的安全处理
                allQuestions = new List<FlightControlQuestion>();
                filteredQuestions = new List<FlightControlQuestion>();

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "加载失败";
                }

                MessageBox.Show($"加载数据失败：{ex.Message}\n\n可能原因:\n" +
                               "1. 题库文件损坏\n" +
                               "2. 文件权限不足\n" +
                               "3. 磁盘空间不足\n\n" +
                               "建议:\n" +
                               "• 检查程序运行权限\n" +
                               "• 重启应用程序\n" +
                               "• 联系技术支持", "加载错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            try
            {
                // 1. 确保基础集合不为空
                if (allQuestions == null)
                {
                    allQuestions = new List<FlightControlQuestion>();
                }

                filteredQuestions = new List<FlightControlQuestion>(allQuestions);

                // 2. 搜索筛选 - 添加空值安全检查
                var searchText = SearchTextBox?.Text?.Trim()?.ToLower();
                if (!string.IsNullOrEmpty(searchText))
                {
                    filteredQuestions = filteredQuestions.Where(q => q != null && (
                        (!string.IsNullOrEmpty(q.QuestionStatement) && q.QuestionStatement.ToLower().Contains(searchText)) ||
                        (!string.IsNullOrEmpty(q.ParameterName) && q.ParameterName.ToLower().Contains(searchText)) ||
                        (!string.IsNullOrEmpty(q.ParameterDescription) && q.ParameterDescription.ToLower().Contains(searchText))
                    )).ToList();
                }

                // 3. 类型筛选 - 安全访问 SelectedValue
                try
                {
                    if (TypeFilterComboBox?.SelectedValue is FCQuestionType selectedType)
                    {
                        filteredQuestions = filteredQuestions.Where(q => q != null && q.Type == selectedType).ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"类型筛选失败: {ex.Message}");
                }

                // 4. 分类筛选 - 安全访问 SelectedValue
                try
                {
                    if (CategoryFilterComboBox?.SelectedValue is FCQuestionCategory selectedCategory)
                    {
                        filteredQuestions = filteredQuestions.Where(q => q != null && q.Category == selectedCategory).ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"分类筛选失败: {ex.Message}");
                }

                // 5. 难度筛选 - 安全访问 SelectedValue
                try
                {
                    if (DifficultyFilterComboBox?.SelectedValue is QuestionDifficulty selectedDifficulty)
                    {
                        filteredQuestions = filteredQuestions.Where(q => q != null && q.Difficulty == selectedDifficulty).ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"难度筛选失败: {ex.Message}");
                }

                // 6. 数据类型筛选 - 安全访问 SelectedValue
                try
                {
                    if (DataTypeFilterComboBox?.SelectedValue is ParameterDataType selectedDataType)
                    {
                        filteredQuestions = filteredQuestions.Where(q => q != null && q.DataType == selectedDataType).ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"数据类型筛选失败: {ex.Message}");
                }

                // 7. 仅显示启用的题目 - 安全检查
                try
                {
                    if (OnlyActiveCheckBox?.IsChecked == true)
                    {
                        filteredQuestions = filteredQuestions.Where(q => q != null && q.IsActive).ToList();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"启用状态筛选失败: {ex.Message}");
                }

                // 8. 确保筛选结果不为空
                if (filteredQuestions == null)
                {
                    filteredQuestions = new List<FlightControlQuestion>();
                }

                // 9. 安全更新数据网格
                try
                {
                    if (QuestionsDataGrid != null)
                    {
                        QuestionsDataGrid.ItemsSource = filteredQuestions;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新数据网格失败: {ex.Message}");
                }

                // 10. 安全更新统计文本
                try
                {
                    if (CountTextBlock != null)
                    {
                        CountTextBlock.Text = $"总计: {filteredQuestions.Count} 题 (共 {allQuestions.Count} 题)";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新统计文本失败: {ex.Message}");
                }

                // 11. 安全更新选择状态
                try
                {
                    UpdateSelectionStatus();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新选择状态失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyFilters 方法发生未处理的异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");

                // 发生异常时的安全处理
                filteredQuestions = allQuestions?.ToList() ?? new List<FlightControlQuestion>();

                if (CountTextBlock != null)
                {
                    CountTextBlock.Text = $"筛选出错，显示全部: {filteredQuestions.Count} 题";
                }

                if (QuestionsDataGrid != null)
                {
                    QuestionsDataGrid.ItemsSource = filteredQuestions;
                }

                MessageBox.Show($"筛选题目时发生错误：{ex.Message}\n\n已显示全部题目。", "筛选错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EnsureNoQuestionsSelected()
        {
            try
            {
                foreach (var question in allQuestions)
                {
                    question.IsSelected = false;
                }
                foreach (var question in filteredQuestions)
                {
                    question.IsSelected = false;
                }
                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除题目选中状态失败：{ex.Message}");
            }
        }

        private void UpdateSelectionStatus()
        {
            try
            {
                // 确保集合不为空
                if (filteredQuestions == null)
                {
                    filteredQuestions = new List<FlightControlQuestion>();
                }

                var checkboxSelected = filteredQuestions.Where(q => q?.IsSelected == true).ToList();
                var dataGridSelected = QuestionsDataGrid?.SelectedItems?.Cast<FlightControlQuestion>()?.Where(q => q != null)?.ToList()
                                      ?? new List<FlightControlQuestion>();

                int checkboxCount = checkboxSelected.Count;
                int dataGridCount = dataGridSelected.Count;
                int totalCount = filteredQuestions.Count;

                // 安全更新选择计数显示
                if (SelectedCountText != null)
                {
                    if (checkboxCount > 0 && dataGridCount > 0)
                    {
                        SelectedCountText.Text = $"已选择: 🔘{checkboxCount} 🖱️{dataGridCount} 题";
                    }
                    else if (checkboxCount > 0)
                    {
                        SelectedCountText.Text = $"已选择: 🔘{checkboxCount} 题";
                    }
                    else if (dataGridCount > 0)
                    {
                        SelectedCountText.Text = $"已选择: 🖱️{dataGridCount} 题";
                    }
                    else
                    {
                        SelectedCountText.Text = "已选择: 0 题";
                    }
                }

                // 安全更新按钮状态
                if (EditQuestionButton != null)
                {
                    EditQuestionButton.IsEnabled = true;
                    EditQuestionButton.ToolTip = "编辑当前鼠标选中行的题目";
                }

                if (DeleteQuestionButton != null)
                {
                    DeleteQuestionButton.IsEnabled = true;
                    int totalSelected = Math.Max(checkboxCount, dataGridCount);

                    if (totalSelected == 0)
                    {
                        DeleteQuestionButton.Content = "删除题目";
                        DeleteQuestionButton.ToolTip = "支持鼠标多选删除";
                    }
                    else if (totalSelected == 1)
                    {
                        DeleteQuestionButton.Content = "删除题目";
                        DeleteQuestionButton.ToolTip = "删除选中的题目";
                    }
                    else
                    {
                        DeleteQuestionButton.Content = $"删除 {totalSelected} 题";
                        DeleteQuestionButton.ToolTip = $"批量删除选中的 {totalSelected} 道题目";
                    }
                }

                // 安全更新全选复选框状态
                if (SelectAllCheckBox != null)
                {
                    if (checkboxCount == 0)
                    {
                        SelectAllCheckBox.IsChecked = false;
                    }
                    else if (checkboxCount == totalCount && totalCount > 0)
                    {
                        SelectAllCheckBox.IsChecked = true;
                    }
                    else
                    {
                        SelectAllCheckBox.IsChecked = null;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新选择状态失败：{ex.Message}");
                // 发生异常时不显示错误对话框，只记录日志
            }
        }

        #region 事件处理
        private void AddQuestion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new FlightControlQuestionEditWindow(null, currentTeacher);
                if (dialog.ShowDialog() == true && dialog.Question != null)
                {
                    if (FlightControlQuestionBankManager.SaveQuestion(dialog.Question))
                    {
                        StatusTextBlock.Text = "题目添加成功";
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
                if (QuestionsDataGrid?.SelectedItem is FlightControlQuestion selectedQuestion)
                {
                    var dialog = new FlightControlQuestionEditWindow(selectedQuestion, currentTeacher);
                    if (dialog.ShowDialog() == true && dialog.Question != null)
                    {
                        if (FlightControlQuestionBankManager.SaveQuestion(dialog.Question))
                        {
                            StatusTextBlock.Text = "题目编辑成功";
                            LoadQuestions();
                        }
                        else
                        {
                            MessageBox.Show("保存题目失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("请先点击选择要编辑的题目行！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
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
                var questionsToDelete = new List<FlightControlQuestion>();

                // 获取要删除的题目
                if (QuestionsDataGrid?.SelectedItems != null && QuestionsDataGrid.SelectedItems.Count > 0)
                {
                    var dataGridSelected = QuestionsDataGrid.SelectedItems.Cast<FlightControlQuestion>().ToList();
                    var checkboxSelected = filteredQuestions.Where(q => q.IsSelected).ToList();

                    if (checkboxSelected.Any())
                    {
                        var dialogResult = MessageBox.Show(
                            $"检测到两种选择方式：\n\n" +
                            $"🔘 复选框选中：{checkboxSelected.Count} 道题目\n" +
                            $"🖱️ 鼠标选中：{dataGridSelected.Count} 道题目\n\n" +
                            $"选择'是'：删除复选框选中的题目\n" +
                            $"选择'否'：删除鼠标选中的题目",
                            "选择删除方式",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        questionsToDelete = dialogResult switch
                        {
                            MessageBoxResult.Yes => checkboxSelected,
                            MessageBoxResult.No => dataGridSelected,
                            _ => new List<FlightControlQuestion>()
                        };

                        if (!questionsToDelete.Any()) return;
                    }
                    else
                    {
                        questionsToDelete = dataGridSelected;
                    }
                }
                else
                {
                    var checkboxSelected = filteredQuestions.Where(q => q.IsSelected).ToList();
                    if (checkboxSelected.Any())
                    {
                        questionsToDelete = checkboxSelected;
                    }
                    else
                    {
                        MessageBox.Show("请先选择要删除的题目！", "提示", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                // 执行删除
                if (questionsToDelete.Count == 1)
                {
                    DeleteSingleQuestion(questionsToDelete.First());
                }
                else if (questionsToDelete.Count > 1)
                {
                    DeleteMultipleQuestions(questionsToDelete);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSingleQuestion(FlightControlQuestion question)
        {
            var result = MessageBox.Show(
                $"确认要删除题目：\n\n\"{question.QuestionStatement}\"\n\n此操作无法撤销！",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (FlightControlQuestionBankManager.DeleteQuestion(question.Id))
                {
                    StatusTextBlock.Text = "题目删除成功";
                    LoadQuestions();
                }
                else
                {
                    MessageBox.Show("删除题目失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteMultipleQuestions(List<FlightControlQuestion> questions)
        {
            var confirmMessage = $"确认要删除选中的 {questions.Count} 道题目吗？\n\n" +
                                "此操作无法撤销！\n\n" +
                                "将要删除的题目：\n" +
                                string.Join("\n", questions.Take(5).Select(q =>
                                    $"• {q.QuestionStatement.Substring(0, Math.Min(50, q.QuestionStatement.Length))}..."));

            if (questions.Count > 5)
            {
                confirmMessage += $"\n... 还有 {questions.Count - 5} 道题目";
            }

            var result = MessageBox.Show(confirmMessage, "确认批量删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                StatusTextBlock.Text = $"正在删除 {questions.Count} 道题目...";

                var questionIds = questions.Select(q => q.Id).ToList();
                var deleteResult = FlightControlQuestionBankManager.BatchDeleteQuestions(questionIds);

                var resultMessage = $"批量删除完成！\n\n" +
                                   $"成功删除：{deleteResult.SuccessCount} 题\n" +
                                   $"删除失败：{deleteResult.FailCount} 题";

                if (deleteResult.Errors.Any())
                {
                    resultMessage += $"\n\n错误详情：\n{string.Join("\n", deleteResult.Errors.Take(5))}";
                }

                MessageBox.Show(resultMessage, "删除完成", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadQuestions();
                StatusTextBlock.Text = $"批量删除完成：成功 {deleteResult.SuccessCount} 题";
            }
        }

        private void DeleteDuplicates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "正在检测重复题目...";
                var detectionResult = FlightControlQuestionBankManager.DetectDuplicateQuestions();

                if (!detectionResult.Success)
                {
                    MessageBox.Show($"检测重复题目时出错：{detectionResult.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusTextBlock.Text = "检测重复题目失败";
                    return;
                }

                if (detectionResult.TotalDuplicateGroups == 0)
                {
                    MessageBox.Show("未发现重复题目！", "检测结果",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusTextBlock.Text = "未发现重复题目";
                    return;
                }

                var confirmMessage = $"检测到 {detectionResult.TotalDuplicateGroups} 组重复题目，" +
                                   $"共 {detectionResult.TotalDuplicateQuestions} 道题目。\n\n" +
                                   $"将要删除：{detectionResult.QuestionsToDelete} 道重复题目\n" +
                                   $"将要保留：{detectionResult.QuestionsToKeep} 道题目\n\n" +
                                   $"确定要删除重复题目吗？";

                var result = MessageBox.Show(confirmMessage, "确认删除重复题目",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    StatusTextBlock.Text = $"正在删除 {detectionResult.QuestionsToDelete} 道重复题目...";
                    var deleteResult = FlightControlQuestionBankManager.DeleteDuplicateQuestions(detectionResult.DuplicateGroups);

                    if (deleteResult.Success)
                    {
                        MessageBox.Show($"重复题目删除完成！成功删除：{deleteResult.SuccessCount} 道题目", 
                            "删除完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadQuestions();
                        StatusTextBlock.Text = $"重复题目删除完成：删除 {deleteResult.SuccessCount} 题";
                    }
                    else
                    {
                        MessageBox.Show($"删除重复题目失败：{deleteResult.Message}", "删除失败",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        StatusTextBlock.Text = "删除重复题目失败";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除重复题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "删除重复题目出错";
            }
        }

        // 更新导入按钮事件
        private void Import_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FlightControlQuestionBankManager.ShowImportDialog();

                // 导入完成后刷新显示
                LoadQuestions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入操作失败：{ex.Message}", "导入错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 更新导出按钮事件
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查是否有选中的题目
                List<FlightControlQuestion>? selectedQuestions = null;

                var checkboxSelected = filteredQuestions?.Where(q => q?.IsSelected == true).ToList();
                var dataGridSelected = QuestionsDataGrid?.SelectedItems?.Cast<FlightControlQuestion>()?.ToList();

                if (checkboxSelected?.Any() == true)
                {
                    selectedQuestions = checkboxSelected;
                }
                else if (dataGridSelected?.Any() == true)
                {
                    selectedQuestions = dataGridSelected;
                }

                // 如果没有选中的题目，询问是否导出全部
                if (selectedQuestions == null || !selectedQuestions.Any())
                {
                    var result = MessageBox.Show(
                        "没有选中的题目。是否导出全部题目？",
                        "导出确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                FlightControlQuestionBankManager.ShowExportDialog(selectedQuestions);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出操作失败：{ex.Message}", "导出错误",
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
                SearchTextBox.Text = "";
                TypeFilterComboBox.SelectedIndex = 0;
                CategoryFilterComboBox.SelectedIndex = 0;
                DifficultyFilterComboBox.SelectedIndex = 0;
                DataTypeFilterComboBox.SelectedIndex = 0;
                OnlyActiveCheckBox.IsChecked = true;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清除筛选时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectAll_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var question in filteredQuestions)
                {
                    question.IsSelected = true;
                }
                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"全选操作失败：{ex.Message}");
            }
        }

        private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var question in filteredQuestions)
                {
                    question.IsSelected = false;
                }
                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取消全选操作失败：{ex.Message}");
            }
        }

        private void SelectAll_Indeterminate(object sender, RoutedEventArgs e)
        {
            // 不确定状态不需要特殊处理
        }

        private void QuestionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新题目选择状态失败：{ex.Message}");
            }
        }

        private void QuestionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataGrid选择变化处理失败：{ex.Message}");
            }
        }

        private void QuestionsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

        private void QuestionsDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            e.Cancel = true;
        }

        private void CancelImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _importCancellationTokenSource?.Cancel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取消导入失败：{ex.Message}");
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
                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                    this.Close();
                }));
            }
        }
        #endregion
    }
}