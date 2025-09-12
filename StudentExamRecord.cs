using System;
using System.Collections.Generic;

namespace DroneSimulator
{
    /// <summary>
    /// 学生考试详细记录
    /// </summary>
    public class DetailedExamRecord
    {
        /// <summary>
        /// 考试序号（自动递增）
        /// </summary>
        public int ExamSequence { get; set; }

        /// <summary>
        /// 考生姓名
        /// </summary>
        public string StudentName { get; set; } = "";

        /// <summary>
        /// 身份证号
        /// </summary>
        public string StudentId { get; set; } = "";

        /// <summary>
        /// 试题信息
        /// </summary>
        public ExamInfo ExamInfo { get; set; } = new();

        /// <summary>
        /// 得分
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// 正确答题数量
        /// </summary>
        public int CorrectAnswers { get; set; }

        /// <summary>
        /// 错误答题数量（误修复）
        /// </summary>
        public int WrongAnswers { get; set; }

        /// <summary>
        /// 答题花费时间
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// 答题开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 提交时间
        /// </summary>
        public DateTime SubmitTime { get; set; }

        /// <summary>
        /// 误修复的题目列表
        /// </summary>
        public List<string> WronglyRepairedQuestions { get; set; } = new();

        /// <summary>
        /// 未修复的题目列表（应该修复但未修复）
        /// </summary>
        public List<string> UnrepairedQuestions { get; set; } = new();

        /// <summary>
        /// 正确修复的题目列表
        /// </summary>
        public List<string> CorrectlyRepairedQuestions { get; set; } = new();

        /// <summary>
        /// 所有题目的状态记录
        /// </summary>
        public List<QuestionStatus> QuestionStatuses { get; set; } = new();
    }

    /// <summary>
    /// 考试基本信息
    /// </summary>
    public class ExamInfo
    {
        public string ExamName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string TeacherId { get; set; } = "";
        public DateTime CreationTime { get; set; }
        public int TotalQuestions { get; set; }
    }

    /// <summary>
    /// 题目状态
    /// </summary>
    public class QuestionStatus
    {
        /// <summary>
        /// 题目名称（CheckBox的Name）
        /// </summary>
        public string QuestionName { get; set; } = "";

        /// <summary>
        /// 题目内容
        /// </summary>
        public string QuestionContent { get; set; } = "";

        /// <summary>
        /// 是否应该修复（试卷中是否选中）
        /// </summary>
        public bool ShouldRepair { get; set; }

        /// <summary>
        /// 学生是否修复了
        /// </summary>
        public bool StudentRepaired { get; set; }

        /// <summary>
        /// 修复状态
        /// </summary>
        public RepairStatus Status { get; set; }

        /// <summary>
        /// 指令字符串
        /// </summary>
        public string CommandString { get; set; } = "";
    }

    /// <summary>
    /// 修复状态枚举
    /// </summary>
    public enum RepairStatus
    {
        /// <summary>
        /// 未操作
        /// </summary>
        NotTouched,

        /// <summary>
        /// 正确修复
        /// </summary>
        CorrectlyRepaired,

        /// <summary>
        /// 误修复（不该修复却修复了）
        /// </summary>
        WronglyRepaired,

        /// <summary>
        /// 未修复（应该修复但没修复）
        /// </summary>
        Unrepaired
    }
}