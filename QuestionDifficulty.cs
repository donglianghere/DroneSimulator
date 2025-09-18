using System.Text.Json.Serialization;
using System.ComponentModel;

namespace DroneSimulator
{
    /// <summary>
    /// 题目难度等级
    /// </summary>
    public enum QuestionDifficulty
    {
        Easy = 1,       // 简单
        Medium = 2,     // 中等  
        Hard = 3        // 困难
    }

    /// <summary>
    /// 题目类型
    /// </summary>
    public enum TheoryQuestionType
    {
        SingleChoice = 1,    // 单选题
        MultipleChoice = 2   // 多选题
    }

    /// <summary>
    /// 内容分类
    /// </summary>
    public enum TheoryQuestionCategory
    {
        FlightPrinciples = 1,    // 飞行原理
        Structure = 2,           // 结构组成
        ControlAlgorithm = 3,    // 控制算法
        SensorFusion = 4,        // 传感器融合
        FlightSafety = 5,        // 飞行安全
        LawsRegulations = 6      // 法律法规
    }

    /// <summary>
    /// 选择题选项
    /// </summary>
    public class TheoryOption : INotifyPropertyChanged
    {
        private string _text = "";
        private bool _isCorrect = false;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string Text 
        { 
            get => _text; 
            set 
            { 
                _text = value; 
                OnPropertyChanged(nameof(Text)); 
            } 
        }
        
        public bool IsCorrect 
        { 
            get => _isCorrect; 
            set 
            { 
                _isCorrect = value; 
                OnPropertyChanged(nameof(IsCorrect)); 
            } 
        }
        
        public string Explanation { get; set; } = ""; // 选项解释

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 理论题目类
    /// </summary>
    public class TheoryQuestion : INotifyPropertyChanged
    {
        private string _questionStatement = "";
        private bool _isSelected = false;

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string QuestionStatement
        {
            get => _questionStatement;
            set
            {
                _questionStatement = value;
                OnPropertyChanged(nameof(QuestionStatement));
            }
        }

        public TheoryQuestionType Type { get; set; } = TheoryQuestionType.SingleChoice;
        public TheoryQuestionCategory Category { get; set; } = TheoryQuestionCategory.FlightPrinciples;
        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;
        public int Points { get; set; } = 2; // 题目分值
        public bool IsActive { get; set; } = true; // 是否启用

        public string TypeDisplayName => GetTypeDisplayName();
        public string CategoryDisplayName => GetCategoryDisplayName();
        public string DifficultyDisplayName => GetDifficultyDisplayName();

        // 修改正确答案显示属性，支持选项代号显示
        public string CorrectAnswersDisplay
        {
            get
            {
                if (!CorrectAnswers.Any())
                    return "未设置";

                // 如果正确答案是选项代号（A、B、C等），直接显示
                if (CorrectAnswers.All(answer => answer.Length == 1 && answer[0] >= 'A' && answer[0] <= 'F'))
                {
                    return string.Join(", ", CorrectAnswers);
                }

                // 如果正确答案是选项文本，尝试转换为代号显示
                var codes = new List<string>();
                for (int i = 0; i < Options.Count; i++)
                {
                    if (CorrectAnswers.Contains(Options[i].Text))
                    {
                        codes.Add(((char)('A' + i)).ToString());
                    }
                }

                return codes.Any() ? string.Join(", ", codes) : string.Join("; ", CorrectAnswers);
            }
        }

        /// <summary>
        /// 是否被选中（用于批量操作）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                }
            }
        }

        // 选项（最多6个）
        public List<TheoryOption> Options { get; set; } = new();

        // 正确答案，单选时只有一个，多选时可能多个
        public List<string> CorrectAnswers { get; set; } = new();

        // 题目解析
        public string Explanation { get; set; } = "";

        // 创建信息
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string LastModifiedBy { get; set; } = "";

        // 使用统计
        public int UsageCount { get; set; } = 0;
        public double AverageScore { get; set; } = 0.0; // 平均得分率

        // 验证题目完整性
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(QuestionStatement)) return false;
            if (Options.Count < 2 || Options.Count > 6) return false;
            if (!CorrectAnswers.Any()) return false;

            // 验证正确答案是否在选项中
            var optionTexts = Options.Select(o => o.Text).ToList();
            foreach (var answer in CorrectAnswers)
            {
                if (!optionTexts.Contains(answer)) return false;
            }

            // 单选题只能有一个正确答案
            if (Type == TheoryQuestionType.SingleChoice && CorrectAnswers.Count != 1)
                return false;

            return true;
        }

        // 获取正确选项
        public List<TheoryOption> GetCorrectOptions()
        {
            return Options.Where(o => CorrectAnswers.Contains(o.Text)).ToList();
        }

        // 获取显示用的分类名称
        public string GetCategoryDisplayName()
        {
            return Category switch
            {
                TheoryQuestionCategory.FlightPrinciples => "飞行原理",
                TheoryQuestionCategory.Structure => "结构组成",
                TheoryQuestionCategory.ControlAlgorithm => "控制算法",
                TheoryQuestionCategory.SensorFusion => "传感器融合",
                TheoryQuestionCategory.FlightSafety => "飞行安全",
                TheoryQuestionCategory.LawsRegulations => "法规法律",
                _ => "未知分类"
            };
        }

        // 获取显示用的难度名称
        public string GetDifficultyDisplayName()
        {
            return Difficulty switch
            {
                QuestionDifficulty.Easy => "简",
                QuestionDifficulty.Medium => "中等",
                QuestionDifficulty.Hard => "困难",
                _ => "中等"
            };
        }

        // 获取显示用的类型名称
        public string GetTypeDisplayName()
        {
            return Type switch
            {
                TheoryQuestionType.SingleChoice => "单选题",
                TheoryQuestionType.MultipleChoice => "多选题",
                _ => "单选题"
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 理论试卷数据
    /// </summary>
    public class TheoryExamData
    {
        public string ExamId { get; set; } = Guid.NewGuid().ToString();
        public string ExamName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string TeacherId { get; set; } = "";
        public DateTime CreationTime { get; set; } = DateTime.Now;
        public int TotalPoints { get; set; } = 0;
        public int TimeLimit { get; set; } = 60; // 考试时间限制（分钟）
        public List<TheoryQuestion> Questions { get; set; } = new();
        public bool IsActive { get; set; } = false; // 是否为当前考试用卷
        
        // 计算总分
        public void CalculateTotalPoints()
        {
            TotalPoints = Questions.Sum(q => q.Points);
        }
        
        // 获取统计信息
        public string GetStatistics()
        {
            if (!Questions.Any()) return "暂无题目";
            
            var stats = Questions.GroupBy(q => q.Category)
                .Select(g => $"{g.First().GetCategoryDisplayName()}({g.Count()}题)")
                .ToList();
                
            return $"共{Questions.Count}题，{TotalPoints}分：{string.Join("、", stats)}";
        }
    }
}