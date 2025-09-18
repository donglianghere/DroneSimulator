using System.Windows.Controls;
using System.Xaml;

namespace DroneSimulator
{
    public class Question
    {
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
        public bool IsChecked { get; set; }
        public string CommandString { get; set; } = "";

        public Question() { }

        public Question(CheckBox checkBox)
        {
            Name = checkBox.Name ?? "";
            Content = checkBox.Content?.ToString() ?? "";
            IsChecked = checkBox.IsChecked == true;
            CommandString = CheckBoxCommandHelper.GetCommandString(checkBox);
        }

        public Question(string name, string content, bool isChecked, string commandString)
        {
            Name = name;
            Content = content;
            IsChecked = isChecked;
            CommandString = commandString;
        }
    }

    public class CircuitQuestion : Question
    {
        public CircuitQuestion() : base() { }
        public CircuitQuestion(CheckBox checkBox) : base(checkBox) { }
    }

    public class FCQuestion : Question
    {
        public FCQuestion() : base() { }
        public FCQuestion(CheckBox checkBox) : base(checkBox) { }

        // 飞控题目可能需要的额外属性
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public enum ExamType
    {
        Mixed,          // 混合试卷
        TheoryOnly,     // 纯理论
        CircuitOnly,    // 纯电路实测
        FCOnly,         // 纯飞控实操
        Comprehensive   // 综合试卷
    }

    
    // 题目提供者接口（只读）
    public interface ITheoryQuestionProvider
    {
        List<TheoryQuestion> GetAvailableQuestions();
        void RefreshQuestions();
    }

    public interface ICircuitQuestionProvider
    {
        List<CircuitQuestion> GetAvailableQuestions();
        void RefreshQuestions();
    }

    public interface IFCQuestionProvider
    {
        List<FCQuestion> GetAvailableQuestions();
        void RefreshQuestions();
    }
}