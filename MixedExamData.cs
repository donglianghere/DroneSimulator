using System.IO;
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
        public string GetStatistics()
        {
            return $"电路实测：{Content.CircuitQuestions.Count}题，" +
                   $"理论题目：{Content.TheoryQuestions.Count}题，" +
                   $"飞控题目：{Content.FCQuestions.Count}题，" +
                   $"总计：{TotalQuestions}题";
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

        // 🚀 新增：获取所有题目的统一列表（用于MainWindow兼容）
        public List<Question> GetAllQuestionsAsGeneric()
        {
            var allQuestions = new List<Question>();

            // 转换电路题目
            allQuestions.AddRange(CircuitQuestions.Select(cq => new Question
            {
                Name = cq.Name,
                Content = cq.Content,
                IsChecked = cq.IsChecked,
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

        public static void SaveExam(MixedExamData examData)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(EXAMS_DIRECTORY))
                    Directory.CreateDirectory(EXAMS_DIRECTORY);

                // 🚀 修正：保存完整的混合试卷数据
                var compatibleExamData = new ExamData
                {
                    ExamName = examData.ExamName,
                    TeacherName = examData.TeacherName,
                    TeacherId = examData.TeacherId,
                    CreationTime = examData.CreationTime,
                    // 🚀 关键修复：保存所有类型的题目
                    Questions = examData.Content.GetAllQuestionsAsGeneric()
                };

                // 保存主试卷文件（向后兼容格式）
                string fileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}.json");
                string jsonString = JsonSerializer.Serialize(compatibleExamData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(fileName, jsonString);

                // 🚀 同时保存完整的混合试卷数据（新格式）
                string mixedFileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}_mixed.json");
                string mixedJsonString = JsonSerializer.Serialize(examData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(mixedFileName, mixedJsonString);

                Console.WriteLine($"✅ 混合试卷保存成功：{examData.GetStatistics()}");
            }
            catch (Exception ex)
            {
                throw new Exception($"保存混合试卷失败：{ex.Message}");
            }
        }

        // 🚀 新增：加载混合试卷的方法
        public static MixedExamData? LoadMixedExam(string examName)
        {
            try
            {
                // 优先加载新格式的混合试卷
                string mixedFileName = Path.Combine(EXAMS_DIRECTORY, $"{examName}_mixed.json");
                if (File.Exists(mixedFileName))
                {
                    string mixedJson = File.ReadAllText(mixedFileName);
                    return JsonSerializer.Deserialize<MixedExamData>(mixedJson);
                }

                // 如果没有新格式，尝试从旧格式转换
                string fileName = Path.Combine(EXAMS_DIRECTORY, $"{examName}.json");
                if (File.Exists(fileName))
                {
                    string json = File.ReadAllText(fileName);
                    var examData = JsonSerializer.Deserialize<ExamData>(json);

                    if (examData != null)
                    {
                        // 转换为混合试卷格式
                        return new MixedExamData
                        {
                            ExamName = examData.ExamName,
                            TeacherName = examData.TeacherName,
                            TeacherId = examData.TeacherId,
                            CreationTime = examData.CreationTime,
                            Content = new ExamContent
                            {
                                // 假设旧格式的题目都是电路题目
                                CircuitQuestions = examData.Questions.Select(q => new CircuitQuestion
                                {
                                    Name = q.Name,
                                    Content = q.Content,
                                    IsChecked = q.IsChecked,
                                    CommandString = q.CommandString
                                }).ToList()
                            }
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载混合试卷失败：{ex.Message}");
                return null;
            }
        }
    }
}