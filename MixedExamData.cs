using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DroneSimulator
{
    /// <summary>
    /// 试卷内容模型（只包含选中的题目）
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
    }

    /// <summary>
    /// 混合试卷数据（包含电路题和理论题）
    /// </summary>
    public class MixedExamData
    {
        public string ExamName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string TeacherId { get; set; } = "";
        public DateTime CreationTime { get; set; } = DateTime.Now;

        // 电路实测题目
        public List<CircuitQuestion> CircuitQuestions { get; set; } = new();

        // 理论题目
        public List<TheoryQuestion> TheoryQuestions { get; set; } = new();

        // 🔧 添加缺少的 Content 属性
        public ExamContent Content { get; set; } = new();

        // 获取总题目数
        public int TotalQuestions => CircuitQuestions.Count + TheoryQuestions.Count + (Content?.FCQuestions?.Count ?? 0);

        // 获取统计信息
        public string GetStatistics()
        {
            return $"电路实测：{CircuitQuestions.Count}题，理论题目：{TheoryQuestions.Count}题，飞控题目：{Content?.FCQuestions?.Count ?? 0}题，总计：{TotalQuestions}题";
        }
    }

    // 在合适的位置添加 ExamFileManager 类
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

                // 如果有飞控题目，单独保存
                if (examData.Content?.FCQuestions?.Any() == true)
                {
                    string fcFileName = Path.Combine(EXAMS_DIRECTORY, $"{examData.ExamName}_fc.json");
                    string fcJsonString = JsonSerializer.Serialize(examData.Content.FCQuestions, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    File.WriteAllText(fcFileName, fcJsonString);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"保存混合试卷失败：{ex.Message}");
            }
        }
    }
}