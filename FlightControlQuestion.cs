using System.IO;
using System.Text;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DroneSimulator
{
    /// <summary>
    /// 飞控实操题目类型
    /// </summary>
    public enum FCQuestionType
    {
        ParameterSetting,    // 参数设置类型
        ParameterVerify,     // 参数验证类型
        ParameterCalculation // 参数计算类型
    }

    /// <summary>
    /// 飞控实操题目分类
    /// </summary>
    public enum FCQuestionCategory
    {
        BasicParameters,     // 基础参数
        PIDTuning,          // PID调节
        SensorCalibration,  // 传感器校准
        FlightModes,        // 飞行模式
        SafetySettings,     // 安全设置
        AdvancedFeatures    // 高级功能
    }

    /// <summary>
    /// 参数数据类型
    /// </summary>
    public enum ParameterDataType
    {
        Float,   // 浮点数
        Integer, // 整数
        Boolean, // 布尔值
        String   // 字符串
    }

    /// <summary>
    /// 飞控实操题目
    /// </summary>
    public class FlightControlQuestion : INotifyPropertyChanged
    {
        #region 基础信息
        /// <summary>
        /// 题目ID
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// 题目陈述（如：将参数MC_PITCHRATE_P设置为0.15）
        /// </summary>
        public string QuestionStatement { get; set; } = "";

        /// <summary>
        /// 题目类型
        /// </summary>
        public FCQuestionType Type { get; set; } = FCQuestionType.ParameterSetting;

        /// <summary>
        /// 题目分类
        /// </summary>
        public FCQuestionCategory Category { get; set; } = FCQuestionCategory.BasicParameters;

        /// <summary>
        /// 难度等级
        /// </summary>
        public QuestionDifficulty Difficulty { get; set; } = QuestionDifficulty.Medium;

        /// <summary>
        /// 分值
        /// </summary>
        public int Points { get; set; } = 5;

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsActive { get; set; } = true;
        #endregion

        #region UI选择状态
        private bool _isSelected;

        /// <summary>
        /// 是否被选中（用于UI复选框）
        /// </summary>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region 参数相关信息
        /// <summary>
        /// 参数名称（如：MC_PITCHRATE_P）
        /// </summary>
        public string ParameterName { get; set; } = "";

        /// <summary>
        /// 参数描述
        /// </summary>
        public string ParameterDescription { get; set; } = "";

        /// <summary>
        /// 参数数据类型
        /// </summary>
        public ParameterDataType DataType { get; set; } = ParameterDataType.Float;

        /// <summary>
        /// 正确答案值（字符串形式存储）
        /// </summary>
        public string CorrectValue { get; set; } = "";

        /// <summary>
        /// 容差范围（用于浮点数比较）
        /// </summary>
        public double Tolerance { get; set; } = 0.001;

        /// <summary>
        /// 参数的有效范围（最小值）
        /// </summary>
        public string MinValue { get; set; } = "";

        /// <summary>
        /// 参数的有效范围（最大值）
        /// </summary>
        public string MaxValue { get; set; } = "";

        /// <summary>
        /// 参数单位
        /// </summary>
        public string Unit { get; set; } = "";
        #endregion

        #region 题目验证逻辑
        /// <summary>
        /// 是否需要从飞控读取参数验证（true：从飞控读取，false：直接输入验证）
        /// </summary>
        public bool RequireFlightControllerRead { get; set; } = true;

        /// <summary>
        /// 验证方法类型
        /// </summary>
        public ParameterVerifyMethod VerifyMethod { get; set; } = ParameterVerifyMethod.ExactMatch;
        #endregion

        #region 元数据
        /// <summary>
        /// 题目解释说明
        /// </summary>
        public string Explanation { get; set; } = "";

        /// <summary>
        /// 创建者
        /// </summary>
        public string CreatedBy { get; set; } = "";

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改者
        /// </summary>
        public string LastModifiedBy { get; set; } = "";

        /// <summary>
        /// 使用次数
        /// </summary>
        public int UsageCount { get; set; } = 0;

        /// <summary>
        /// 平均正确率
        /// </summary>
        public double AverageCorrectRate { get; set; } = 0.0;
        #endregion

        #region 显示属性
        [JsonIgnore]
        public string TypeDisplayName => Type switch
        {
            FCQuestionType.ParameterSetting => "参数设置",
            FCQuestionType.ParameterVerify => "参数验证",
            FCQuestionType.ParameterCalculation => "参数计算",
            _ => "未知类型"
        };

        [JsonIgnore]
        public string CategoryDisplayName => Category switch
        {
            FCQuestionCategory.BasicParameters => "基础参数",
            FCQuestionCategory.PIDTuning => "PID调节",
            FCQuestionCategory.SensorCalibration => "传感器校准",
            FCQuestionCategory.FlightModes => "飞行模式",
            FCQuestionCategory.SafetySettings => "安全设置",
            FCQuestionCategory.AdvancedFeatures => "高级功能",
            _ => "未知分类"
        };

        [JsonIgnore]
        public string DifficultyDisplayName => Difficulty switch
        {
            QuestionDifficulty.Easy => "简单",
            QuestionDifficulty.Medium => "中等",
            QuestionDifficulty.Hard => "困难",
            _ => "未知"
        };

        [JsonIgnore]
        public string ParameterInfo => $"{ParameterName} ({Unit})";

        [JsonIgnore]
        public string ValueRange => string.IsNullOrEmpty(MinValue) || string.IsNullOrEmpty(MaxValue) 
            ? "无限制" 
            : $"{MinValue} ~ {MaxValue}";
        #endregion

        #region 验证方法
        /// <summary>
        /// 验证题目数据是否完整
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(QuestionStatement) &&
                   !string.IsNullOrWhiteSpace(ParameterName) &&
                   !string.IsNullOrWhiteSpace(CorrectValue) &&
                   Points > 0;
        }

        /// <summary>
        /// 验证答案是否正确
        /// </summary>
        public bool ValidateAnswer(string studentAnswer)
        {
            if (string.IsNullOrWhiteSpace(studentAnswer))
                return false;

            return VerifyMethod switch
            {
                ParameterVerifyMethod.ExactMatch => 
                    string.Equals(studentAnswer.Trim(), CorrectValue.Trim(), StringComparison.OrdinalIgnoreCase),
                
                ParameterVerifyMethod.NumericRange => ValidateNumericRange(studentAnswer),
                
                ParameterVerifyMethod.FloatTolerance => ValidateFloatTolerance(studentAnswer),
                
                _ => false
            };
        }

        private bool ValidateNumericRange(string value)
        {
            if (!double.TryParse(value, out double numValue) ||
                !double.TryParse(CorrectValue, out double correctNum))
                return false;

            if (!string.IsNullOrEmpty(MinValue) && double.TryParse(MinValue, out double min))
                if (numValue < min) return false;

            if (!string.IsNullOrEmpty(MaxValue) && double.TryParse(MaxValue, out double max))
                if (numValue > max) return false;

            return true;
        }

        private bool ValidateFloatTolerance(string value)
        {
            if (!double.TryParse(value, out double numValue) ||
                !double.TryParse(CorrectValue, out double correctNum))
                return false;

            return Math.Abs(numValue - correctNum) <= Tolerance;
        }
        #endregion

        #region INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    /// <summary>
    /// 参数验证方法
    /// </summary>
    public enum ParameterVerifyMethod
    {
        ExactMatch,      // 精确匹配
        NumericRange,    // 数值范围
        FloatTolerance   // 浮点容差
    }
}