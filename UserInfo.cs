using DroneSimulator;
namespace DroneSimulator
{
    public enum UserType { Student, Teacher, Admin }

    public class UserInfo
    {
        public required string Name { get; set; }
        public required string IdNumber { get; set; }
        public required string Password { get; set; }
        public required UserType Type { get; set; }
    }

    /// <summary>
    /// 学生类，继承自UserInfo，记录每次考试的详细信息
    /// </summary>
    public class Student : UserInfo
    {
        /// <summary>
        /// 学生所有考试记录
        /// </summary>
        public List<StudentExamRecord> ExamRecords { get; set; } = new();
    }

    /// <summary>
    /// 单次考试记录
    /// </summary>
    public class StudentExamRecord
    {
        /// <summary>
        /// 原始试卷（出题时的题目列表）
        /// </summary>
        public List<Question> OriginalQuestions { get; set; } = new();

        /// <summary>
        /// 学生作答后的试卷（含作答内容/选择）
        /// </summary>
        public List<Question> AnsweredQuestions { get; set; } = new();

        /// <summary>
        /// 考试时间
        /// </summary>
        public DateTime ExamTime { get; set; }

        /// <summary>
        /// 得分
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// 关联的试卷名（可选）
        /// </summary>
        public string ExamName { get; set; } = "";
    }
}