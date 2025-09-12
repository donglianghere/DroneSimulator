using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Media;
using System.Linq;

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

            // 添加试卷名称输入框的实时检查
            ExamNameBox.TextChanged += ExamNameBox_TextChanged;

            // 初始化状态检查（在窗口加载完成后执行）
            this.Loaded += (s, e) => CheckExamNameConflict();

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

        /// <summary>
        /// 试卷名称输入框文本改变事件
        /// </summary>
        private void ExamNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckExamNameConflict();
        }

        /// <summary>
        /// 检查试卷名称冲突
        /// </summary>
        private void CheckExamNameConflict()
        {
            if (string.IsNullOrWhiteSpace(ExamNameBox.Text))
            {
                // 重置生成按钮状态
                GenerateButton.IsEnabled = true;
                GenerateButton.ToolTip = "生成试卷";

                // 清空状态提示
                if (ExamNameStatusText != null)
                {
                    ExamNameStatusText.Text = "";
                }
                return;
            }

            string fileName = Path.Combine(EXAMS_DIRECTORY, $"{ExamNameBox.Text}.json");

            if (File.Exists(fileName))
            {
                try
                {
                    string existingJson = File.ReadAllText(fileName);
                    var existingExam = JsonSerializer.Deserialize<ExamData>(existingJson);

                    if (existingExam != null)
                    {
                        bool canModify = CanModifyExam(existingExam);

                        if (!canModify)
                        {
                            // 不能修改，禁用生成按钮
                            GenerateButton.IsEnabled = false;
                            GenerateButton.ToolTip = $"试卷名称已被教师 { existingExam.TeacherName} 使用，请更换名称";

                            // 更新状态提示
                            if (ExamNameStatusText != null)
                            {
                                ExamNameStatusText.Text = $"⚠️ 试卷名称已被教师 { existingExam.TeacherName} 使用";
                                ExamNameStatusText.Foreground = new SolidColorBrush(Colors.Red);
                            }
                        }
                        else
                        {
                            // 可以覆盖，启用生成按钮但提示用户
                            GenerateButton.IsEnabled = true;
                            GenerateButton.ToolTip = $"试卷已存在，点击将覆盖现有试卷（创建于{existingExam.CreationTime:yyyy-MM-dd HH:mm}）";

                            // 更新状态提示
                            if (ExamNameStatusText != null)
                            {
                                ExamNameStatusText.Text = $"ℹ️ 试卷已存在，将覆盖现有版本（{existingExam.CreationTime:yyyy-MM-dd HH:mm}）";
                                ExamNameStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                            }
                        }
                    }
                }
                catch
                {
                    // 文件读取失败，建议更换名称
                    GenerateButton.IsEnabled = false;
                    GenerateButton.ToolTip = "试卷文件存在异常，请更换名称";

                    // 更新状态提示
                    if (ExamNameStatusText != null)
                    {
                        ExamNameStatusText.Text = "❌ 试卷文件异常，请更换名称";
                        ExamNameStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
            }
            else
            {
                // 名称可用
                GenerateButton.IsEnabled = true;
                GenerateButton.ToolTip = "生成试卷";

                // 更新状态提示
                if (ExamNameStatusText != null)
                {
                    ExamNameStatusText.Text = "✓ 试卷名称可用";
                    ExamNameStatusText.Foreground = new SolidColorBrush(Colors.Green);
                }
            }
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

            // 检查同名试卷是否已存在
            string fileName = Path.Combine(EXAMS_DIRECTORY, $"{ExamNameBox.Text}.json");
            if (File.Exists(fileName))
            {
                // 读取现有试卷数据，检查创建者
                try
                {
                    string existingJson = File.ReadAllText(fileName);
                    var existingExam = JsonSerializer.Deserialize<ExamData>(existingJson);

                    if (existingExam != null)
                    {
                        // 检查是否为当前用户创建的试卷
                        bool isOwnExam = CanModifyExam(existingExam);

                        if (!isOwnExam)
                        {
                            // 不是自己创建的试卷，禁止覆盖
                            MessageBox.Show($"试卷名称冲突！\n\n" +
                                           $"试卷《{ExamNameBox.Text}》已由教师 { existingExam.TeacherName} 创建。\n" + 
                                           $"创建时间：{existingExam.CreationTime:yyyy-MM-dd HH:mm}\n\n" +
                                           $"请更换试卷名称或联系原创建者。", 
                                           "无法创建试卷", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        else
                        {
                            // 是自己创建的试卷，询问是否覆盖
                            var result = MessageBox.Show($"试卷《{ExamNameBox.Text}》已存在！\n\n" +
                                                       $"创建时间：{existingExam.CreationTime:yyyy-MM-dd HH:mm}\n" +
                                                       $"题目数量：{existingExam.Questions?.Count(q => q.IsChecked) ?? 0}\n\n" +
                                                       $"确定要覆盖现有试卷吗？",
                                                       "确认覆盖", MessageBoxButton.YesNo, MessageBoxImage.Question);

                            if (result != MessageBoxResult.Yes)
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取现有试卷信息失败：{ex.Message}\n\n请更换试卷名称。",
                                   "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // 创建新试卷数据
            var examData = new ExamData
            {
                ExamName = ExamNameBox.Text,
                TeacherName = currentTeacher.Name,
                TeacherId = currentTeacher.IdNumber,
                CreationTime = DateTime.Now,
                Questions = allQuestions
            };

            try
            {
                SaveExam(examData);
                LoadExistingExams();
                MessageBox.Show("试卷生成成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存试卷失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 检查当前用户是否可以修改指定试卷
        /// </summary>
        /// <param name="examData">试卷数据</param>
        /// <returns>true=可以修改，false=不可以修改</returns>
        private bool CanModifyExam(ExamData examData)
        {
            // 检查用户的原始身份（Type），而不是当前角色（CurrentRole）
            // 管理员可以修改任意试卷
            if (currentTeacher.Type == UserType.Admin)
            {
                return true;
            }

            // 教师只能修改自己创建的试卷
            if (currentTeacher.Type == UserType.Teacher)
            {
                // 通过教师工号进行匹配（更准确）
                return examData.TeacherId == currentTeacher.IdNumber;
            }

            // 其他身份（如学生）不能修改试卷
            return false;
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

            // 读取试卷文件，检查权限
            string fileName = Path.Combine(EXAMS_DIRECTORY, $"{selectedItem}.json");
            if (!File.Exists(fileName))
            {
                MessageBox.Show("试卷文件不存在！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // 读取试卷数据
                string jsonString = File.ReadAllText(fileName);
                var examData = JsonSerializer.Deserialize<ExamData>(jsonString);

                if (examData == null)
                {
                    MessageBox.Show("试卷文件格式错误！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 权限检查：检查用户的原始身份（Type）
                bool canDelete = CanDeleteExam(examData);

                if (!canDelete)
                {
                    MessageBox.Show($"权限不足！\n\n试卷《{examData.ExamName}》由教师 { examData.TeacherName} 创建，\n您只能删除自己创建的试卷。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 显示详细的确认信息
                string confirmMessage = $"确定要删除选中的试卷吗？\n\n" +
                                       $"试卷名称：{examData.ExamName}\n" +
                                       $"创建教师：{examData.TeacherName}\n" +
                                       $"创建时间：{examData.CreationTime:yyyy-MM-dd HH:mm}\n" +
                                       $"题目数量：{examData.Questions?.Count(q => q.IsChecked) ?? 0}\n\n" +
                                       $"注意：删除操作不可恢复！";

                var result = MessageBox.Show(confirmMessage, "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // 执行删除
                File.Delete(fileName);

                // 从列表中移除
                ExamList.Items.Remove(selectedItem);

                // 清空试卷信息显示
                ExamSummaryText.Text = "";

                MessageBox.Show("试卷删除成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除试卷时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 检查当前用户是否可以删除指定试卷
        /// </summary>
        /// <param name="examData">试卷数据</param>
        /// <returns>true=可以删除，false=不可以删除</returns>
        private bool CanDeleteExam(ExamData examData)
        {
            // 检查用户的原始身份（Type），而不是当前角色（CurrentRole）
            // 管理员可以删除任意试卷
            if (currentTeacher.Type == UserType.Admin)
            {
                return true;
            }

            // 教师只能删除自己创建的试卷
            if (currentTeacher.Type == UserType.Teacher)
            {
                // 通过教师工号进行匹配（更准确）
                return examData.TeacherId == currentTeacher.IdNumber;
            }

            // 其他身份（如学生）不能删除试卷
            return false;
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
                    try
                    {
                        string jsonString = File.ReadAllText(fileName);
                        var examData = JsonSerializer.Deserialize<ExamData>(jsonString);
                        LoadExamData(examData);

                        // 更新试卷信息概述
                        ExamSummaryText.Text = GenerateExamSummary(examData);

                        // 根据权限更新删除按钮状态
                        UpdateDeleteButtonState(examData);
                    }
                    catch (Exception ex)
                    {
                        ExamSummaryText.Text = $"读取试卷信息失败：{ex.Message}";
                        DeleteButton.IsEnabled = false;
                    }
                }
            }
            else
            {
                ExamSummaryText.Text = "";
                DeleteButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// 根据权限更新删除按钮的状态
        /// </summary>
        /// <param name="examData">选中的试卷数据</param>
        private void UpdateDeleteButtonState(ExamData examData)
        {
            if (examData == null)
            {
                DeleteButton.IsEnabled = false;
                DeleteButton.ToolTip = "请先选择试卷";
                return;
            }

            bool canDelete = CanDeleteExam(examData);
            DeleteButton.IsEnabled = canDelete;

            if (canDelete)
            {
                DeleteButton.ToolTip = "删除选中的试卷";
            }
            else
            {
                if (currentTeacher.Type == UserType.Teacher)
                {
                    DeleteButton.ToolTip = $"此试卷由{examData.TeacherName}创建，您只能删除自己创建的试卷";
                }
                else
                {
                    DeleteButton.ToolTip = "您没有删除试卷的权限";
                }
            }
        }

        // 生成试卷信息概述
        private string GenerateExamSummary(ExamData examData)
        {
            if (examData == null) return "";

            int selectedCount = examData.Questions?.Count(q => q.IsChecked) ?? 0;

            // 添加权限提示信息
            string permissionInfo = "";
            bool canDelete = CanDeleteExam(examData);

            if (!canDelete && currentTeacher.Type == UserType.Teacher)
            {
                permissionInfo = "\n⚠️ 注意：您只能删除自己创建的试卷";
            }
            else if (canDelete && currentTeacher.Type == UserType.Admin)
            {
                permissionInfo = "\n✓ 管理员权限：可以删除此试卷";
            }
            else if (canDelete)
            {
                permissionInfo = "\n✓ 您可以删除此试卷";
            }

            return $"试卷名称：{examData.ExamName}\n" +
                   $"出题教师：{examData.TeacherName}\n" +
                   $"教师工号：{examData.TeacherId}\n" +
                   $"题目数量：{selectedCount}\n" +
                   $"创建时间：{examData.CreationTime:yyyy-MM-dd HH:mm}{permissionInfo}";
        }

        private void LoadExamData(ExamData examData)
        {
            if (examData == null) return;

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
            // ExamNameBox.Text = examData.ExamName;
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