using System.Windows.Controls;

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
}