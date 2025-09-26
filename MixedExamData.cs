using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DroneSimulator
{
    /// <summary>
    /// 统一的混合试卷数据结构
    /// </summary>
    public class MixedExamData
    {
        public string ExamName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string TeacherId { get; set; } = "";
        public DateTime CreationTime { get; set; } = DateTime.Now;
        public ExamType ExamType { get; set; } = ExamType.Mixed;

        // 🚀 统一的题目内容存储
        public ExamContent Content { get; set; } = new();

        // 🚀 新增：试卷分数配置
        public ExamScoreConfig ScoreConfig { get; set; } = new();

        // 🔧 为了向后兼容，保留这些属性但标记为过时
        [Obsolete("使用 Content.CircuitQuestions 替代")]
        public List<CircuitQuestion> CircuitQuestions
        {
            get => Content.CircuitQuestions;
            set => Content.CircuitQuestions = value;
        }

        [Obsolete("使用 Content.TheoryQuestions 替代")]
        public List<TheoryQuestion> TheoryQuestions
        {
            get => Content.TheoryQuestions;
            set => Content.TheoryQuestions = value;
        }

        // 获取总题目数
        public int TotalQuestions => Content.TotalQuestions;

        // 获取统计信息
        /// <summary>
        /// 🔧 修改：获取统计信息 - 区分被勾选和未勾选的电路题目
        /// </summary>
        public string GetStatistics()
        {
            var selectedCircuitCount = Content.CircuitQuestions.Count(q => q.IsChecked);
            var totalCircuitCount = Content.CircuitQuestions.Count;

            string circuitInfo;
            if (totalCircuitCount == selectedCircuitCount)
            {
                circuitInfo = $"{selectedCircuitCount}题";
            }
            else
            {
                circuitInfo = $"{selectedCircuitCount}题已选中 (共{totalCircuitCount}题)";
            }

            return $"电路实测：{circuitInfo}，" +
                   $"理论题目：{Content.TheoryQuestions.Count}题，" +
                   $"飞控题目：{Content.FCQuestions.Count}题，" +
                   $"考试总计：{selectedCircuitCount + Content.TheoryQuestions.Count + Content.FCQuestions.Count}题";
        }

        /// <summary>
        /// 🔧 新增：获取详细的统计信息
        /// </summary>
        public string GetDetailedStatistics()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📋 试卷统计详情：");

            if (Content.TheoryQuestions.Any())
            {
                sb.AppendLine($"  📚 理论题目：{Content.TheoryQuestions.Count} 题");
            }

            if (Content.CircuitQuestions.Any())
            {
                var selectedCount = Content.CircuitQuestions.Count(q => q.IsChecked);
                var totalCount = Content.CircuitQuestions.Count;
                sb.AppendLine($"  🔧 电路实测：{selectedCount} 题已选中 (共 {totalCount} 题)");

                if (totalCount > selectedCount)
                {
                    sb.AppendLine($"     💡 未勾选：{totalCount - selectedCount} 题 (用于误修复检测)");
                }
            }

            if (Content.FCQuestions.Any())
            {
                sb.AppendLine($"  🛩️ 飞控实操：{Content.FCQuestions.Count} 题");
            }

            sb.AppendLine($"  📊 考试题目总数：{ExamQuestionCount} 题");
            sb.AppendLine($"  💾 文件保存总数：{TotalQuestions} 题");

            return sb.ToString();
        }

        /// <summary>
        /// 🔧 新增：获取考试题目总数（只计算实际参与考试的题目）
        /// </summary>
        public int ExamQuestionCount =>
            Content.TheoryQuestions.Count +
            Content.CircuitQuestions.Count(q => q.IsChecked) +
            Content.FCQuestions.Count;

        // 🚀 新增：获取分数配置统计信息
        public string GetScoreStatistics()
        {
            return $"总分：{ScoreConfig.TotalScore}分，" +
                   $"理论题：{ScoreConfig.TheoryPercentage}%({ScoreConfig.TheoryScore}分)，" +
                   $"电路题：{ScoreConfig.CircuitPercentage}%({ScoreConfig.CircuitScore}分)，" +
                   $"飞控题：{ScoreConfig.FCPercentage}%({ScoreConfig.FCScore}分)";
        }
    }

    /// <summary>
    /// 试卷内容模型（统一存储所有类型题目）
    /// </summary>
    public class ExamContent
    {
        public List<TheoryQuestion> TheoryQuestions { get; set; } = new();
        public List<CircuitQuestion> CircuitQuestions { get; set; } = new();
        public List<FCQuestion> FCQuestions { get; set; } = new();

        public bool HasAnyQuestions =>
            TheoryQuestions.Any() || CircuitQuestions.Any() || FCQuestions.Any();

        public int TotalQuestions =>
            TheoryQuestions.Count + CircuitQuestions.Count + FCQuestions.Count;

        // 🚀 修复：获取所有题目的统一列表（包含未勾选的题目）
        public List<Question> GetAllQuestionsAsGeneric()
        {
            var allQuestions = new List<Question>();

            // 🔧 关键修复：转换所有电路题目（包含勾选和未勾选的）
            allQuestions.AddRange(CircuitQuestions.Select(cq => new Question
            {
                Name = cq.Name,
                Content = cq.Content,
                IsChecked = cq.IsChecked, // 🚀 保持原有的勾选状态
                CommandString = cq.CommandString
            }));

            // 🚀 转换理论题目（映射为通用Question格式）
            allQuestions.AddRange(TheoryQuestions.Select(tq => new Question
            {
                Name = tq.Id,
                Content = tq.QuestionStatement,
                IsChecked = false, // 理论题目在考试中不需要IsChecked
                CommandString = "" // 理论题目没有串口指令
            }));

            // 🚀 转换飞控题目
            allQuestions.AddRange(FCQuestions.Select(fcq => new Question
            {
                Name = fcq.Id,
                Content = fcq.QuestionStatement,
                IsChecked = false, // 飞控题目在考试中不需要IsChecked
                CommandString = ""  // 飞控题目没有串口指令
            }));

            return allQuestions;
        }
    }

    /// <summary>
    /// 🚀 新增：试卷分数配置模型
    /// </summary>
    public class ExamScoreConfig
    {
        /// <summary>
        /// 试卷总分
        /// </summary>
        public int TotalScore { get; set; } = 100;

        /// <summary>
        /// 理论题目分数占比（百分比）
        /// </summary>
        public double TheoryPercentage { get; set; } = 40.0;

        /// <summary>
        /// 电路检测题目分数占比（百分比）
        /// </summary>
        public double CircuitPercentage { get; set; } = 40.0;

        /// <summary>
        /// 飞控实操题目分数占比（百分比）
        /// </summary>
        public double FCPercentage { get; set; } = 20.0;

        /// <summary>
        /// 理论题目分数（根据占比计算）
        /// </summary>
        [JsonIgnore]
        public int TheoryScore => (int)Math.Round(TotalScore * TheoryPercentage / 100.0);

        /// <summary>
        /// 电路检测题目分数（根据占比计算）
        /// </summary>
        [JsonIgnore]
        public int CircuitScore => (int)Math.Round(TotalScore * CircuitPercentage / 100.0);

        /// <summary>
        /// 飞控实操题目分数（根据占比计算）
        /// </summary>
        [JsonIgnore]
        public int FCScore => (int)Math.Round(TotalScore * FCPercentage / 100.0);

        /// <summary>
        /// 验证分数配置是否合法
        /// </summary>
        [JsonIgnore]
        public bool IsValid => Math.Abs(TheoryPercentage + CircuitPercentage + FCPercentage - 100.0) < 0.01;

        /// <summary>
        /// 获取配置说明文本
        /// </summary>
        [JsonIgnore]
        public string ConfigurationText =>
            $"总分{TotalScore}分：理论{TheoryPercentage}%({TheoryScore}分)，电路{CircuitPercentage}%({CircuitScore}分)，飞控{FCPercentage}%({FCScore}分)";

        /// <summary>
        /// 自动调整占比，确保总和为100%
        /// </summary>
        public void AutoAdjustPercentages()
        {
            var total = TheoryPercentage + CircuitPercentage + FCPercentage;
            if (Math.Abs(total - 100.0) > 0.01)
            {
                var factor = 100.0 / total;
                TheoryPercentage = Math.Round(TheoryPercentage * factor, 1);
                CircuitPercentage = Math.Round(CircuitPercentage * factor, 1);
                FCPercentage = Math.Round(100.0 - TheoryPercentage - CircuitPercentage, 1);
            }
        }

        /// <summary>
        /// 根据题目数量智能推荐分数占比
        /// </summary>
        public void RecommendPercentages(int theoryCount, int circuitCount, int fcCount)
        {
            var totalCount = theoryCount + circuitCount + fcCount;
            if (totalCount == 0) return;

            TheoryPercentage = Math.Round((double)theoryCount / totalCount * 100, 1);
            CircuitPercentage = Math.Round((double)circuitCount / totalCount * 100, 1);
            FCPercentage = Math.Round(100.0 - TheoryPercentage - CircuitPercentage, 1);

            // 确保占比合理（最小值为5%）
            if (theoryCount > 0 && TheoryPercentage < 5.0) TheoryPercentage = 5.0;
            if (circuitCount > 0 && CircuitPercentage < 5.0) CircuitPercentage = 5.0;
            if (fcCount > 0 && FCPercentage < 5.0) FCPercentage = 5.0;

            AutoAdjustPercentages();
        }
    }

    public enum ExamType
    {
        Mixed,          // 混合试卷
        TheoryOnly,     // 纯理论
        CircuitOnly,    // 纯电路实测
        FCOnly,         // 纯飞控实操
        Comprehensive   // 综合试卷
    }

    public static class ExamFileManager
    {
        private const string EXAMS_DIRECTORY = "Exams";

        /// <summary>
        /// 保存试卷
        /// </summary>
        public static void SaveExam(MixedExamData examData)
        {
            try
            {
                if (!Directory.Exists(EXAMS_DIRECTORY))
                {
                    Directory.CreateDirectory(EXAMS_DIRECTORY);
                }

                // 保存混合试卷格式
                string mixedFileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}_mixed.json");
                string mixedJson = JsonSerializer.Serialize(examData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(mixedFileName, mixedJson);

                // 为了向后兼容，同时保存传统格式（仅电路题目）
                if (examData.Content.CircuitQuestions.Any())
                {
                    var compatibleExamData = new ExamData
                    {
                        ExamName = examData.ExamName,
                        TeacherName = examData.TeacherName,
                        TeacherId = examData.TeacherId,
                        CreationTime = examData.CreationTime,
                        Questions = examData.Content.GetAllQuestionsAsGeneric()
                    };

                    string fileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}.json");
                    string json = JsonSerializer.Serialize(compatibleExamData, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(fileName, json);
                }

                // 单独保存理论题目（如果有）
                if (examData.Content.TheoryQuestions.Any())
                {
                    string theoryFileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}_theory.json");
                    string theoryJson = JsonSerializer.Serialize(examData.Content.TheoryQuestions, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(theoryFileName, theoryJson);
                }

                // 单独保存飞控题目（如果有）
                if (examData.Content.FCQuestions.Any())
                {
                    string fcFileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}_fc.json");
                    string fcJson = JsonSerializer.Serialize(examData.Content.FCQuestions, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(fcFileName, fcJson);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"保存试卷失败：{ex.Message}");
            }
        }
    }    
}