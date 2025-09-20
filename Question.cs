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
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
        public bool IsChecked { get; set; }
        public string CommandString { get; set; } = "";

        public CircuitQuestion(CheckBox checkBox) : base(checkBox)
        {
            Name = checkBox.Name ?? "";
            Content = checkBox.Content?.ToString() ?? "";
            IsChecked = checkBox.IsChecked == true;
            CommandString = CheckBoxCommandHelper.GetCommandString(checkBox) ?? "";
        }

        public CircuitQuestion() : base() { }
    }

    public class FCQuestion : Question
    {
        public FCQuestion() : base() { }
        public FCQuestion(CheckBox checkBox) : base(checkBox) { }
        public string Id { get; set; } = "";
        public string QuestionStatement { get; set; } = "";
        public string ParameterName { get; set; } = "";
        public string ParameterDescription { get; set; } = "";
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";

        public string Difficulty { get; set; } = "";
        public int Points { get; set; }
        public string CorrectValue { get; set; } = "";
        public string DataType { get; set; } = "";
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedTime { get; set; }
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