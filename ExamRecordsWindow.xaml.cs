using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace DroneSimulator
{
    public partial class ExamRecordsWindow : Window
    {
        private List<DetailedExamRecordViewModel> _allRecords = new();
        private List<DetailedExamRecordViewModel> _filteredRecords = new();
        private UserInfo _currentUser;

        public ExamRecordsWindow(UserInfo currentUser)
        {
            InitializeComponent();
            _currentUser = currentUser;
            InitializeByUserRole();
            LoadExamRecords();
        }

        /// <summary>
        /// 根据用户角色初始化界面权限
        /// </summary>
        private void InitializeByUserRole()
        {
            // 检查用户的原始身份（Type），而不是当前角色（CurrentRole）
            bool isAdmin = _currentUser.Type == UserType.Admin;

            if (!isAdmin)
            {
                // 非管理员隐藏删除按钮
                DeleteButton.Visibility = Visibility.Collapsed;

                // 可选：也可以禁用而不是隐藏
                // DeleteButton.IsEnabled = false;
                // DeleteButton.ToolTip = "只有管理员才能删除考试记录";
            }

            // 更新窗口标题，显示当前用户角色信息
            UpdateWindowTitle();
        }

        /// <summary>
        /// 更新窗口标题
        /// </summary>
        private void UpdateWindowTitle()
        {
            string roleInfo = "";
            if (_currentUser.Type != UserType.Admin)
            {
                roleInfo = $" - {GetUserTypeDisplayName(_currentUser.Type)}用户";
            }
            this.Title = $"考试记录查看{roleInfo}";
        }

        /// <summary>
        /// 获取用户类型显示名称
        /// </summary>
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

        private void LoadExamRecords()
        {
            try
            {
                var records = ExamRecordManager.GetAllExamRecords();
                _allRecords = records.Select(r => new DetailedExamRecordViewModel(r)).ToList();
                _filteredRecords = new List<DetailedExamRecordViewModel>(_allRecords);

                UpdateDataGrid();
                UpdateRecordCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载考试记录失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDataGrid()
        {
            ExamRecordsDataGrid.ItemsSource = null;
            ExamRecordsDataGrid.ItemsSource = _filteredRecords;
        }

        private void UpdateRecordCount()
        {
            RecordCountText.Text = _filteredRecords.Count.ToString();
        }

        private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            string studentNameFilter = StudentNameFilter.Text.Trim().ToLower();
            string examNameFilter = ExamNameFilter.Text.Trim().ToLower();

            _filteredRecords = _allRecords.Where(record =>
                (string.IsNullOrEmpty(studentNameFilter) ||
                 record.StudentName.ToLower().Contains(studentNameFilter)) &&
                (string.IsNullOrEmpty(examNameFilter) ||
                 record.ExamInfo.ExamName.ToLower().Contains(examNameFilter))
            ).ToList();

            UpdateDataGrid();
            UpdateRecordCount();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadExamRecords();
        }

        private void ExamRecordsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExamRecordsDataGrid.SelectedItem is DetailedExamRecordViewModel selectedRecord)
            {
                ShowRecordDetails(selectedRecord);
            }
            else
            {
                ClearRecordDetails();
            }
        }

        private void ShowRecordDetails(DetailedExamRecordViewModel record)
        {
            // 清空之前的内容
            QuestionStatusPanel.Children.Clear();
            WronglyRepairedList.ItemsSource = null;
            UnrepairedList.ItemsSource = null;

            // 显示题目状态统计
            var statusGroups = record.OriginalRecord.QuestionStatuses
                .GroupBy(q => q.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var statusGroup in statusGroups)
            {
                var statusText = new TextBlock
                {
                    Text = $"{GetStatusDisplayName(statusGroup.Key)}: {statusGroup.Value} 题",
                    Margin = new Thickness(0, 2, 0, 2),
                    Foreground = GetStatusColor(statusGroup.Key)
                };
                QuestionStatusPanel.Children.Add(statusText);
            }

            // 显示误修复题目
            var wronglyRepairedQuestions = record.OriginalRecord.QuestionStatuses
                .Where(q => q.Status == RepairStatus.WronglyRepaired)
                .Select(q => new { Name = q.QuestionName, Content = q.QuestionContent })
                .ToList();
            WronglyRepairedList.ItemsSource = wronglyRepairedQuestions;

            // 显示未修复题目
            var unrepairedQuestions = record.OriginalRecord.QuestionStatuses
                .Where(q => q.Status == RepairStatus.Unrepaired)
                .Select(q => new { Name = q.QuestionName, Content = q.QuestionContent })
                .ToList();
            UnrepairedList.ItemsSource = unrepairedQuestions;
        }

        private void ClearRecordDetails()
        {
            QuestionStatusPanel.Children.Clear();
            WronglyRepairedList.ItemsSource = null;
            UnrepairedList.ItemsSource = null;
        }

        private string GetStatusDisplayName(RepairStatus status)
        {
            return status switch
            {
                RepairStatus.NotTouched => "未操作",
                RepairStatus.CorrectlyRepaired => "正确修复",
                RepairStatus.WronglyRepaired => "误修复",
                RepairStatus.Unrepaired => "未修复",
                _ => "未知"
            };
        }

        private Brush GetStatusColor(RepairStatus status)
        {
            return status switch
            {
                RepairStatus.NotTouched => Brushes.Gray,
                RepairStatus.CorrectlyRepaired => Brushes.Green,
                RepairStatus.WronglyRepaired => Brushes.Red,
                RepairStatus.Unrepaired => Brushes.Orange,
                _ => Brushes.Black
            };
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"考试记录_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportToCsv(saveDialog.FileName);
                    MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportToCsv(string fileName)
        {
            using var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8);

            // 写入标题行
            writer.WriteLine("序号,学生姓名,身份证号,试卷名称,出题教师,得分,正确答题,错误答题,用时,开始时间,提交时间,误修复题目,未修复题目");

            // 写入数据行
            foreach (var record in _filteredRecords)
            {
                var wronglyRepaired = string.Join(";", record.OriginalRecord.WronglyRepairedQuestions);
                var unrepaired = string.Join(";", record.OriginalRecord.UnrepairedQuestions);

                writer.WriteLine($"{record.ExamSequence}," +
                    $"{record.StudentName}," +
                    $"{record.StudentId}," +
                    $"{record.ExamInfo.ExamName}," +
                    $"{record.ExamInfo.TeacherName}," +
                    $"{record.Score}," +
                    $"{record.CorrectAnswers}," +
                    $"{record.WrongAnswers}," +
                    $"{record.ElapsedTimeString}," +
                    $"{record.StartTime:yyyy-MM-dd HH:mm:ss}," +
                    $"{record.SubmitTime:yyyy-MM-dd HH:mm:ss}," +
                    $"\"{wronglyRepaired}\"," +
                    $"\"{unrepaired}\"");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // 双重检查权限
            if (_currentUser.Type != UserType.Admin)
            {
                MessageBox.Show("只有管理员才能删除考试记录！", "权限不足",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ExamRecordsDataGrid.SelectedItem is DetailedExamRecordViewModel selectedRecord)
            {
                var result = MessageBox.Show(
                    $"确定要删除学生 {selectedRecord.StudentName} 的考试记录吗？\n" +
                    $"序号：{selectedRecord.ExamSequence}\n" +
                    $"提交时间：{selectedRecord.SubmitTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                    $"警告：此操作不可恢复！",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // 实际删除文件的逻辑
                        bool deleted = ExamRecordManager.DeleteExamRecord(selectedRecord.ExamSequence);

                        if (deleted)
                        {
                            _allRecords.Remove(selectedRecord);
                            ApplyFilters();
                            MessageBox.Show("删除成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("删除失败！记录可能已不存在。", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除失败：{ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("请先选择要删除的记录！", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// 用于数据绑定的考试记录视图模型
    /// </summary>
    public class DetailedExamRecordViewModel : INotifyPropertyChanged
    {
        public DetailedExamRecord OriginalRecord { get; }

        public DetailedExamRecordViewModel(DetailedExamRecord record)
        {
            OriginalRecord = record;
        }

        public int ExamSequence => OriginalRecord.ExamSequence;
        public string StudentName => OriginalRecord.StudentName;
        public string StudentId => OriginalRecord.StudentId;
        public ExamInfo ExamInfo => OriginalRecord.ExamInfo;
        public int Score => OriginalRecord.Score;
        public int CorrectAnswers => OriginalRecord.CorrectAnswers;
        public int WrongAnswers => OriginalRecord.WrongAnswers;
        public TimeSpan ElapsedTime => OriginalRecord.ElapsedTime;
        public DateTime StartTime => OriginalRecord.StartTime;
        public DateTime SubmitTime => OriginalRecord.SubmitTime;

        public string ElapsedTimeString =>
            $"{(int)ElapsedTime.TotalMinutes:D2}:{ElapsedTime.Seconds:D2}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}