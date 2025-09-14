using System.Text.Json.Serialization;

namespace DroneSimulator
{
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
        public List<Question> CircuitQuestions { get; set; } = new();
        
        // 理论题目
        public List<TheoryQuestion> TheoryQuestions { get; set; } = new();
        
        // 获取总题目数
        public int TotalQuestions => CircuitQuestions.Count + TheoryQuestions.Count;
        
        // 获取统计信息
        public string GetStatistics()
        {
            return $"电路实测：{CircuitQuestions.Count}题，理论题目：{TheoryQuestions.Count}题，总计：{TotalQuestions}题";
        }
    }
}