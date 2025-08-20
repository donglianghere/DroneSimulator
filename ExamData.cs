using System;
using System.Collections.Generic;

namespace DroneSimulator
{
    public class ExamData
    {
        public string ExamName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string TeacherId { get; set; } = "";
        public DateTime CreationTime { get; set; }
        public List<Question> Questions { get; set; } = new();
        public List<StudentExamResult> StudentResults { get; set; } = new();
    }

    public class StudentExamResult
    {
        public string StudentName { get; set; } = "";
        public string StudentId { get; set; } = "";
        public double Score { get; set; }
        public DateTime SubmitTime { get; set; }
    }
}