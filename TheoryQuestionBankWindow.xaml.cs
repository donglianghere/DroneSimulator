using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

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
                // 检查所有控件是否存在
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
                    new { Value = (QuestionDifficulty?)QuestionDifficulty.Easy, Display = "简" },
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
                // 检查状态文本控件
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "正在加载数据...";
                }

                allQuestions = TheoryQuestionBankManager.GetAllQuestions();

                ApplyFilters();

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "数据加载完成";
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"加载数据失败：{ex.Message}";

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

                // 检查搜索框存在
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

                // 类型筛选 - 检查安全
                if (TypeFilterComboBox?.SelectedValue is TheoryQuestionType selectedType)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.Type == selectedType).ToList();
                }

                // 分类筛选 - 检查安全
                if (CategoryFilterComboBox?.SelectedValue is TheoryQuestionCategory selectedCategory)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.Category == selectedCategory).ToList();
                }

                // 难度筛选 - 检查安全
                if (DifficultyFilterComboBox?.SelectedValue is QuestionDifficulty selectedDifficulty)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.Difficulty == selectedDifficulty).ToList();
                }

                // 仅显示启用的题目 - 检查安全
                if (OnlyActiveCheckBox?.IsChecked == true)
                {
                    filteredQuestions = filteredQuestions.Where(q => q.IsActive).ToList();
                }

                // 更新数据网格 - 检查安全
                if (QuestionsDataGrid != null)
                {
                    QuestionsDataGrid.ItemsSource = filteredQuestions;
                }

                // 更新统计文本 - 检查安全
                if (CountTextBlock != null)
                {
                    CountTextBlock.Text = $"总计: {filteredQuestions.Count} 题 (共 {allQuestions.Count} 题)";
                }

                // 更新选择状态
                UpdateSelectionStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"筛选题目时发生错误：{ex.Message}", "筛选错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 批量选择和删除功能

        /// <summary>
        /// 更新选择状态和按钮状态
        /// </summary>
        private void UpdateSelectionStatus()
        {
            try
            {
                var selectedQuestions = filteredQuestions.Where(q => q.IsSelected).ToList();
                int selectedCount = selectedQuestions.Count;
                int totalCount = filteredQuestions.Count;

                // 更新选择计数显示
                if (SelectedCountText != null)
                {
                    SelectedCountText.Text = $"已选择: {selectedCount} 题";
                }

                // 更新编辑按钮状态和提示
                if (EditQuestionButton != null)
                {
                    if (selectedCount == 1)
                    {
                        EditQuestionButton.IsEnabled = true;
                        EditQuestionButton.ToolTip = "编辑选中的题目";
                    }
                    else if (selectedCount > 1)
                    {
                        EditQuestionButton.IsEnabled = true;
                        EditQuestionButton.ToolTip = "只能编辑一个题目，将编辑第一个选中的题目";
                    }
                    else
                    {
                        EditQuestionButton.IsEnabled = true;
                        EditQuestionButton.ToolTip = "编辑当前行选中的题目";
                    }
                }

                // 更新删除按钮状态和提示（合并了批量删除功能）
                if (DeleteQuestionButton != null)
                {
                    DeleteQuestionButton.IsEnabled = true; // 始终启用，让按钮内部逻辑处理

                    if (selectedCount == 0)
                    {
                        DeleteQuestionButton.Content = "删除题目";
                        DeleteQuestionButton.ToolTip = "删除当前行选中的题目";
                    }
                    else if (selectedCount == 1)
                    {
                        DeleteQuestionButton.Content = "删除题目";
                        DeleteQuestionButton.ToolTip = "删除选中的题目";
                    }
                    else
                    {
                        DeleteQuestionButton.Content = $"删除 {selectedCount} 题";
                        DeleteQuestionButton.ToolTip = $"批量删除选中的 {selectedCount} 道题目";
                    }
                }

                // 更新全选复选框状态
                if (SelectAllCheckBox != null)
                {
                    if (selectedCount == 0)
                    {
                        SelectAllCheckBox.IsChecked = false;
                    }
                    else if (selectedCount == totalCount)
                    {
                        SelectAllCheckBox.IsChecked = true;
                    }
                    else
                    {
                        SelectAllCheckBox.IsChecked = null; // 部分选择状态
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新选择状态失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 全选复选框被选中
        /// </summary>
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

        /// <summary>
        /// 全选复选框被取消选中
        /// </summary>
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

        /// <summary>
        /// 全选复选框处于不确定状态（部分选择）
        /// </summary>
        private void SelectAll_Indeterminate(object sender, RoutedEventArgs e)
        {
            // 不确定状态不需要特殊处理
        }

        /// <summary>
        /// 单个题目复选框状态改变
        /// </summary>
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

        /// <summary>
        /// 智能删除题目按钮点击事件（合并了批量删除功能）
        /// </summary>
        private void DeleteQuestion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 首先检查是否有复选框选中的题目
                var selectedQuestions = filteredQuestions.Where(q => q.IsSelected).ToList();

                if (selectedQuestions.Any())
                {
                    // 如果有复选框选中的题目，删除选中的题目
                    if (selectedQuestions.Count == 1)
                    {
                        // 单个题目删除
                        DeleteSingleQuestion(selectedQuestions.First());
                    }
                    else
                    {
                        // 多个题目批量删除
                        DeleteMultipleQuestions(selectedQuestions);
                    }
                }
                else
                {
                    // 如果没有复选框选中的题目，使用DataGrid当前选中的行
                    if (QuestionsDataGrid?.SelectedItem is TheoryQuestion selectedQuestion)
                    {
                        DeleteSingleQuestion(selectedQuestion);
                    }
                    else
                    {
                        MessageBox.Show("请先选择要删除的题目！\n\n您可以：\n1. 勾选题目前的复选框\n2. 或者点击题目行进行选择",
                            "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除单个题目
        /// </summary>
        private void DeleteSingleQuestion(TheoryQuestion question)
        {
            var result = MessageBox.Show(
                $"确认要删除题目：\n\n\"{question.QuestionStatement}\"\n\n此操作无法撤销！",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (TheoryQuestionBankManager.DeleteQuestion(question.Id))
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

        /// <summary>
        /// 批量删除多个题目
        /// </summary>
        private void DeleteMultipleQuestions(List<TheoryQuestion> questions)
        {
            // 确认删除
            string confirmMessage = $"确认要删除选中的 {questions.Count} 道题目吗？\n\n" +
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
                // 执行批量删除
                StatusTextBlock.Text = $"正在删除 {questions.Count} 道题目...";

                var questionIds = questions.Select(q => q.Id).ToList();
                var deleteResult = TheoryQuestionBankManager.BatchDeleteQuestions(questionIds);

                // 显示删除结果
                string resultMessage = $"批量删除完成！\n\n" +
                                     $"成功删除：{deleteResult.SuccessCount} 题\n" +
                                     $"删除失败：{deleteResult.FailCount} 题";

                if (deleteResult.Errors.Any())
                {
                    resultMessage += $"\n\n错误详情：\n{string.Join("\n", deleteResult.Errors.Take(5))}";
                    if (deleteResult.Errors.Count > 5)
                    {
                        resultMessage += $"\n... 还有 {deleteResult.Errors.Count - 5} 个错误";
                    }
                }

                MessageBox.Show(resultMessage,
                    deleteResult.SuccessCount > 0 ? "删除完成" : "删除失败",
                    MessageBoxButton.OK,
                    deleteResult.SuccessCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

                // 刷新数据
                LoadQuestions();

                StatusTextBlock.Text = $"批量删除完成：成功 {deleteResult.SuccessCount} 题，失败 {deleteResult.FailCount} 题";
            }
        }
        #endregion

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

        /// <summary>
        /// 编辑题目按钮点击事件（支持复选框选择）
        /// </summary>
        private void EditQuestion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TheoryQuestion questionToEdit = null;

                // 首先检查是否有复选框选中的题目
                var selectedQuestions = filteredQuestions.Where(q => q.IsSelected).ToList();

                if (selectedQuestions.Count == 1)
                {
                    // 如果只选中了一个题目，编辑该题目
                    questionToEdit = selectedQuestions.First();
                }
                else if (selectedQuestions.Count > 1)
                {
                    // 如果选中了多个题目，提示用户并编辑第一个
                    var result = MessageBox.Show(
                        $"您选中了 {selectedQuestions.Count} 道题目，但只能编辑一道题目。\n\n" +
                        $"是否编辑第一个选中的题目：\n\"{selectedQuestions.First().QuestionStatement.Substring(0, Math.Min(50, selectedQuestions.First().QuestionStatement.Length))}...\"？",
                        "多个题目选中",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        questionToEdit = selectedQuestions.First();
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    // 如果没有复选框选中的题目，使用DataGrid当前选中的行
                    if (QuestionsDataGrid?.SelectedItem is TheoryQuestion selectedQuestion)
                    {
                        questionToEdit = selectedQuestion;
                    }
                    else
                    {
                        MessageBox.Show("请先选择要编辑的题目！\n\n您可以：\n1. 勾选题目前的复选框\n2. 或者点击题目行进行选择",
                            "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                if (questionToEdit != null)
                {
                    var dialog = new TheoryQuestionEditWindow(questionToEdit, currentTeacher);
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"编辑题目时发生错误：{ex.Message}", "错误",
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
                    Filter = "支持的文件 (*.json;*.csv;*.xlsx)|*.json;*.csv;*.xlsx|TQ4格式CSV (*.csv)|*.csv|JSON文件 (*.json)|*.json|Excel文件 (*.xlsx)|*.xlsx|所有文件 (*.*)|*.*",
                    DefaultExt = "csv"
                };

                if (openDialog.ShowDialog() == true)
                {
                    var filePath = openDialog.FileName;
                    var extension = Path.GetExtension(filePath).ToLower();
                    var fileName = Path.GetFileName(filePath).ToLower();

                    // 检测是否为 TQ4.csv 格式
                    bool isTQ4Format = fileName.Contains("tq") || IsDetectedAsTQ4Format(filePath);

                    if (extension == ".csv" && isTQ4Format)
                    {
                        // TQ4.csv 格式预览
                        var previewResult = PreviewTQ4CsvFile(filePath);
                        if (previewResult.Success)
                        {
                            var previewMessage = $"检测到TQ4.csv格式文件：\n\n" +
                                               $"预计题目数量：{previewResult.EstimatedQuestionCount}\n" +
                                               $"检测到的题目类型：{string.Join(", ", previewResult.DetectedTypes)}\n\n" +
                                               $"是否继续导入？";

                            var continueResult = MessageBox.Show(previewMessage, "TQ4.csv文件检测",
                                MessageBoxButton.YesNo, MessageBoxImage.Information);

                            if (continueResult != MessageBoxResult.Yes)
                                return;
                        }
                    }

                    // 询问是否覆盖已存在的题目
                    var overwriteResult = MessageBox.Show(
                        "是否覆盖已存在的题目？\n\n" +
                        "选择“是”：覆盖同ID的题目\n" +
                        "选择“否”：跳过已存在的题目",
                        "导入选项",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (overwriteResult == MessageBoxResult.Cancel)
                        return;

                    bool overwriteExisting = overwriteResult == MessageBoxResult.Yes;

                    // 显示进度
                    if (StatusTextBlock != null)
                    {
                        StatusTextBlock.Text = "正在导入题库...";
                    }

                    ImportExportResult result;

                    // 根据文件格式选择导入方法
                    switch (extension)
                    {
                        case ".json":
                            result = TheoryQuestionImportExportService.ImportFromJson(filePath, overwriteExisting);
                            break;
                        case ".csv" when isTQ4Format:
                            result = TheoryQuestionImportExportService.ImportFromTQ4Csv(filePath, overwriteExisting);
                            break;
                        case ".csv":
                            result = TheoryQuestionImportExportService.ImportFromCsv(filePath, overwriteExisting);
                            break;
                        case ".xlsx":
                        case ".xls":
                            result = TheoryQuestionImportExportService.ImportFromExcel(filePath, overwriteExisting);
                            break;
                        default:
                            MessageBox.Show("不支持的文件格式！", "格式错误",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                    }

                    // 显示结果
                    DisplayImportResult(result, filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入操作失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "导入失败";
                }
            }
        }

        /// <summary>
        /// 检测是否为 TQ4 格式的 CSV 文件
        /// </summary>
        private bool IsDetectedAsTQ4Format(string filePath)
        {
            try
            {
                var lines = File.ReadLines(filePath, Encoding.UTF8).Take(5).ToList();
                if (lines.Count < 2) return false;

                // 检查第一行是否包含 TQ4 格式的特征
                var firstLine = lines[0];
                if (firstLine.Contains("条件选择") || firstLine.Contains("题型") || firstLine.Contains("难度代码"))
                    return true;

                // 检查数据行是否符合 TQ4 格式特征
                for (int i = 1; i < lines.Count; i++)
                {
                    var fields = TheoryQuestionImportExportService.ParseCsvLine(lines[i]);
                    if (fields.Count >= 15 &&
                        (fields[1].Contains("A-A-") || fields[1].Contains("A-B-") || fields[1].Contains("B-A-")) &&
                        (fields[2] == "B" || fields[2] == "C" || fields[2].Contains("选择题") || fields[2].Contains("判断题")))
                    {
                        return true;
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
        /// 预览 TQ4.csv 文件
        /// </summary>
        private TQ4PreviewResult PreviewTQ4CsvFile(string filePath)
        {
            try
            {
                var lines = File.ReadLines(filePath, Encoding.UTF8).Take(50).ToList();
                var result = new TQ4PreviewResult { Success = true };

                var detectedTypes = new HashSet<string>();
                int questionCount = 0;

                for (int i = 1; i < lines.Count; i++)
                {
                    var fields = TheoryQuestionImportExportService.ParseCsvLine(lines[i]);
                    if (fields.Count >= 15 && !string.IsNullOrWhiteSpace(fields[4]))
                    {
                        questionCount++;
                        if (fields[2] == "B") detectedTypes.Add("单选题");
                        if (fields[2] == "C") detectedTypes.Add("判断题");
                    }
                }

                result.EstimatedQuestionCount = Math.Max(questionCount, lines.Count - 1);
                result.DetectedTypes = detectedTypes.ToList();

                return result;
            }
            catch (Exception ex)
            {
                return new TQ4PreviewResult
                {
                    Success = false,
                    Message = $"预览TQ4.csv文件失败：{ex.Message}"
                };
            }
        }

        /// <summary>
        /// TQ4预览结果
        /// </summary>
        private class TQ4PreviewResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public int EstimatedQuestionCount { get; set; }
            public List<string> DetectedTypes { get; set; } = new();
        }

        private void DisplayImportResult(ImportExportResult result, string filePath)
        {
            if (result.Success)
            {
                var successMessage = $"导入成功！\n\n" +
                                   $"文件：{Path.GetFileName(filePath)}\n" +
                                   $"结果：{result.Message}";

                MessageBox.Show(successMessage, "导入成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // 刷新题库显示
                LoadQuestions();

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "导入完成";
                }
            }
            else
            {
                var errorMessage = $"导入失败！\n\n" +
                                  $"文件：{Path.GetFileName(filePath)}\n" +
                                  $"错误：{result.Message}";

                MessageBox.Show(errorMessage, "导入失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "导入失败";
                }
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
                    var filePath = saveDialog.FileName;
                    var extension = Path.GetExtension(filePath).ToLower();

                    // 询问导出范围
                    var exportResult = MessageBox.Show(
                        "选择导出范围：\n\n" +
                        "选择“是”：导出当前筛选结果\n" +
                        "选择“否”：导出全部题目",
                        "导出范围",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (exportResult == MessageBoxResult.Cancel)
                        return;

                    // 确定要导出的题目
                    var questionsToExport = exportResult == MessageBoxResult.Yes
                        ? filteredQuestions
                        : allQuestions;

                    if (!questionsToExport.Any())
                    {
                        MessageBox.Show("没有题目可以导出！", "导出错误",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 显示进度
                    if (StatusTextBlock != null)
                    {
                        StatusTextBlock.Text = $"正在导出 {questionsToExport.Count} 道题目...";
                    }

                    ImportExportResult result;

                    // 根据文件扩展名选择导出方法
                    if (extension == ".json")
                    {
                        result = TheoryQuestionImportExportService.ExportToJson(filePath, questionsToExport);
                    }
                    else if (extension == ".csv")
                    {
                        result = TheoryQuestionImportExportService.ExportToCsv(filePath, questionsToExport);
                    }
                    else
                    {
                        MessageBox.Show("不支持的文件格式！仅支持 JSON 和 CSV 格式。", "格式错误",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 显示结果
                    if (result.Success)
                    {
                        MessageBox.Show(result.Message, "导出成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // 询问是否打开文件所在文件夹
                        var openFolderResult = MessageBox.Show(
                            "是否打开文件所在文件夹？",
                            "导出完成",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (openFolderResult == MessageBoxResult.Yes)
                        {
                            try
                            {
                                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"打开文件夹失败：{ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show(result.Message, "导出失败",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    if (StatusTextBlock != null)
                    {
                        StatusTextBlock.Text = result.Success ? "导出完成" : "导出失败";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出操作失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = "导出失败";
                }
            }
        }

        // 添加一个测试导入导出的方法
        private void TestImportExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建示例题目
                var sampleQuestions = TheoryQuestionImportExportService.CreateSampleQuestions();

                // 测试JSON导出
                var jsonPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test_questions.json");
                var jsonResult = TheoryQuestionImportExportService.ExportToJson(jsonPath, sampleQuestions);

                // 测试CSV导出
                var csvPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test_questions.csv");
                var csvResult = TheoryQuestionImportExportService.ExportToCsv(csvPath, sampleQuestions);

                var message = $"测试结果：\n" +
                             $"JSON导出：{(jsonResult.Success ? "成功" : "失败")} - {jsonResult.Message}\n" +
                             $"CSV导出：{(csvResult.Success ? "成功" : "失败")} - {csvResult.Message}\n\n" +
                             $"测试文件已保存到桌面";

                MessageBox.Show(message, "测试完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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