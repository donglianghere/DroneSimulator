using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DroneSimulator
{
    /// <summary>
    /// 错误状态到画刷转换器
    /// </summary>
    public class ErrorToBrushConverter : IValueConverter
    {
        public Brush ErrorBrush { get; set; } = Brushes.Red;
        public Brush NormalBrush { get; set; } = (Brush)new BrushConverter().ConvertFrom("#5EC6FF")!;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? NormalBrush : ErrorBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }    

    /// <summary>
    /// 🚀 新增：布尔值到可见性转换器
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter?.ToString()?.ToLower() == "invert";
                return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }

    /// <summary>
    /// 🚀 新增：空值到可见性转换器
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString()?.ToLower() == "invert";
            bool isNull = value == null || (value is string str && string.IsNullOrEmpty(str));

            return (isNull ^ invert) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 题目编号转换器 - 将题目ID转换为序号
    /// </summary>
    public class QuestionNumberConverter : IValueConverter
    {
        private static int _questionCounter = 0;
        private static Dictionary<string, int> _questionNumbers = new Dictionary<string, int>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "1";

            string questionId = value.ToString();

            if (!_questionNumbers.ContainsKey(questionId))
            {
                _questionNumbers[questionId] = ++_questionCounter;
            }

            return _questionNumbers[questionId].ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        // 重置计数器的静态方法
        public static void Reset()
        {
            _questionCounter = 0;
            _questionNumbers.Clear();
        }
    }

    /// <summary>
    /// 飞控题目类型到可见性转换器
    /// </summary>
    public class FCQuestionTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;

            string typeString = value.ToString();

            // 如果是参数设置类型的题目，显示输入框
            if (typeString.Contains("ParameterSetting") || typeString.Contains("Setting"))
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 字符串到可见性转换器
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TheoryQuestionVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2) return Visibility.Collapsed;

            if (values[0] is bool isChecked && values[1] is TheoryQuestionType questionType)
            {
                string expectedType = parameter?.ToString() ?? "";

                if (isChecked &&
                    ((expectedType == "SingleChoice" && questionType == TheoryQuestionType.SingleChoice) ||
                     (expectedType == "MultipleChoice" && questionType == TheoryQuestionType.MultipleChoice)))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}