using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Media;

namespace DroneSimulator
{
    public static class CheckBoxCommandHelper
    {
        public static readonly DependencyProperty CommandStringProperty =
            DependencyProperty.RegisterAttached(
                "CommandString",
                typeof(string),
                typeof(CheckBoxCommandHelper),
                new PropertyMetadata(string.Empty));

        public static void SetCommandString(DependencyObject element, string value)
        {
            element.SetValue(CommandStringProperty, value);
        }

        public static string GetCommandString(DependencyObject element)
        {
            return (string)element.GetValue(CommandStringProperty);
        }
    }

    public partial class QuestionPanel : Window
    {
        private UserInfo currentTeacher;
        private List<string> examFiles = new();
        private const string EXAMS_DIRECTORY = "Exams";

        public QuestionPanel(UserInfo teacher)
        {
            InitializeComponent();
            currentTeacher = teacher;
            InitializeTeacherInfo();
            LoadExistingExams();

            // 在窗口标题中显示当前登录身份信息
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            string roleInfo = "";
            if (currentTeacher.Type != UserType.Teacher)
            {
                roleInfo = $" - {GetUserTypeDisplayName(currentTeacher.Type)}以教师身份登录";
            }
            this.Title = $"试题管理面板{roleInfo}";
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

        private void InitializeTeacherInfo()
        {
            TeacherNameText.Text = $"姓名：{currentTeacher.Name}";
            TeacherIdText.Text = $"工号：{currentTeacher.IdNumber}";
        }

        private void LoadExistingExams()
        {
            if (!Directory.Exists(EXAMS_DIRECTORY))
                Directory.CreateDirectory(EXAMS_DIRECTORY);

            examFiles.Clear();
            ExamList.Items.Clear();

            foreach (string file in Directory.GetFiles(EXAMS_DIRECTORY, "*.json"))
            {
                string examName = Path.GetFileNameWithoutExtension(file);
                examFiles.Add(file);
                ExamList.Items.Add(examName);
            }
        }

        private void SelectAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var checkbox in FindAllCheckBoxes())
            {
                checkbox.IsChecked = true;
            }
        }

        private void ClearAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var checkbox in FindAllCheckBoxes())
            {
                checkbox.IsChecked = false;
            }
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExamNameBox.Text))
            {
                MessageBox.Show("请输入试卷名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 获取所有CheckBox对应的Question对象
            var allQuestions = GetAllQuestions();
            // 只保存被选中的题目
            var selectedQuestions = allQuestions.FindAll(q => q.IsChecked);

            if (selectedQuestions.Count == 0)
            {
                MessageBox.Show("请至少选择一道题目！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Questions = allQuestions
            // 其中 IsChecked 为 true 的题就是选中的题。
            var examData = new ExamData
            {
                ExamName = ExamNameBox.Text,
                TeacherName = currentTeacher.Name,
                TeacherId = currentTeacher.IdNumber,
                CreationTime = DateTime.Now,
                Questions = allQuestions
            };

            SaveExam(examData);
            LoadExistingExams();
            MessageBox.Show("试卷生成成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取选中的试卷
            var selectedItem = ExamList.SelectedItem;
            if (selectedItem == null)
            {
                MessageBox.Show("请先选择要删除的试卷。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确认删除
            var result = MessageBox.Show($"确定要删除选中的试卷吗？\n\n{selectedItem}", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            // 删除对应的文件
            string fileName = Path.Combine(EXAMS_DIRECTORY, $"{selectedItem}.json");
            try
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"文件删除失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 从列表中移除
            ExamList.Items.Remove(selectedItem);
            MessageBox.Show("试卷已删除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private List<Question> GetSelectedQuestions()
        {
            var selected = new List<Question>();
            foreach (var checkbox in FindAllCheckBoxes())
            {
                if (checkbox.IsChecked == true)
                {
                    selected.Add(new Question(checkbox));
                }
            }
            return selected;
        }

        private IEnumerable<CheckBox> FindAllCheckBoxes()
        {
            var checkboxes = new List<CheckBox>();
            FindVisualChildren<CheckBox>(this, checkboxes);
            return checkboxes;
        }

        private void FindVisualChildren<T>(DependencyObject obj, List<T> results) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t)
                    results.Add(t);
                FindVisualChildren<T>(child, results);
            }
        }

        private void SaveExam(ExamData examData)
        {
            string fileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}.json");
            string jsonString = JsonSerializer.Serialize(examData);
            File.WriteAllText(fileName, jsonString);
        }

        private void ExamList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExamList.SelectedItem != null)
            {
                string fileName = Path.Combine(EXAMS_DIRECTORY, $"{ExamList.SelectedItem}.json");
                if (File.Exists(fileName))
                {
                    string jsonString = File.ReadAllText(fileName);
                    var examData = JsonSerializer.Deserialize<ExamData>(jsonString);
                    LoadExamData(examData);

                    // 更新试卷信息概述
                    ExamSummaryText.Text = GenerateExamSummary(examData);
                }
            }
            else
            {
                ExamSummaryText.Text = "";
            }
        }

        // 生成试卷信息概述
        private string GenerateExamSummary(ExamData examData)
        {
            int selectedCount = examData.Questions?.Count(q => q.IsChecked) ?? 0;
            return $"试卷名称：{examData.ExamName}\n" +
                   $"出题教师：{examData.TeacherName}\n" +
                   $"教师工号：{examData.TeacherId}\n" +
                   $"题目数量：{selectedCount}\n" +
                   $"创建时间：{examData.CreationTime:yyyy-MM-dd HH:mm}\n";
        }

        private void LoadExamData(ExamData examData)
        {
            foreach (var checkbox in FindAllCheckBoxes())
            {
                checkbox.IsChecked = false;
            }
            foreach (var question in examData.Questions)
            {
                var checkbox = FindName(question.Name) as CheckBox;
                if (checkbox != null)
                {
                    checkbox.IsChecked = question.IsChecked;
                }
            }
            ExamNameBox.Text = examData.ExamName;
        }

        private void ViewExamRecords_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var recordsWindow = new ExamRecordsWindow(currentTeacher);
                recordsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开考试记录窗口失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();            
            // Application.Current.Shutdown(); // 强制退出应用
        }

        // 遍历所有带有 CommandString 的 CheckBox，获取所有试题
        public List<Question> GetAllQuestions()
        {
            var questions = new List<Question>();
            foreach (var checkbox in FindAllCheckBoxes())
            {
                // 只处理带有 CommandString 的 CheckBox
                var commandString = CheckBoxCommandHelper.GetCommandString(checkbox);
                if (!string.IsNullOrEmpty(commandString))
                {
                    questions.Add(new Question(checkbox));
                }
            }
            return questions;
        }
    }
}