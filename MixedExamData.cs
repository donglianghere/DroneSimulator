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