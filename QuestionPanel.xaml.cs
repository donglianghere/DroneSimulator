using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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

    public class ExamListItem : INotifyPropertyChanged
    {
        private bool _isActive;
        private string _examName = "";
        private bool _isSelected; // 添加选中状态

        public string ExamName
        {
            get => _examName;
            set
            {
                _examName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveIndicator));
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(FontWeight));
            }
        }

        // 添加选中状态属性
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(BorderBrush));
            }
        }

        public string ActiveIndicator => IsActive ? "★ 考卷" : "";

        // 修改背景刷，支持选中高亮
        public Brush BackgroundBrush
        {
            get
            {
                if (IsSelected)
                    return new SolidColorBrush(Color.FromRgb(173, 216, 230)); // 浅蓝色高亮
                if (IsActive)
                    return new SolidColorBrush(Color.FromRgb(232, 245, 233)); // 浅绿色活跃
                return new SolidColorBrush(Colors.White); // 默认白色
            }
        }

        // 添加边框刷属性
        public Brush BorderBrush
        {
            get
            {
                if (IsSelected)
                    return new SolidColorBrush(Color.FromRgb(70, 130, 180)); // 蓝色边框
                return new SolidColorBrush(Color.FromRgb(129, 209, 221)); // 默认边框颜色
            }
        }

        public FontWeight FontWeight => IsActive ? FontWeights.Bold : FontWeights.Normal;

        public DateTime CreationTime { get; set; }
        public string FilePath { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class QuestionPanel : Window
    {
        private UserInfo currentTeacher;
        private List<string> examFiles = new();
        private const string EXAMS_DIRECTORY = "Exams";
        private const string ACTIVE_EXAM_FILE = "active_exam.txt";

        // === 核心字段：仅管理当前选中的题目 ===
        private List<TheoryQuestion> selectedTheoryQuestions = new();
        private List<CircuitQuestion> selectedCircuitQuestions = new();
        private List<FCQuestion> selectedFCQuestions = new();

        // === 题库管理器的引用（仅用于获取题目，不负责管理） ===
        private readonly ITheoryQuestionProvider theoryProvider;
        private readonly ICircuitQuestionProvider circuitProvider;
        private readonly IFCQuestionProvider fcProvider;

        // 添加试卷列表数据
        private ObservableCollection<ExamListItem> examItems = new();
        private ExamListItem? selectedExamItem;

        // 新增：用于缓存所有题目CheckBox的名称
        private List<string>? _questionCheckBoxNames;

        // 理论题库相关字段
        private List<TheoryQuestion> theoryQuestions = new List<TheoryQuestion>();
        private List<TheoryQuestion> currentDisplayedQuestions = new List<TheoryQuestion>();


        public QuestionPanel(UserInfo teacher)
        {
            InitializeComponent();
            currentTeacher = teacher;

            // 初始化题目提供者（暂时使用简单实现）
            theoryProvider = new SimpleTheoryQuestionProvider();
            circuitProvider = new SimpleCircuitQuestionProvider();
            fcProvider = new SimpleFCQuestionProvider();

            // 初始化理论题库
            InitializeTheoryQuestionBank();

            // 绑定数据源
            ExamItemsControl.ItemsSource = examItems;

            InitializeTeacherInfo();
            LoadExistingExams();

            // 在窗口标题中显示当前登录身份信息
            UpdateWindowTitle();

            // 添加试卷名称输入框的实时检查
            ExamNameBox.TextChanged += ExamNameBox_TextChanged;

            // 初始化状态检查（在窗口加载完成后执行）
            this.Loaded += (s, e) => {
                CheckExamNameConflict();

                // ========== 修复：确保在窗口加载完成后执行全选 ==========
                // 延迟执行，确保所有控件都已完全加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 由于CircuitSelectAllRadio默认选中，手动触发全选逻辑
                    if (CircuitSelectAllRadio.IsChecked == true)
                    {
                        CircuitSelectAllRadio_Checked(CircuitSelectAllRadio, new RoutedEventArgs());
                    }

                    UpdateCategoryStats(); // 验证分类统计
                    UpdateSelectionStats(); // 初始化选择统计

                    // 初始化理论题库显示
                    LoadTheoryQuestions();
                }), DispatcherPriority.Loaded);
            };
        }

        // 添加简单的接口实现类
        private class SimpleTheoryQuestionProvider : ITheoryQuestionProvider
        {
            public List<TheoryQuestion> GetAvailableQuestions()
            {
                return TheoryQuestionBankManager.GetAllQuestions();
            }

            public void RefreshQuestions()
            {
                TheoryQuestionBankManager.ReloadQuestions();
            }
        }

        private class SimpleCircuitQuestionProvider : ICircuitQuestionProvider
        {
            public List<CircuitQuestion> GetAvailableQuestions()
            {
                // 暂时返回空列表，后续可以实现具体逻辑
                return new List<CircuitQuestion>();
            }

            public void RefreshQuestions()
            {
                // 暂时空实现
            }
        }

        private class SimpleFCQuestionProvider : IFCQuestionProvider
        {
            public List<FCQuestion> GetAvailableQuestions()
            {
                // 暂时返回空列表，后续可以实现具体逻辑
                return new List<FCQuestion>();
            }

            public void RefreshQuestions()
            {
                // 暂时空实现
            }
        }

        // 添加收集各类题目的方法
        private List<TheoryQuestion> CollectSelectedTheoryQuestions()
        {
            return selectedTheoryQuestions ?? new List<TheoryQuestion>();
        }

        private List<CircuitQuestion> CollectSelectedCircuitQuestions()
        {
            var circuitQuestions = new List<CircuitQuestion>();
            foreach (var checkbox in FindAllCheckBoxes())
            {
                if (checkbox.IsChecked == true)
                {
                    circuitQuestions.Add(new CircuitQuestion(checkbox));
                }
            }
            return circuitQuestions;
        }

        private List<FCQuestion> CollectSelectedFCQuestions()
        {
            return selectedFCQuestions ?? new List<FCQuestion>();
        }

        private void RefreshTheoryQuestionSelection()
        {
            // 刷新理论题目选择界面的逻辑
            LoadTheoryQuestions();
        }

        private void RefreshFCQuestionSelection()
        {
            // 刷新飞控题目选择界面的逻辑
            // 暂时空实现，后续可以添加具体逻辑
        }

        private void ShowExamCreationSummary(MixedExamData examData)
        {
            var message = $"试卷生成成功！\n\n" +
                          $"试卷名称：{examData.ExamName}\n" +
                          $"理论题目：{examData.TheoryQuestions.Count} 题\n" +
                          $"电路题目：{examData.CircuitQuestions.Count} 题\n" +
                          $"飞控题目：{examData.Content?.FCQuestions?.Count ?? 0} 题\n" +
                          $"总题目数：{examData.TotalQuestions} 题";

            MessageBox.Show(message, "试卷生成完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearExamNameInput()
        {
            ExamNameBox.Text = "";
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

        #region 理论题库功能实现

        /// <summary>
        /// 初始化理论题库
        /// </summary>
        private void InitializeTheoryQuestionBank()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 初始化理论题库 ===");
                LoadTheoryQuestions();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化理论题库失败：{ex.Message}");
                MessageBox.Show($"初始化理论题库失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 在 LoadTheoryQuestions() 方法中添加更详细的异常处理

        /// <summary>
        /// 加载理论题目 - 修复版
        /// </summary>
        private void LoadTheoryQuestions()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== QuestionPanel: 开始加载理论题目 ===");

                theoryQuestions = TheoryQuestionBankManager.GetAllQuestions()
                    .Where(q => q.IsActive)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"✅ QuestionPanel: 成功加载了 {theoryQuestions.Count} 道理论题目");

                // 默认显示前5道题目
                if (theoryQuestions.Any())
                {
                    var defaultQuestions = theoryQuestions.Take(5).ToList();
                    DisplayTheoryQuestions(defaultQuestions);
                    selectedTheoryQuestions = new List<TheoryQuestion>(defaultQuestions);
                    System.Diagnostics.Debug.WriteLine($"显示了前 {defaultQuestions.Count} 道题目");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("⚠️ 理论题库为空");
                    ShowEmptyTheoryBank();
                }
            }
            catch (TheoryBankException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 理论题库异常：{ex.Message}");
                ShowTheoryBankError($"理论题库加载失败：\n\n{ex.Message}\n\n可能的解决方案：\n1. 检查程序权限\n2. 检查磁盘空间\n3. 重启应用程序");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 未知异常：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");
                ShowTheoryBankError($"加载理论题目时发生未知错误：\n\n{ex.Message}\n\n请联系技术支持或重启应用程序");
            }
        }

        /// <summary>
        /// 显示空题库提示
        /// </summary>
        private void ShowEmptyTheoryBank()
        {
            TheoryQuestionsPanel.Children.Clear();

            var emptyPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 50, 20, 20)
            };

            var emptyIcon = new TextBlock
            {
                Text = "📚",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var emptyText = new TextBlock
            {
                Text = "理论题库为空,请点击'题库管理'添加理论题目",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Gray)
            };

            var manageButton = new Button
            {
                Content = "打开题库管理",
                Width = 120,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                Margin = new Thickness(0, 20, 0, 0)
            };
            manageButton.Click += ManageTheoryBank_Click;

            emptyPanel.Children.Add(emptyIcon);
            emptyPanel.Children.Add(emptyText);
            emptyPanel.Children.Add(manageButton);

            TheoryQuestionsPanel.Children.Add(emptyPanel);
        }

        /// <summary>
        /// 显示题库错误信息
        /// </summary>
        private void ShowTheoryBankError(string errorMessage)
        {
            TheoryQuestionsPanel.Children.Clear();

            var errorPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 50, 20, 20)
            };

            var errorIcon = new TextBlock
            {
                Text = "⚠️",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var errorText = new TextBlock
            {
                Text = errorMessage,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Red),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 600
            };

            // 添加重试按钮
            var retryButton = new Button
            {
                Content = "重新加载题库",
                Width = 120,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                Margin = new Thickness(0, 20, 10, 0)
            };
            retryButton.Click += (s, e) => {
                try
                {
                    TheoryQuestionBankManager.ReloadQuestions();
                    LoadTheoryQuestions();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"重新加载失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // 添加题库管理按钮
            var manageButton = new Button
            {
                Content = "打开题库管理",
                Width = 120,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                Margin = new Thickness(10, 20, 0, 0)
            };
            manageButton.Click += ManageTheoryBank_Click;

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            buttonPanel.Children.Add(retryButton);
            buttonPanel.Children.Add(manageButton);

            errorPanel.Children.Add(errorIcon);
            errorPanel.Children.Add(errorText);
            errorPanel.Children.Add(buttonPanel);

            TheoryQuestionsPanel.Children.Add(errorPanel);
        }

        /// <summary>
        /// 动态显示理论题目（两列布局）
        /// </summary>
        private void DisplayTheoryQuestions(List<TheoryQuestion> questions)
        {
            try
            {
                TheoryQuestionsPanel.Children.Clear();
                currentDisplayedQuestions = new List<TheoryQuestion>(questions ?? new List<TheoryQuestion>());

                if (questions == null || !questions.Any())
                {
                    ShowEmptyTheoryBank();
                    return;
                }

                // 添加标题
                var titlePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };

                var titleText = new TextBlock
                {
                    Text = $"理论试卷预览（共 {questions.Count} 题）",
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(35, 57, 93))
                };

                titlePanel.Children.Add(titleText);
                TheoryQuestionsPanel.Children.Add(titlePanel);

                // 分两列显示题目
                for (int i = 0; i < questions.Count; i += 2)
                {
                    var rowPanel = new UniformGrid
                    {
                        Columns = 2,
                        Margin = new Thickness(0, 0, 0, 20)
                    };

                    // 左列题目
                    var leftQuestion = questions[i];
                    if (leftQuestion != null)
                    {
                        var leftQuestionPanel = CreateQuestionPanel(leftQuestion, i + 1);
                        rowPanel.Children.Add(leftQuestionPanel);
                    }

                    // 右列题目（如果存在）
                    if (i + 1 < questions.Count)
                    {
                        var rightQuestion = questions[i + 1];
                        if (rightQuestion != null)
                        {
                            var rightQuestionPanel = CreateQuestionPanel(rightQuestion, i + 2);
                            rowPanel.Children.Add(rightQuestionPanel);
                        }
                        else
                        {
                            rowPanel.Children.Add(new Border());
                        }
                    }
                    else
                    {
                        // 如果右列没有题目，添加空白占位
                        rowPanel.Children.Add(new Border());
                    }

                    TheoryQuestionsPanel.Children.Add(rowPanel);
                }

                System.Diagnostics.Debug.WriteLine($"已显示 {questions.Count} 道理论题目");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示理论题目失败：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");
                ShowTheoryBankError($"显示理论题目时发生错误：\n\n{ex.Message}");
            }
        }

        /// <summary>
        /// 创建单个题目面板
        /// </summary>
        private Border CreateQuestionPanel(TheoryQuestion question, int questionNumber)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 57, 93)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                Margin = new Thickness(10),
                Padding = new Thickness(15)
            };

            var mainPanel = new StackPanel();

            // 题目标题行
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // 题号
            var questionNumberText = new TextBlock
            {
                Text = $"{questionNumber}. ",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(35, 57, 93))
            };

            // 题目类型标识
            var typeText = new TextBlock
            {
                Text = question.Type == TheoryQuestionType.SingleChoice ? "[单选]" : "[多选]",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = question.Type == TheoryQuestionType.SingleChoice
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                    : new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                Margin = new Thickness(5, 0, 10, 0)
            };

            // 🔧 修复：安全处理题目ID显示
            string displayId;
            if (string.IsNullOrEmpty(question.Id))
            {
                displayId = "ID: 未知";
            }
            else if (question.Id.Length <= 8)
            {
                // 如果ID长度不超过8个字符，直接显示完整ID
                displayId = $"ID: {question.Id}";
            }
            else
            {
                // 如果ID长度超过8个字符，截取前8个字符并添加省略号
                displayId = $"ID: {question.Id.Substring(0, 8)}...";
            }

            var idText = new TextBlock
            {
                Text = displayId,
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 2, 0, 0)
            };

            headerPanel.Children.Add(questionNumberText);
            headerPanel.Children.Add(typeText);
            headerPanel.Children.Add(idText);

            // 题目陈述
            var questionText = new TextBlock
            {
                Text = question.QuestionStatement ?? "",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                LineHeight = 20
            };

            // 选项列表
            var optionsPanel = new StackPanel();
            char optionLabel = 'A';

            foreach (var option in question.Options ?? new List<TheoryOption>())
            {
                var optionPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                var optionLabelText = new TextBlock
                {
                    Text = $"{optionLabel}. ",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(35, 57, 93)),
                    Width = 20
                };

                var optionContentText = new TextBlock
                {
                    Text = option.Text ?? "",
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (question.CorrectAnswers?.Contains(option.Text) == true)
                        ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // 正确答案用绿色
                        : new SolidColorBrush(Colors.Black)
                };

                // 如果是正确答案，添加标记
                if (question.CorrectAnswers?.Contains(option.Text) == true)
                {
                    optionContentText.FontWeight = FontWeights.Bold;

                    var correctMark = new TextBlock
                    {
                        Text = " ✓",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    optionPanel.Children.Add(optionLabelText);
                    optionPanel.Children.Add(optionContentText);
                    optionPanel.Children.Add(correctMark);
                }
                else
                {
                    optionPanel.Children.Add(optionLabelText);
                    optionPanel.Children.Add(optionContentText);
                }

                optionsPanel.Children.Add(optionPanel);
                optionLabel++;
            }

            // 题目信息底部
            var infoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var difficultyText = new TextBlock
            {
                Text = $"难度：{question.GetDifficultyDisplayName()}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray)
            };

            var categoryText = new TextBlock
            {
                Text = $" | 分类：{question.GetCategoryDisplayName()}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray)
            };

            var pointsText = new TextBlock
            {
                Text = $" | {question.Points}分",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray)
            };

            infoPanel.Children.Add(difficultyText);
            infoPanel.Children.Add(categoryText);
            infoPanel.Children.Add(pointsText);

            // 组装面板
            mainPanel.Children.Add(headerPanel);
            mainPanel.Children.Add(questionText);
            mainPanel.Children.Add(optionsPanel);
            mainPanel.Children.Add(infoPanel);

            border.Child = mainPanel;
            return border;
        }

        #endregion

        #region 理论题库事件处理

        /// <summary>
        /// 理论题库全选事件
        /// </summary>
        private void TheorySelectAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("=== 执行理论题库全选操作 ===");

                    if (!theoryQuestions.Any())
                    {
                        // 尝试重新加载题库
                        LoadTheoryQuestions();
                    }

                    if (theoryQuestions.Any())
                    {
                        selectedTheoryQuestions = new List<TheoryQuestion>(theoryQuestions);
                        DisplayTheoryQuestions(selectedTheoryQuestions);

                        MessageBox.Show($"已选择全部 {selectedTheoryQuestions.Count} 道理论题目", "全选完成",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("理论题库为空，无法执行全选操作！\n\n请点击'题库管理'添加理论题目。", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (TheoryBankException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"理论题库全选失败：{ex.Message}");
                    MessageBox.Show($"理论题库全选失败：\n\n{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"理论题库全选时发生未知错误：{ex.Message}");
                    MessageBox.Show($"执行全选操作时发生错误：\n\n{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 理论题库清空事件
        /// </summary>
        private void TheoryClearAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                selectedTheoryQuestions.Clear();
                TheoryQuestionsPanel.Children.Clear();

                var emptyText = new TextBlock
                {
                    Text = "已清空所有理论题目选择",
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 50, 0, 0),
                    Foreground = new SolidColorBrush(Colors.Gray)
                };

                TheoryQuestionsPanel.Children.Add(emptyText);

                MessageBox.Show("已清空所有理论题目选择", "清空完成",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 理论题库随机选择事件
        /// </summary>
        private void TheoryRandomRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("=== 执行理论题库随机选择操作 ===");

                    if (!theoryQuestions.Any())
                    {
                        // 尝试重新加载题库
                        LoadTheoryQuestions();
                    }

                    if (theoryQuestions.Any())
                    {
                        ExecuteTheoryRandomSelection(showResult: true);
                    }
                    else
                    {
                        MessageBox.Show("理论题库为空，无法执行随机选择！\n\n请点击'题库管理'添加理论题目。", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (TheoryBankException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"理论题库随机选择失败：{ex.Message}");
                    MessageBox.Show($"理论题库随机选择失败：\n\n{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"理论题库随机选择时发生未知错误：{ex.Message}");
                    MessageBox.Show($"执行随机选择时发生错误：\n\n{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 理论题库随机数量选择变化事件
        /// </summary>
        private void TheoryRandomCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TheoryRandomRadio?.IsChecked == true)
            {
                ExecuteTheoryRandomSelection(showResult: false);
            }
        }

        /// <summary>
        /// 执行理论题库随机选择
        /// </summary>
        private void ExecuteTheoryRandomSelection(bool showResult = false)
        {
            try
            {
                int randomCount = GetTheoryRandomCount();

                if (randomCount <= 0)
                {
                    MessageBox.Show("请选择有效的题目数量！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!theoryQuestions.Any())
                {
                    MessageBox.Show("理论题库为空，无法进行随机选择！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 智能平衡选题
                selectedTheoryQuestions = TheoryQuestionBankManager.SelectBalancedQuestions(randomCount);

                if (!selectedTheoryQuestions.Any())
                {
                    // 如果智能选题失败，使用简单随机选择
                    selectedTheoryQuestions = TheoryQuestionBankManager.SelectRandomQuestions(randomCount);
                }

                // 显示选中的题目
                DisplayTheoryQuestions(selectedTheoryQuestions);

                if (showResult)
                {
                    ShowTheoryRandomSelectionResult(selectedTheoryQuestions.Count, randomCount, theoryQuestions.Count);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"理论题库随机选择失败：{ex.Message}");
                MessageBox.Show($"随机选择理论题目失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取理论题库随机选择数量
        /// </summary>
        private int GetTheoryRandomCount()
        {
            try
            {
                // 优先尝试解析文本输入
                if (!string.IsNullOrWhiteSpace(TheoryRandomCountComboBox.Text))
                {
                    if (int.TryParse(TheoryRandomCountComboBox.Text.Trim(), out int textCount))
                    {
                        // 添加合理的范围验证
                        if (textCount > 0 && textCount <= 1000) // 最大1000题的限制
                        {
                            return textCount;
                        }
                        else if (textCount > 1000)
                        {
                            // 如果超过1000，自动限制为1000
                            TheoryRandomCountComboBox.Text = "1000";
                            return 1000;
                        }
                    }
                }

                // 备用：尝试从选中项获取
                if (TheoryRandomCountComboBox?.SelectedItem is ComboBoxItem selectedItem)
                {
                    if (int.TryParse(selectedItem.Content?.ToString(), out int count))
                    {
                        return count;
                    }
                }

                // 默认值
                return 5;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取理论题库随机数量失败: {ex.Message}");
                return 5;
            }
        }

        /// <summary>
        /// 显示理论题库随机选择结果
        /// </summary>
        private void ShowTheoryRandomSelectionResult(int actualCount, int requestedCount, int totalCount)
        {
            string message = $"理论题目随机选择完成！\n\n" +
                            $"请求选择：{requestedCount} 题\n" +
                            $"实际选择：{actualCount} 题\n" +
                            $"题库总数：{totalCount} 题\n\n" +
                            $"已按分类智能平衡选题";

            if (actualCount < requestedCount)
            {
                message += $"\n\n注意：由于题库总数限制，实际选择数量少于请求数量。";
            }

            MessageBox.Show(message, "随机选择结果",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 电路随机数量文本变化事件
        /// </summary>
        private void CircuitRandomCountComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (CircuitRandomRadio?.IsChecked == true)
                {
                    if (int.TryParse(CircuitRandomCountComboBox.Text, out int count) && count > 0)
                    {
                        ExecuteRandomSelection(showResult: false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CircuitRandomCountComboBox 文本变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开题库管理窗口 - 单例模式
        /// </summary>
        private static TheoryQuestionBankWindow? _theoryBankWindowInstance;

        private void ManageTheoryBank_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 开始打开理论题库管理窗口 ===");

                // 🔧 实现单例模式：检查是否已有窗口实例
                if (_theoryBankWindowInstance != null)
                {
                    // 如果窗口实例存在，检查是否还在显示
                    if (_theoryBankWindowInstance.IsVisible)
                    {
                        // 窗口已打开，将其激活并带到前台
                        _theoryBankWindowInstance.Activate();
                        _theoryBankWindowInstance.WindowState = WindowState.Normal;
                        _theoryBankWindowInstance.Focus();

                        System.Diagnostics.Debug.WriteLine("理论题库管理窗口已存在，激活现有窗口");
                        return;
                    }
                    else
                    {
                        // 窗口实例存在但已关闭，清除引用
                        _theoryBankWindowInstance = null;
                    }
                }

                // 先检查理论题库管理器是否正常
                System.Diagnostics.Debug.WriteLine("检查理论题库管理器...");
                var testQuestions = TheoryQuestionBankManager.GetAllQuestions();
                System.Diagnostics.Debug.WriteLine($"理论题库管理器正常，获取到 {testQuestions.Count} 道题目");

                // 创建新的窗口实例
                System.Diagnostics.Debug.WriteLine("创建新的理论题库管理窗口...");
                _theoryBankWindowInstance = new TheoryQuestionBankWindow(currentTeacher.Name);

                // 🔧 重要：监听窗口关闭事件，确保引用被清除
                _theoryBankWindowInstance.Closed += (s, args) =>
                {
                    _theoryBankWindowInstance = null;
                    System.Diagnostics.Debug.WriteLine("理论题库管理窗口已关闭，清除实例引用");

                    // 刷新理论题目选择界面
                    RefreshTheoryQuestionSelection();

                    // 重新加载题目
                    LoadTheoryQuestions();
                };

                System.Diagnostics.Debug.WriteLine("窗口创建成功，准备显示...");

                // 显示窗口
                _theoryBankWindowInstance.Show(); // 使用 Show() 而不是 ShowDialog()，避免阻塞

                System.Diagnostics.Debug.WriteLine("=== 理论题库管理窗口操作完成 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"=== 理论题库管理窗口出错 ===");
                System.Diagnostics.Debug.WriteLine($"错误类型：{ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"错误消息：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");

                // 发生错误时清除实例引用
                _theoryBankWindowInstance = null;

                MessageBox.Show($"打开题库管理窗口失败：\n\n" +
                               $"错误类型：{ex.GetType().Name}\n" +
                               $"错误消息：{ex.Message}\n\n" +
                               $"详细信息请查看调试输出", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 理论题库随机数量文本变化事件
        /// </summary>
        private void TheoryRandomCountComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                // 如果随机选项被选中，且用户手动输入了数字，立即应用
                if (TheoryRandomRadio?.IsChecked == true)
                {
                    // 验证输入的数字是否有效
                    if (int.TryParse(TheoryRandomCountComboBox.Text, out int count) && count > 0)
                    {
                        ExecuteTheoryRandomSelection(showResult: false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TheoryRandomCountComboBox 文本变化处理失败: {ex.Message}");
            }
        }

        // 新增：飞控题库管理入口
        /// <summary>
        /// 管理飞控题库按钮点击事件
        /// </summary>
        private void ManageFCBank_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 暂时显示提示信息，等待 FlightControlQuestionBankWindow 实现
                MessageBox.Show("飞控题库管理功能正在开发中...", "功能提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                var fcBankWindow = new FlightControlQuestionBankWindow(TeacherNameText.Text);
                fcBankWindow.ShowDialog();

                // 刷新飞控题目显示
                RefreshFCQuestions();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开飞控题库管理失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新飞控题目显示
        /// </summary>
        private void RefreshFCQuestions()
        {
            try
            {
                // 清空现有控件
                var fcPanel = FindName("FCQuestionsPanel") as StackPanel;
                if (fcPanel != null)
                {
                    fcPanel.Children.Clear();

                    // 获取所有可用的飞控题目
                    var availableQuestions = FlightControlQuestionBankManager.GetQuestions(isActive: true);

                    foreach (var question in availableQuestions)
                    {
                        var questionControl = CreateFCQuestionControl(question);
                        fcPanel.Children.Add(questionControl);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新飞控题目失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 创建飞控题目控件
        /// </summary>
        private UIElement CreateFCQuestionControl(FlightControlQuestion question)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.LightBlue),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(5),
                Padding = new Thickness(10),
                Background = new SolidColorBrush(Colors.White)
            };

            var stackPanel = new StackPanel();

            // 题目标题
            var titleText = new TextBlock
            {
                Text = question.QuestionStatement,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(titleText);

            // 参数信息
            var paramInfo = new TextBlock
            {
                Text = $"参数：{question.ParameterName} | 类型：{question.TypeDisplayName} | 分值：{question.Points}分",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 0, 0, 5)
            };
            stackPanel.Children.Add(paramInfo);

            // 选择复选框
            var checkBox = new CheckBox
            {
                Content = "选择此题",
                Tag = question.Id,
                Margin = new Thickness(0, 5, 0, 0)
            };
            checkBox.Checked += FCQuestionCheckBox_Changed;
            checkBox.Unchecked += FCQuestionCheckBox_Changed;
            stackPanel.Children.Add(checkBox);

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 飞控题目选择状态改变事件
        /// </summary>
        private void FCQuestionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // 更新选择统计等
            UpdateFCSelectionStats();
        }        

        #endregion


        #region 飞控题库事件处理

        /// <summary>
        /// 飞控题库全选事件
        /// </summary>
        private void FCSelectAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                try
                {
                    selectedFCQuestions = fcProvider.GetAvailableQuestions();
                    RefreshFCQuestionSelection();

                    MessageBox.Show($"已选择全部飞控题目", "全选完成",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"飞控题库全选失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 飞控题库清空事件
        /// </summary>
        private void FCClearAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                try
                {
                    selectedFCQuestions.Clear();
                    RefreshFCQuestionSelection();

                    MessageBox.Show("已清空所有飞控题目选择", "清空完成",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"飞控题库清空失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 飞控题库随机选择事件
        /// </summary>
        private void FCRandomRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                try
                {
                    ExecuteFCRandomSelection(showResult: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"飞控题库随机选择失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 飞控题库随机数量选择变化事件
        /// </summary>
        private void FCRandomCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FCRandomRadio?.IsChecked == true)
            {
                ExecuteFCRandomSelection(showResult: false);
            }
        }

        /// <summary>
        /// 执行飞控题库随机选择
        /// </summary>
        private void ExecuteFCRandomSelection(bool showResult = false)
        {
            try
            {
                int randomCount = GetFCRandomCount();

                if (randomCount <= 0)
                {
                    MessageBox.Show("请选择有效的题目数量！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var availableQuestions = fcProvider.GetAvailableQuestions();

                if (!availableQuestions.Any())
                {
                    MessageBox.Show("飞控题库为空，无法进行随机选择！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var random = new Random();
                selectedFCQuestions = availableQuestions
                    .OrderBy(x => random.Next())
                    .Take(randomCount)
                    .ToList();

                RefreshFCQuestionSelection();

                if (showResult)
                {
                    MessageBox.Show($"飞控题目随机选择完成！\n共选择了 {selectedFCQuestions.Count} 道题目。",
                        "随机选择结果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行飞控随机选择失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取飞控题库随机选择数量
        /// </summary>
        private int GetFCRandomCount()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(FCRandomCountComboBox.Text))
                {
                    if (int.TryParse(FCRandomCountComboBox.Text.Trim(), out int textCount))
                    {
                        if (textCount > 0 && textCount <= 100)
                        {
                            return textCount;
                        }
                        else if (textCount > 100)
                        {
                            FCRandomCountComboBox.Text = "100";
                            return 100;
                        }
                    }
                }

                if (FCRandomCountComboBox?.SelectedItem is ComboBoxItem selectedItem)
                {
                    if (int.TryParse(selectedItem.Content?.ToString(), out int count))
                    {
                        return count;
                    }
                }

                return 3; // 默认值
            }
            catch
            {
                return 3;
            }
        }

        /// <summary>
        /// 更新飞控题目选择统计
        /// </summary>
        private void UpdateFCSelectionStats()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 更新飞控题目选择统计 ===");

                int selectedFCCount = selectedFCQuestions?.Count ?? 0;
                System.Diagnostics.Debug.WriteLine($"当前选中的飞控题目数量: {selectedFCCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新飞控题目选择统计失败: {ex.Message}");
            }
        }

        #endregion

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
            examItems.Clear();

            // 读取考卷试卷设置
            string activeExamName = GetActiveExamName();

            var examFileInfos = new List<(string filePath, string examName, DateTime creationTime)>();

            foreach (string file in Directory.GetFiles(EXAMS_DIRECTORY, "*.json"))
            {
                string examName = Path.GetFileNameWithoutExtension(file);
                DateTime creationTime = File.GetLastWriteTime(file);

                examFiles.Add(file);
                examFileInfos.Add((file, examName, creationTime));
            }

            // 按创建时间排序（最新的在前）
            examFileInfos = examFileInfos.OrderByDescending(x => x.creationTime).ToList();

            // 如果没有设置考卷，自动设置最新的试题为考卷
            if (string.IsNullOrEmpty(activeExamName) && examFileInfos.Count > 0)
            {
                activeExamName = examFileInfos[0].examName;
                SetActiveExamName(activeExamName);
            }

            // 创建试卷列表项
            foreach (var (filePath, examName, creationTime) in examFileInfos)
            {
                var item = new ExamListItem
                {
                    ExamName = examName,
                    IsActive = examName == activeExamName,
                    CreationTime = creationTime,
                    FilePath = filePath
                };
                examItems.Add(item);
            }
        }

        // 获取考卷名称
        private string GetActiveExamName()
        {
            try
            {
                if (File.Exists(ACTIVE_EXAM_FILE))
                {
                    return File.ReadAllText(ACTIVE_EXAM_FILE).Trim();
                }
            }
            catch { }
            return "";
        }

        // 设置考卷名称
        private void SetActiveExamName(string examName)
        {
            try
            {
                if (string.IsNullOrEmpty(examName))
                {
                    if (File.Exists(ACTIVE_EXAM_FILE))
                        File.Delete(ACTIVE_EXAM_FILE);
                }
                else
                {
                    File.WriteAllText(ACTIVE_EXAM_FILE, examName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置考卷失败: {ex.Message}");
            }
        }

        // 单选按钮选中事件
        private void ExamRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag is string examName)
            {
                // 更新所有项的考卷状态
                foreach (var item in examItems)
                {
                    item.IsActive = item.ExamName == examName;
                }

                // 保存考卷设置
                SetActiveExamName(examName);

                // 加载选中试卷的详情
                LoadExamByName(examName);
            }
        }

        // 试卷名称点击事件
        private void ExamName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is string examName)
            {
                // 清除所有项的选中状态
                foreach (var item in examItems)
                {
                    item.IsSelected = false;
                }

                // 设置当前点击项为选中状态
                var clickedItem = examItems.FirstOrDefault(x => x.ExamName == examName);
                if (clickedItem != null)
                {
                    clickedItem.IsSelected = true;
                }

                // 加载试卷详情
                LoadExamByName(examName);
            }
        }

        // 根据试卷名称加载试卷
        private void LoadExamByName(string examName)
        {
            selectedExamItem = examItems.FirstOrDefault(x => x.ExamName == examName);
            if (selectedExamItem != null)
            {
                string fileName = Path.Combine(EXAMS_DIRECTORY, $"{examName}.json");
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
        }

        // 通用的获取右键菜单试卷名称的方法
        private string GetExamNameFromContextMenu(object sender)
        {
            if (sender is not MenuItem menuItem)
                return "";

            // 方法1：直接从 MenuItem.Tag 获取
            if (menuItem.Tag is string examName)
            {
                return examName;
            }

            // 方法2：从父级 ContextMenu 的 PlacementTarget 获取
            if (menuItem.Parent is ContextMenu contextMenu)
            {
                if (contextMenu.PlacementTarget is TextBlock textBlock &&
                    textBlock.Tag is string examName2)
                {
                    return examName2;
                }

                // 方法3：从数据上下文获取
                if (contextMenu.PlacementTarget is FrameworkElement element &&
                    element.DataContext is ExamListItem examItem)
                {
                    return examItem.ExamName;
                }
            }
            return "";
        }

        // 右键菜单：设为考试用卷
        private void SetAsActiveExam_Click(object sender, RoutedEventArgs e)
        {
            string examName = GetExamNameFromContextMenu(sender);

            if (string.IsNullOrEmpty(examName))
            {
                MessageBox.Show("无法获取试卷信息，请重试。", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 更新所有项的活跃状态
            foreach (var item in examItems)
            {
                item.IsActive = item.ExamName == examName;
            }

            // 保存活跃试卷设置
            SetActiveExamName(examName);

            // 加载选中试卷的详情
            LoadExamByName(examName);

            MessageBox.Show($"已将试卷{ examName}设为考试用卷。", "设置成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewExamDetails_Click(object sender, RoutedEventArgs e)
        {
            string examName = GetExamNameFromContextMenu(sender);

            if (string.IsNullOrEmpty(examName))
            {
                MessageBox.Show("无法获取试卷信息，请重试。", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadExamByName(examName);
            MessageBox.Show($"已加载试卷{ examName}的详细信息。", "查看详情", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 右键菜单：取消考试用卷
        private void ClearActiveExam_Click(object sender, RoutedEventArgs e)
        {
            // 取消所有试卷的活跃状态
            foreach (var item in examItems)
            {
                item.IsActive = false;
            }

            // 清除活跃试卷设置
            SetActiveExamName("");

            MessageBox.Show("已取消考试用卷设置。学生将自动使用最新创建的试卷。", "取消成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }        

        // 查找指定试卷名的单选按钮
        private RadioButton? FindRadioButtonByExamName(string examName)
        {
            // 这里可以通过遍历可视化树来查找，但简化处理
            // 实际上单选按钮的选中会通过数据绑定自动处理
            return null;
        }

        // 修改原有的 ExamList_SelectionChanged 方法名，因为我们不再使用 ListBox
        // 可以删除这个方法，或者重命名为备用

        // 获取学生应该使用的试卷
        public static string GetActiveExamForStudent()
        {
            try
            {
                const string ACTIVE_EXAM_FILE = "active_exam.txt";
                const string EXAMS_DIRECTORY = "Exams";

                // 首先检查是否有考卷设置
                if (File.Exists(ACTIVE_EXAM_FILE))
                {
                    string activeExam = File.ReadAllText(ACTIVE_EXAM_FILE).Trim();
                    string activeExamPath = Path.Combine(EXAMS_DIRECTORY, $"{activeExam}.json");

                    if (!string.IsNullOrEmpty(activeExam) && File.Exists(activeExamPath))
                    {
                        return activeExamPath;
                    }
                }

                // 如果没有考卷或考卷不存在，返回最新的试卷为考卷
                if (Directory.Exists(EXAMS_DIRECTORY))
                {
                    var files = Directory.GetFiles(EXAMS_DIRECTORY, "*.json");
                    if (files.Length > 0)
                    {
                        return files.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                    }
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        private void CircuitSelectAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 确保仅在 RadioButton 真正被选中时执行
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                System.Diagnostics.Debug.WriteLine("=== 执行全选操作 ===");
                var checkboxes = FindAllCheckBoxes().ToList();
                System.Diagnostics.Debug.WriteLine($"找到 {checkboxes.Count} 个题目控件");

                foreach (var checkbox in checkboxes)
                {
                    checkbox.SetCurrentValue(CheckBox.IsCheckedProperty, true);
                }

                System.Diagnostics.Debug.WriteLine("全选操作完成");
                UpdateSelectionStats();
            }
        }

        private void CircuitClearAllRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 确保仅在 RadioButton 真正被选中时执行
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                System.Diagnostics.Debug.WriteLine("=== 执行清空操作 ===");
                var checkboxes = FindAllCheckBoxes().ToList();
                System.Diagnostics.Debug.WriteLine($"找到 {checkboxes.Count} 个题目控件");

                foreach (var checkbox in checkboxes)
                {
                    checkbox.SetCurrentValue(CheckBox.IsCheckedProperty, false);
                }

                System.Diagnostics.Debug.WriteLine("清空操作完成");
                UpdateSelectionStats();
            }
        }

        /// <summary>
        /// 随机题目数量下拉列表选择变化事件处理
        /// </summary>
        private void CircuitRandomCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 检查 RandomRadio 是否处于选中状态
                if (CircuitRandomRadio?.IsChecked == true)
                {
                    // 如果随机选项被选中，则执行随机选题（不显示结果对话框）
                    ExecuteRandomSelection(showResult: false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CircuitRandomCountComboBox 选择变化处理失败: {ex.Message}");

                // 可选：显示错误提示给用户
                MessageBox.Show($"更新随机选择时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 随机选择事件处理方法
        /// </summary>
        private void CircuitRandomRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 确保仅在 RadioButton 真正被选中时执行
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                System.Diagnostics.Debug.WriteLine("=== 执行随机选择操作 ===");
                ExecuteRandomSelection(showResult: true);  // 显示结果
            }
        }

        /// <summary>
        /// 执行随机选择逻辑（提取公共方法）
        /// </summary>
        /// <param name="showResult">是否显示结果对话框</param>
        private void ExecuteRandomSelection(bool showResult = false)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 开始执行随机选择 ===");

                // 获取随机选择的题目数量
                int randomCount = GetRandomCount();
                System.Diagnostics.Debug.WriteLine($"随机选择数量: {randomCount}");

                if (randomCount <= 0)
                {
                    MessageBox.Show("请选择有效的题目数量！", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 获取所有可用的题目
                var allCheckboxes = FindAllCheckBoxes().ToList();
                System.Diagnostics.Debug.WriteLine($"找到的题目数量: {allCheckboxes.Count}");

                if (allCheckboxes.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("❌ 未找到任何题目控件！");
                    MessageBox.Show("未找到可选择的题目！\n\n调试信息：请检查控件是否正确加载。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 先清空所有选择
                foreach (var checkbox in allCheckboxes)
                {
                    checkbox.SetCurrentValue(CheckBox.IsCheckedProperty, false);
                }

                // 确保随机数量不超过总题目数
                int actualCount = Math.Min(randomCount, allCheckboxes.Count);

                // 使用随机算法选择题目
                var random = new Random();
                var selectedIndexes = new HashSet<int>();

                while (selectedIndexes.Count < actualCount)
                {
                    int randomIndex = random.Next(allCheckboxes.Count);
                    selectedIndexes.Add(randomIndex);
                }

                // 设置选中的题目
                foreach (int index in selectedIndexes)
                {
                    allCheckboxes[index].SetCurrentValue(CheckBox.IsCheckedProperty, true);
                }

                System.Diagnostics.Debug.WriteLine($"随机选择完成：选中了 {actualCount} 个题目");

                // 根据参数决定是否显示结果统计
                if (showResult)
                {
                    ShowRandomSelectionResult(actualCount, randomCount, allCheckboxes.Count);
                }

                // 更新题库统计页面的选择统计（如果存在）
                UpdateSelectionStats();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"随机选择失败: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"随机选择题目时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取随机选择的题目数量
        /// </summary>
        /// <returns>选择的题目数量</returns>
        private int GetRandomCount()
        {
            try
            {
                // 优先尝试解析文本输入
                if (!string.IsNullOrWhiteSpace(CircuitRandomCountComboBox.Text))
                {
                    if (int.TryParse(CircuitRandomCountComboBox.Text.Trim(), out int textCount))
                    {
                        if (textCount > 0 && textCount <= 100) // 电路题目限制为100题
                        {
                            return textCount;
                        }
                        else if (textCount > 100)
                        {
                            CircuitRandomCountComboBox.Text = "100";
                            return 100;
                        }
                    }
                }

                // 备用：从选中项获取
                if (CircuitRandomCountComboBox?.SelectedItem is ComboBoxItem selectedItem)
                {
                    if (int.TryParse(selectedItem.Content?.ToString(), out int count))
                    {
                        return count;
                    }
                }

                return 4; // 默认值
            }
            catch
            {
                return 4;
            }
        }

        /// <summary>
        /// 显示随机选择结果统计
        /// </summary>
        /// <param name="actualCount">实际选择的题目数</param>
        /// <param name="requestedCount">请求的题目数</param>
        /// <param name="totalCount">总题目数</param>
        private void ShowRandomSelectionResult(int actualCount, int requestedCount, int totalCount)
        {
            string message = $"随机选择完成！\n\n" +
                            $"请求选择：{requestedCount} 题\n" +
                            $"实际选择：{actualCount} 题\n" +
                            $"题库总数：{totalCount} 题";

            if (actualCount < requestedCount)
            {
                message += $"\n\n注意：由于题库总数限制，实际选择数量少于请求数量。";
            }

            MessageBox.Show(message, "随机选择结果",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 更新选择统计信息（在题库统计页面显示）
        /// </summary>
        private void UpdateSelectionStats()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 开始更新选择统计 ===");

                if (SelectionStatsText == null)
                {
                    System.Diagnostics.Debug.WriteLine("SelectionStatsText 为 null");
                    return;
                }

                // 获取所有 CheckBox
                var allCheckboxes = FindAllCheckBoxes().ToList();
                System.Diagnostics.Debug.WriteLine($"找到的所有 CheckBox 数量: {allCheckboxes.Count}");

                // 获取有效题目（有 CommandString 的 CheckBox）
                var allValidCheckboxes = allCheckboxes
                    .Where(cb => !string.IsNullOrEmpty(CheckBoxCommandHelper.GetCommandString(cb)))
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"有效题目数量: {allValidCheckboxes.Count}");

                // 调试：输出前5个有效题目的信息
                for (int i = 0; i < Math.Min(5, allValidCheckboxes.Count); i++)
                {
                    var cb = allValidCheckboxes[i];
                    var commandString = CheckBoxCommandHelper.GetCommandString(cb);
                    System.Diagnostics.Debug.WriteLine($"题目 {i + 1}: 名称={cb.Name}, CommandString={commandString}, 选中={cb.IsChecked}");
                }

                // 获取选中的题目
                var selectedCheckboxes = allValidCheckboxes
                    .Where(cb => cb.IsChecked == true)
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"选中题目数量: {selectedCheckboxes.Count}");

                int selectedCount = selectedCheckboxes.Count;
                int totalCount = allValidCheckboxes.Count;

                // 按分类统计选择情况
                var motorSelected = selectedCheckboxes.Count(cb => cb.Name.StartsWith("M"));
                var escSelected = selectedCheckboxes.Count(cb => cb.Name.StartsWith("ESC"));
                var pwmSelected = selectedCheckboxes.Count(cb => cb.Name.StartsWith("S"));
                var gpsSelected = selectedCheckboxes.Count(cb => cb.Name.StartsWith("UART") || cb.Name.StartsWith("GPS5V"));
                var otherSelected = selectedCheckboxes.Count(cb =>
                    cb.Name.StartsWith("Receiver") || cb.Name.StartsWith("SERVO") || cb.Name.StartsWith("Battery"));

                System.Diagnostics.Debug.WriteLine($"分类统计 - 电机:{motorSelected}, 电调:{escSelected}, PWM:{pwmSelected}, GPS:{gpsSelected}, 其他:{otherSelected}");

                // 计算百分比，避免除零错误
                double percentage = totalCount > 0 ? (double)selectedCount / totalCount * 100 : 0;

                // 生成详细的统计信息
                string statsText = $"📊 当前选择统计：\n\n" +
                                  $"📈 总体情况：已选择 {selectedCount} / {totalCount} 题（{percentage:F1}%）\n\n" +
                                  $"📋 分类详情：\n" +
                                  $"🔧 电机题目：{motorSelected} 题\n" +
                                  $"⚡ 电调题目：{escSelected} 题\n" +
                                  $"📡 PWM输出：{pwmSelected} 题\n" +
                                  $"📍 GPS题目：{gpsSelected} 题\n" +
                                  $"🔗 其他组件：{otherSelected} 题\n\n";

                // 添加选择状态提示
                if (selectedCount == 0)
                {
                    statsText += "⚠️ 提示：当前未选择任何题目，生成试卷时将无法保存！";
                }
                else if (selectedCount == totalCount)
                {
                    statsText += "✅ 状态：已选择全部题目！";
                }
                else
                {
                    statsText += $"✅ 状态：已选择部分题目，可以生成包含 {selectedCount} 道题的试卷。";
                }

                SelectionStatsText.Text = statsText;
                System.Diagnostics.Debug.WriteLine("=== 选择统计更新完成 ===");
                System.Diagnostics.Debug.WriteLine($"显示的文本: {statsText}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新选择统计失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");

                if (SelectionStatsText != null)
                {
                    SelectionStatsText.Text = $"统计信息更新失败：{ex.Message}";
                }
            }
        }

        /// <summary>
        /// 动态生成并验证题目分类统计
        /// </summary>
        private void UpdateCategoryStats()
        {
            try
            {
                // 获取所有有效题目
                var allValidCheckboxes = FindAllCheckBoxes()
                    .Where(cb => !string.IsNullOrEmpty(CheckBoxCommandHelper.GetCommandString(cb)))
                    .ToList();

                // 按分类统计题目数量
                var motorCount = allValidCheckboxes.Count(cb => cb.Name.StartsWith("M"));
                var escCount = allValidCheckboxes.Count(cb => cb.Name.StartsWith("ESC"));
                var pwmCount = allValidCheckboxes.Count(cb => cb.Name.StartsWith("S"));
                var gpsCount = allValidCheckboxes.Count(cb => cb.Name.StartsWith("UART") || cb.Name.StartsWith("GPS5V"));
                var otherCount = allValidCheckboxes.Count(cb =>
                    cb.Name.StartsWith("Receiver") || cb.Name.StartsWith("SERVO") || cb.Name.StartsWith("Battery"));

                int totalCount = allValidCheckboxes.Count;

                // 输出调试信息以验证统计数据
                System.Diagnostics.Debug.WriteLine($"=== 题目分类统计验证 ===");
                System.Diagnostics.Debug.WriteLine($"电机题目：{motorCount} 题");
                System.Diagnostics.Debug.WriteLine($"电调题目：{escCount} 题");
                System.Diagnostics.Debug.WriteLine($"PWM输出题目：{pwmCount} 题");
                System.Diagnostics.Debug.WriteLine($"GPS题目：{gpsCount} 题");
                System.Diagnostics.Debug.WriteLine($"其他组件题目：{otherCount} 题");
                System.Diagnostics.Debug.WriteLine($"动态统计总计：{totalCount} 题");
                System.Diagnostics.Debug.WriteLine($"XAML固定显示：51 题");

                // 如果总数不匹配，在调试输出中显示警告
                if (totalCount != 51)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 警告：动态统计({totalCount})与XAML显示(51)不一致！");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新分类统计失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 按分类随机选择题目（高级功能）
        /// </summary>
        /// <param name="randomCount">总的随机题目数</param>
        private void RandomSelectByCategory(int randomCount)
        {
            try
            {
                var categories = new Dictionary<string, List<CheckBox>>
                {
                    ["电机"] = FindCheckBoxesByPrefix("M").ToList(),
                    ["电调"] = FindCheckBoxesByPrefix("ESC").ToList(),
                    ["PWM输出"] = FindCheckBoxesByPrefix("S").ToList(),
                    ["GPS"] = FindCheckBoxesByPrefix("UART", "GPS5V").ToList(),
                    ["其他"] = FindCheckBoxesByPrefix("Receiver", "SERVO", "Battery").ToList()
                };

                // 先清空所有选择
                foreach (var checkbox in FindAllCheckBoxes())
                {
                    checkbox.IsChecked = false;
                }

                var random = new Random();
                var selectedCheckboxes = new List<CheckBox>();

                // 从每个分类中平均选择题目
                int perCategory = randomCount / categories.Count;
                int remainder = randomCount % categories.Count;

                foreach (var category in categories)
                {
                    var categoryCheckboxes = category.Value;
                    if (categoryCheckboxes.Count == 0) continue;

                    int countForThisCategory = perCategory;
                    if (remainder > 0)
                    {
                        countForThisCategory++;
                        remainder--;
                    }

                    // 从当前分类随机选择题目
                    var shuffled = categoryCheckboxes.OrderBy(x => random.Next()).ToList();
                    var selected = shuffled.Take(Math.Min(countForThisCategory, categoryCheckboxes.Count));

                    foreach (var checkbox in selected)
                    {
                        checkbox.IsChecked = true;
                        selectedCheckboxes.Add(checkbox);
                    }
                }

                MessageBox.Show($"按分类随机选择完成！\n共选择了 {selectedCheckboxes.Count} 道题目。",
                    "分类随机选择", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"分类随机选择失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 根据前缀查找CheckBox
        /// </summary>
        /// <param name="prefixes">前缀数组</param>
        /// <returns>匹配的CheckBox集合</returns>
        private IEnumerable<CheckBox> FindCheckBoxesByPrefix(params string[] prefixes)
        {
            return FindAllCheckBoxes().Where(cb =>
                prefixes.Any(prefix => cb.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        }

        // 在 GenerateButton_Click 方法中添加理论题目支持
        // === 专注于试卷管理的方法 ===
        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExamNameBox.Text))
            {
                MessageBox.Show("请输入试卷名称！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 收集当前界面选中的题目
            var examContent = CollectSelectedQuestions();

            if (!examContent.HasAnyQuestions)
            {
                MessageBox.Show("请至少选择一道题目！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 生成试卷
            var examData = new MixedExamData
            {
                ExamName = ExamNameBox.Text,
                TeacherName = currentTeacher.Name,
                TeacherId = currentTeacher.IdNumber,
                CreationTime = DateTime.Now,
                Content = examContent
            };

            try
            {
                ExamFileManager.SaveExam(examData);
                LoadExistingExams();

                ShowExamCreationSummary(examData);
                ClearExamNameInput();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存试卷失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ExamContent CollectSelectedQuestions()
        {
            return new ExamContent
            {
                TheoryQuestions = CollectSelectedTheoryQuestions(),
                CircuitQuestions = CollectSelectedCircuitQuestions(),
                FCQuestions = CollectSelectedFCQuestions()
            };
        }
        /// <summary>
        /// 保存混合试卷
        /// </summary>
        private void SaveMixedExam(MixedExamData examData)
        {
            try
            {
                // 为了兼容现有系统，将混合试卷转换为ExamData格式保存
                var compatibleExamData = new ExamData
                {
                    ExamName = examData.ExamName,
                    TeacherName = examData.TeacherName,
                    TeacherId = examData.TeacherId,
                    CreationTime = examData.CreationTime,
                    // 🔧 修复：显式转换 CircuitQuestion 为 Question
                    Questions = examData.CircuitQuestions.Select(cq => new Question
                    {
                        Name = cq.Name,
                        Content = cq.Content,
                        IsChecked = cq.IsChecked,
                        CommandString = cq.CommandString
                    }).ToList()
                };

                // 保存电路题目（保持现有格式）
                string fileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}.json");
                string jsonString = JsonSerializer.Serialize(compatibleExamData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(fileName, jsonString);

                // 如果有理论题目，单独保存
                if (examData.TheoryQuestions.Any())
                {
                    string theoryFileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}_theory.json");
                    string theoryJsonString = JsonSerializer.Serialize(examData.TheoryQuestions, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(theoryFileName, theoryJsonString);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"保存混合试卷失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CheckExamNameConflict();

            // 延迟执行，确保所有控件都已完全加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 由于CircuitSelectAllRadio默认选中，手动触发全选逻辑
                if (CircuitSelectAllRadio.IsChecked == true)
                {
                    CircuitSelectAllRadio_Checked(CircuitSelectAllRadio, new RoutedEventArgs());
                }

                UpdateCategoryStats(); // 验证分类统计
                UpdateSelectionStats(); // 初始化选择统计
                UpdateExamRecordsStats(); // 初始化考试记录统计
            }), DispatcherPriority.Loaded);
        }

        // 修改 TabControl_SelectionChanged 方法，添加考试记录统计的更新
        /// <summary>
        /// TabControl 选择变化事件处理
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is TabControl tabControl)
                {
                    // 检查是否切换到了"统计信息"页面
                    if (tabControl.SelectedItem is TabItem selectedTab)
                    {
                        // 通过 Header 内容判断是否是统计信息页面
                        if (selectedTab.Header?.ToString() == "统计信息")
                        {
                            // 当切换到统计信息页面时，自动更新所有统计
                            UpdateSelectionStats();
                            UpdateCategoryStats();
                            UpdateExamRecordsStats(); // 新增：更新考试记录统计
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TabControl 选择变化处理失败: {ex.Message}");
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
            // 使用新的选中项逻辑
            if (selectedExamItem == null)
            {
                MessageBox.Show("请先选择要删除的试卷。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 读取试卷文件，检查权限
            string fileName = Path.Combine(EXAMS_DIRECTORY, $"{selectedExamItem.ExamName}.json");
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
                    MessageBox.Show($"权限不足！\n\n试卷《{examData.ExamName}》由教师 { examData.TeacherName} 创建，\n您只能删除自己创建的试卷。", 
                           "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                // 如果删除的是考卷，清除考卷状态
                if (selectedExamItem.IsActive)
                {
                    SetActiveExamName("");
                }

                // 重新加载试卷列表
                LoadExistingExams();

                // 清空试卷信息显示
                ExamSummaryText.Text = "";
                selectedExamItem = null;

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
            // 如果尚未缓存题目控件的名称
            if (_questionCheckBoxNames == null)
            {
                System.Diagnostics.Debug.WriteLine("首次运行：动态发现并缓存题目控件名称...");
                _questionCheckBoxNames = new List<string>();

                // 方法1：首先尝试通过可视化树查找
                var allCheckboxes = new List<CheckBox>();
                FindVisualChildren<CheckBox>(this, allCheckboxes);

                foreach (var cb in allCheckboxes)
                {
                    // 只将作为"题目"的CheckBox（即设置了CommandString）的名称加入缓存
                    if (!string.IsNullOrEmpty(CheckBoxCommandHelper.GetCommandString(cb)) && !string.IsNullOrEmpty(cb.Name))
                    {
                        _questionCheckBoxNames.Add(cb.Name);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"通过可视化树发现了 {_questionCheckBoxNames.Count} 个题目控件。");

                // 方法2：如果通过可视化树找不到足够的控件，使用已知名称列表作为备用
                if (_questionCheckBoxNames.Count < 10) // 假设至少应该有10个题目
                {
                    System.Diagnostics.Debug.WriteLine("可视化树查找结果不足，使用已知控件名称列表...");

                    var knownNames = new[] {
                "M1_1", "M1_2", "M1_3", "M1_4", "M2_1", "M2_2", "M2_3", "M2_4",
                "M3_1", "M3_2", "M3_3", "M3_4", "M4_1", "M4_2", "M4_3", "M4_4",
                "ESC1_1", "ESC1_2", "ESC1_3", "ESC2_1", "ESC2_2", "ESC2_3",
                "ESC3_1", "ESC3_2", "ESC3_3", "ESC4_1", "ESC4_2", "ESC4_3",
                "S5_1", "S5_2", "S5_3", "S6_1", "S6_2", "S6_3",
                "S7_1", "S7_2", "S7_3", "S8_1", "S8_2", "S8_3",
                "UART_1", "UART_2", "GPS5V_1", "GPS5V_2",
                "Receiver_1", "Receiver_2", "Receiver_3",
                "SERVO_1", "SERVO_2", "Battery_1", "Battery_2"
            };

                    _questionCheckBoxNames.Clear();
                    foreach (var name in knownNames)
                    {
                        var cb = FindName(name) as CheckBox;
                        if (cb != null && !string.IsNullOrEmpty(CheckBoxCommandHelper.GetCommandString(cb)))
                        {
                            _questionCheckBoxNames.Add(name);
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"通过已知名称列表发现了 {_questionCheckBoxNames.Count} 个题目控件。");
                }
            }

            // 使用缓存的名称列表，通过 FindName 安全地获取控件
            var result = new List<CheckBox>();
            foreach (var name in _questionCheckBoxNames)
            {
                if (FindName(name) is CheckBox cb)
                {
                    result.Add(cb);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 警告：无法通过名称 '{name}' 找到CheckBox控件");
                }
            }

            System.Diagnostics.Debug.WriteLine($"最终返回 {result.Count} 个CheckBox控件");
            return result;
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

        // 在 QuestionPanel 类中添加考试记录统计相关字段和方法

        /// <summary>
        /// 刷新考试记录统计
        /// </summary>
        private void RefreshRecordsStats_Click(object sender, RoutedEventArgs e)
        {
            UpdateExamRecordsStats();
        }

        /// <summary>
        /// 查看详细考试记录
        /// </summary>
        private void ViewDetailedRecords_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var recordsWindow = new ExamRecordsWindow(currentTeacher);
                recordsWindow.ShowDialog();

                // 关闭详细记录窗口后刷新统计
                UpdateExamRecordsStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开考试记录窗口失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出考试记录统计
        /// </summary>
        private void ExportRecordsStats_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"考试记录统计_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ExportExamRecordsStatsToCsv(saveDialog.FileName);
                    MessageBox.Show("统计数据导出成功！", "导出完成",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新考试记录统计信息
        /// </summary>
        private void UpdateExamRecordsStats()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 开始更新考试记录统计 ===");

                // 获取所有考试记录
                var allRecords = ExamRecordManager.GetAllExamRecords();

                if (allRecords.Count == 0)
                {
                    // 没有考试记录时显示默认信息
                    ShowEmptyRecordsStats();
                    return;
                }

                // 基础统计
                int totalRecords = allRecords.Count;
                int totalStudents = allRecords.Select(r => r.StudentId).Distinct().Count();
                DateTime latestExamDate = allRecords.Max(r => r.SubmitTime);
                DateTime oldestExamDate = allRecords.Min(r => r.SubmitTime);

                // 分数统计
                var scores = allRecords.Select(r => r.Score).ToList();
                double averageScore = scores.Average();
                int highestScore = scores.Max();
                int lowestScore = scores.Min();
                int passCount = scores.Count(s => s >= 60); // 假设60分及格
                double passRate = (double)passCount / totalRecords * 100;

                // 试卷统计
                var examGroups = allRecords
                    .Where(r => r.ExamInfo != null)
                    .GroupBy(r => r.ExamInfo.ExamName)
                    .ToList();
                int totalExams = examGroups.Count;
                string mostUsedExam = totalExams > 0
                    ? examGroups.OrderByDescending(g => g.Count()).First().Key
                    : "无";

                // 教师统计
                var teacherGroups = allRecords
                    .Where(r => r.ExamInfo != null)
                    .GroupBy(r => r.ExamInfo.TeacherName)
                    .ToList();
                int totalTeachers = teacherGroups.Count;
                string mostActiveTeacher = totalTeachers > 0
                    ? teacherGroups.OrderByDescending(g => g.Count()).First().Key
                    : "无";

                // 时间统计
                var times = allRecords.Select(r => r.ElapsedTime).ToList();
                TimeSpan averageTime = TimeSpan.FromMilliseconds(times.Average(t => t.TotalMilliseconds));
                TimeSpan fastestTime = times.Min();
                TimeSpan slowestTime = times.Max();

                // 更新UI显示
                Dispatcher.Invoke(() =>
                {
                    // 基础统计
                    TotalRecordsText.Text = $"总考试记录：{totalRecords} 份";
                    TotalStudentsText.Text = $"参考学生人数：{totalStudents} 人";
                    LatestExamDateText.Text = $"最新考试时间：{latestExamDate:yyyy-MM-dd HH:mm}";
                    OldestExamDateText.Text = $"最早考试时间：{oldestExamDate:yyyy-MM-dd HH:mm}";

                    // 分数统计
                    AverageScoreText.Text = $"平均分：{averageScore:F1} 分";
                    HighestScoreText.Text = $"最高分：{highestScore} 分";
                    LowestScoreText.Text = $"最低分：{lowestScore} 分";
                    PassRateText.Text = $"及格率：{passRate:F1} %";

                    // 试卷统计
                    TotalExamsText.Text = $"使用的试卷数：{totalExams} 份";
                    MostUsedExamText.Text = $"使用最多的试卷：{mostUsedExam}";

                    // 教师统计
                    TotalTeachersText.Text = $"出题教师数：{totalTeachers} 人";
                    MostActiveTeacherText.Text = $"最活跃教师：{mostActiveTeacher}";

                    // 时间统计
                    AverageTimeText.Text = $"平均答题时间：{FormatTimeSpan(averageTime)}";
                    FastestTimeText.Text = $"最快完成时间：{FormatTimeSpan(fastestTime)}";
                    SlowestTimeText.Text = $"最慢完成时间：{FormatTimeSpan(slowestTime)}";

                    // 更新最近记录预览（显示最近10条）
                    var recentRecords = allRecords
                        .OrderByDescending(r => r.SubmitTime)
                        .Take(10)
                        .Select(r => new DetailedExamRecordViewModel(r))
                        .ToList();

                    RecentRecordsDataGrid.ItemsSource = recentRecords;
                });

                System.Diagnostics.Debug.WriteLine("=== 考试记录统计更新完成 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新考试记录统计失败: {ex.Message}");

                Dispatcher.Invoke(() =>
                {
                    TotalRecordsText.Text = "统计数据加载失败";
                    MessageBox.Show($"加载考试记录统计失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }

        /// <summary>
        /// 显示空记录统计信息
        /// </summary>
        private void ShowEmptyRecordsStats()
        {
            Dispatcher.Invoke(() =>
            {
                // 基础统计
                TotalRecordsText.Text = "总考试记录：0 份";
                TotalStudentsText.Text = "参考学生人数：0 人";
                LatestExamDateText.Text = "最新考试时间：无";
                OldestExamDateText.Text = "最早考试时间：无";

                // 分数统计
                AverageScoreText.Text = "平均分：-- 分";
                HighestScoreText.Text = "最高分：-- 分";
                LowestScoreText.Text = "最低分：-- 分";
                PassRateText.Text = "及格率：-- %";

                // 试卷统计
                TotalExamsText.Text = "使用的试卷数：0 份";
                MostUsedExamText.Text = "使用最多的试卷：无";

                // 教师统计
                TotalTeachersText.Text = "出题教师数：0 人";
                MostActiveTeacherText.Text = "最活跃教师：无";

                // 时间统计
                AverageTimeText.Text = "平均答题时间：--";
                FastestTimeText.Text = "最快完成时间：--";
                SlowestTimeText.Text = "最慢完成时间：--";

                // 清空最近记录
                RecentRecordsDataGrid.ItemsSource = null;
            });
        }

        /// <summary>
        /// 格式化时间跨度显示
        /// </summary>
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
            else
            {
                return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
        }

        /// <summary>
        /// 导出考试记录统计到CSV文件
        /// </summary>
        private void ExportExamRecordsStatsToCsv(string fileName)
        {
            try
            {
                var allRecords = ExamRecordManager.GetAllExamRecords();

                using var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8);

                // 写入统计摘要
                writer.WriteLine("=== 考试记录统计摘要 ===");
                writer.WriteLine($"导出时间,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"总考试记录数,{allRecords.Count}");

                if (allRecords.Count > 0)
                {
                    var scores = allRecords.Select(r => r.Score).ToList();
                    var times = allRecords.Select(r => r.ElapsedTime).ToList();

                    writer.WriteLine($"参考学生人数,{allRecords.Select(r => r.StudentId).Distinct().Count()}");
                    writer.WriteLine($"平均分,{scores.Average():F1}");
                    writer.WriteLine($"最高分,{scores.Max()}");
                    writer.WriteLine($"最低分,{scores.Min()}");
                    writer.WriteLine($"及格率,{(double)scores.Count(s => s >= 60) / allRecords.Count * 100:F1}%");
                    writer.WriteLine($"平均答题时间,{FormatTimeSpan(TimeSpan.FromMilliseconds(times.Average(t => t.TotalMilliseconds)))}");
                }

                writer.WriteLine(); // 空行分隔

                // 写入详细记录表头
                writer.WriteLine("=== 详细考试记录 ===");
                writer.WriteLine("序号,学生姓名,身份证号,试卷名称,出题教师,得分,正确答题,错误答题,答题耗时,开始时间,提交时间");

                // 写入详细记录数据
                foreach (var record in allRecords.OrderBy(r => r.ExamSequence))
                {
                    writer.WriteLine($"{record.ExamSequence}," +
                        $"{record.StudentName}," +
                        $"{record.StudentId}," +
                        $"{record.ExamInfo?.ExamName ?? ""}," +
                        $"{record.ExamInfo?.TeacherName ?? ""}," +
                        $"{record.Score}," +
                        $"{record.CorrectAnswers}," +
                        $"{record.WrongAnswers}," +
                        $"{FormatTimeSpan(record.ElapsedTime)}," +
                        $"{record.StartTime:yyyy-MM-dd HH:mm:ss}," +
                        $"{record.SubmitTime:yyyy-MM-dd HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"导出CSV文件失败：{ex.Message}");
            }
        }

        private void SaveExam(ExamData examData)
        {
            string fileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}.json");
            string jsonString = JsonSerializer.Serialize(examData);
            File.WriteAllText(fileName, jsonString);
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