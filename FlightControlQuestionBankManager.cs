using AutoPilot.Parameters;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DroneSimulator
{
    /// <summary>
    /// 飞控实操题库管理器
    /// </summary>
    public static class FlightControlQuestionBankManager
    {
        private const string FC_BANK_DIRECTORY = "FlightControlQuestionBank";
        private const string FC_BANK_FILE = "fc_questions.json";
        private const string BACKUP_DIRECTORY = "FlightControlQuestionBank/Backups";

        private static List<FlightControlQuestion> _questionCache = new();
        private static bool _cacheLoaded = false;
        private static readonly object _lockObject = new object();
        private static readonly HashSet<int> _assignedIds = new HashSet<int>();

        /// <summary>
        /// 获取下一个可用的题目ID
        /// </summary>
        public static string GetNextQuestionId()
        {
            lock (_lockObject)
            {
                if (!_cacheLoaded) LoadQuestions();

                var existingIds = new HashSet<int>();
                foreach (var question in _questionCache)
                {
                    if (int.TryParse(question.Id, out int numericId))
                    {
                        existingIds.Add(numericId);
                    }
                }

                existingIds.UnionWith(_assignedIds);

                int nextId = 1;
                while (existingIds.Contains(nextId) && nextId <= 99999)
                {
                    nextId++;
                }

                if (nextId > 99999)
                {
                    throw new InvalidOperationException("飞控题目数量已达到上限（99999）");
                }

                _assignedIds.Add(nextId);
                return nextId.ToString("D5");
            }
        }

        /// <summary>
        /// 获取所有飞控题目
        /// </summary>
        public static List<FlightControlQuestion> GetAllQuestions()
        {
            lock (_lockObject)
            {
                if (!_cacheLoaded) LoadQuestions();
                return new List<FlightControlQuestion>(_questionCache);
            }
        }

        /// <summary>
        /// 按条件获取题目
        /// </summary>
        public static List<FlightControlQuestion> GetQuestions(
            FCQuestionType? type = null,
            FCQuestionCategory? category = null,
            QuestionDifficulty? difficulty = null,
            bool? isActive = null,
            string? searchText = null)
        {
            var questions = GetAllQuestions();

            if (type.HasValue)
                questions = questions.Where(q => q.Type == type.Value).ToList();

            if (category.HasValue)
                questions = questions.Where(q => q.Category == category.Value).ToList();

            if (difficulty.HasValue)
                questions = questions.Where(q => q.Difficulty == difficulty.Value).ToList();

            if (isActive.HasValue)
                questions = questions.Where(q => q.IsActive == isActive.Value).ToList();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerSearchText = searchText.ToLower();
                questions = questions.Where(q =>
                    q.QuestionStatement.ToLower().Contains(lowerSearchText) ||
                    q.ParameterName.ToLower().Contains(lowerSearchText) ||
                    q.ParameterDescription.ToLower().Contains(lowerSearchText)
                ).ToList();
            }

            return questions;
        }

        /// <summary>
        /// 随机选择题目
        /// </summary>
        public static List<FlightControlQuestion> SelectRandomQuestions(int count,
            FCQuestionType? type = null,
            FCQuestionCategory? category = null,
            QuestionDifficulty? difficulty = null)
        {
            var questions = GetQuestions(type, category, difficulty, true);
            if (!questions.Any()) return new List<FlightControlQuestion>();

            var random = new Random();
            return questions.OrderBy(x => random.Next()).Take(count).ToList();
        }

        /// <summary>
        /// 保存题目
        /// </summary>
        public static bool SaveQuestion(FlightControlQuestion question)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_cacheLoaded) LoadQuestions();

                    if (!question.IsValid())
                    {
                        throw new ArgumentException("飞控题目数据不完整或不正确");
                    }

                    if (string.IsNullOrEmpty(question.Id))
                    {
                        question.Id = GetNextQuestionId();
                    }

                    question.LastModified = DateTime.Now;

                    var existingIndex = _questionCache.FindIndex(q => q.Id == question.Id);
                    if (existingIndex >= 0)
                    {
                        _questionCache[existingIndex] = question;
                    }
                    else
                    {
                        _questionCache.Add(question);
                    }

                    if (int.TryParse(question.Id, out int numericId))
                    {
                        _assignedIds.Remove(numericId);
                    }

                    return SaveQuestions();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"保存飞控题目失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 删除题目
        /// </summary>
        public static bool DeleteQuestion(string questionId)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_cacheLoaded) LoadQuestions();

                    var question = _questionCache.FirstOrDefault(q => q.Id == questionId);
                    if (question != null)
                    {
                        CreateBackup($"before_delete_{question.Id}");
                        _questionCache.Remove(question);
                        return SaveQuestions();
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"删除飞控题目失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 批量删除题目
        /// </summary>
        public static FCBatchDeleteResult BatchDeleteQuestions(List<string> questionIds)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_cacheLoaded) LoadQuestions();

                    var result = new FCBatchDeleteResult();
                    var deletedQuestions = new List<FlightControlQuestion>();

                    foreach (var questionId in questionIds)
                    {
                        var question = _questionCache.FirstOrDefault(q => q.Id == questionId);
                        if (question != null)
                        {
                            deletedQuestions.Add(question);
                            result.SuccessCount++;
                        }
                        else
                        {
                            result.FailCount++;
                            result.Errors.Add($"未找到ID为 {questionId} 的题目");
                        }
                    }

                    if (deletedQuestions.Any())
                    {
                        CreateBackup($"before_batch_delete_{deletedQuestions.Count}_questions");

                        foreach (var question in deletedQuestions)
                        {
                            _questionCache.Remove(question);
                        }

                        if (SaveQuestions())
                        {
                            result.Success = true;
                            result.Message = $"成功删除 {result.SuccessCount} 道题目";
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = "保存删除结果时失败";
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "没有找到要删除的题目";
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"批量删除题目失败: {ex.Message}");
                    return new FCBatchDeleteResult
                    {
                        Success = false,
                        Message = $"批量删除失败：{ex.Message}",
                        FailCount = questionIds.Count
                    };
                }
            }
        }

        /// <summary>
        /// 检测重复题目
        /// </summary>
        public static FCDuplicateDetectionResult DetectDuplicateQuestions()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_cacheLoaded) LoadQuestions();

                    var result = new FCDuplicateDetectionResult();
                    var duplicateGroups = new List<List<FlightControlQuestion>>();

                    var groupedByStatement = _questionCache
                        .Where(q => !string.IsNullOrWhiteSpace(q.QuestionStatement))
                        .GroupBy(q => q.QuestionStatement.Trim(), StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() > 1)
                        .ToList();

                    foreach (var group in groupedByStatement)
                    {
                        var duplicateList = group.OrderBy(q => q.CreatedTime).ToList();
                        duplicateGroups.Add(duplicateList);

                        result.DuplicateGroups.Add(new FCDuplicateGroup
                        {
                            QuestionStatement = group.Key,
                            DuplicateQuestions = duplicateList,
                            Count = duplicateList.Count
                        });
                    }

                    result.TotalDuplicateGroups = duplicateGroups.Count;
                    result.TotalDuplicateQuestions = duplicateGroups.Sum(g => g.Count);
                    result.QuestionsToKeep = duplicateGroups.Count;
                    result.QuestionsToDelete = result.TotalDuplicateQuestions - result.QuestionsToKeep;

                    result.Success = true;
                    result.Message = result.TotalDuplicateGroups > 0
                        ? $"检测到 {result.TotalDuplicateGroups} 组重复题目，共 {result.TotalDuplicateQuestions} 道题目"
                        : "未发现重复题目";

                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"检测重复题目失败: {ex.Message}");
                    return new FCDuplicateDetectionResult
                    {
                        Success = false,
                        Message = $"检测重复题目失败：{ex.Message}"
                    };
                }
            }
        }

        /// <summary>
        /// 删除重复题目
        /// </summary>
        public static FCBatchDeleteResult DeleteDuplicateQuestions(List<FCDuplicateGroup> duplicateGroups)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_cacheLoaded) LoadQuestions();

                    var questionsToDelete = new List<string>();

                    foreach (var group in duplicateGroups)
                    {
                        var sortedQuestions = group.DuplicateQuestions
                            .OrderBy(q => q.CreatedTime)
                            .ThenBy(q => q.Id)
                            .ToList();

                        for (int i = 1; i < sortedQuestions.Count; i++)
                        {
                            questionsToDelete.Add(sortedQuestions[i].Id);
                        }
                    }

                    if (questionsToDelete.Any())
                    {
                        CreateBackup($"before_delete_duplicates_{questionsToDelete.Count}_questions");
                        return BatchDeleteQuestions(questionsToDelete);
                    }
                    else
                    {
                        return new FCBatchDeleteResult
                        {
                            Success = true,
                            Message = "没有找到需要删除的重复题目",
                            SuccessCount = 0,
                            FailCount = 0
                        };
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"删除重复题目失败: {ex.Message}");
                    return new FCBatchDeleteResult
                    {
                        Success = false,
                        Message = $"删除重复题目失败：{ex.Message}",
                        FailCount = 0
                    };
                }
            }
        }

        /// <summary>
        /// 与飞控通信验证参数
        /// </summary>
        public static async Task<FCAnswerResult> ValidateAnswerWithFlightController(
            FlightControlQuestion question,
            string studentAnswer,
            ParameterService parameterService)
        {
            var result = new FCAnswerResult
            {
                QuestionId = question.Id,
                StudentAnswer = studentAnswer,
                IsCorrect = false
            };

            try
            {
                if (!parameterService.IsConnected)
                {
                    result.ErrorMessage = "飞控未连接";
                    return result;
                }

                if (question.RequireFlightControllerRead)
                {
                    var parameter = parameterService.Parameters.FirstOrDefault(p =>
                        p.Name.Equals(question.ParameterName, StringComparison.OrdinalIgnoreCase));

                    if (parameter == null)
                    {
                        result.ErrorMessage = $"飞控中未找到参数 {question.ParameterName}";
                        return result;
                    }

                    result.ActualValue = parameter.Value.ToString();
                    result.IsCorrect = question.ValidateAnswer(result.ActualValue);
                }
                else
                {
                    result.ActualValue = studentAnswer;
                    result.IsCorrect = question.ValidateAnswer(studentAnswer);
                }

                result.CorrectValue = question.CorrectValue;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"验证过程出错: {ex.Message}";
            }

            return result;
        }

        #region 私有方法
        private static void LoadQuestions()
        {
            try
            {
                if (!Directory.Exists(FC_BANK_DIRECTORY))
                {
                    Directory.CreateDirectory(FC_BANK_DIRECTORY);
                }

                string filePath = Path.Combine(FC_BANK_DIRECTORY, FC_BANK_FILE);

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true
                    };

                    var questions = JsonSerializer.Deserialize<List<FlightControlQuestion>>(json, options);
                    _questionCache = questions ?? new List<FlightControlQuestion>();
                }
                else
                {
                    _questionCache = CreateSampleFCQuestions();
                    SaveQuestions();
                }

                _cacheLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载飞控题库失败: {ex.Message}");
                _questionCache = new List<FlightControlQuestion>();
                _cacheLoaded = true;
            }
        }

        private static bool SaveQuestions()
        {
            try
            {
                if (!Directory.Exists(FC_BANK_DIRECTORY))
                {
                    Directory.CreateDirectory(FC_BANK_DIRECTORY);
                }

                string filePath = Path.Combine(FC_BANK_DIRECTORY, FC_BANK_FILE);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(_questionCache, options);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存飞控题库失败: {ex.Message}");
                return false;
            }
        }

        private static bool CreateBackup(string backupName = "")
        {
            try
            {
                if (!Directory.Exists(BACKUP_DIRECTORY))
                {
                    Directory.CreateDirectory(BACKUP_DIRECTORY);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = string.IsNullOrEmpty(backupName)
                    ? $"fc_backup_{timestamp}.json"
                    : $"fc_backup_{backupName}_{timestamp}.json";

                string backupPath = Path.Combine(BACKUP_DIRECTORY, fileName);
                string sourcePath = Path.Combine(FC_BANK_DIRECTORY, FC_BANK_FILE);

                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, backupPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建飞控题库备份失败: {ex.Message}");
            }
            return false;
        }

        private static List<FlightControlQuestion> CreateSampleFCQuestions()
        {
            return new List<FlightControlQuestion>
            {
                new FlightControlQuestion
                {
                    Id = "00001",
                    QuestionStatement = "将俯仰角速度控制器P增益参数MC_PITCHRATE_P设置为0.15",
                    Type = FCQuestionType.ParameterSetting,
                    Category = FCQuestionCategory.PIDTuning,
                    Difficulty = QuestionDifficulty.Medium,
                    Points = 5,
                    ParameterName = "MC_PITCHRATE_P",
                    ParameterDescription = "俯仰角速度控制器P增益",
                    DataType = ParameterDataType.Float,
                    CorrectValue = "0.15",
                    Tolerance = 0.001,
                    MinValue = "0.0",
                    MaxValue = "1.0",
                    Unit = "",
                    RequireFlightControllerRead = true,
                    VerifyMethod = ParameterVerifyMethod.FloatTolerance,
                    Explanation = "俯仰角速度控制器P增益影响飞行器对俯仰角速度控制的响应速度",
                    CreatedBy = "系统",
                    CreatedTime = DateTime.Now
                },
                new FlightControlQuestion
                {
                    Id = "00002",
                    QuestionStatement = "验证横滚角速度控制器I增益参数MC_ROLLRATE_I的值是否设置为0.05",
                    Type = FCQuestionType.ParameterVerify,
                    Category = FCQuestionCategory.PIDTuning,
                    Difficulty = QuestionDifficulty.Easy,
                    Points = 3,
                    ParameterName = "MC_ROLLRATE_I",
                    ParameterDescription = "横滚角速度控制器I增益",
                    DataType = ParameterDataType.Float,
                    CorrectValue = "0.05",
                    Tolerance = 0.001,
                    Unit = "",
                    RequireFlightControllerRead = true,
                    VerifyMethod = ParameterVerifyMethod.FloatTolerance,
                    Explanation = "横滚角速度控制器I增益用于消除横滚角速度控制的稳态误差",
                    CreatedBy = "系统",
                    CreatedTime = DateTime.Now
                }
            };
        }
        #endregion
    }

    #region 辅助类定义（重命名避免冲突）

    /// <summary>
    /// 飞控题库批量删除结果
    /// </summary>
    public class FCBatchDeleteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// 飞控题库重复题目检测结果
    /// </summary>
    public class FCDuplicateDetectionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int TotalDuplicateGroups { get; set; }
        public int TotalDuplicateQuestions { get; set; }
        public int QuestionsToKeep { get; set; }
        public int QuestionsToDelete { get; set; }
        public List<FCDuplicateGroup> DuplicateGroups { get; set; } = new();
    }

    /// <summary>
    /// 飞控题目重复分组
    /// </summary>
    public class FCDuplicateGroup
    {
        public string QuestionStatement { get; set; } = "";
        public List<FlightControlQuestion> DuplicateQuestions { get; set; } = new();
        public int Count { get; set; }
    }

    /// <summary>
    /// 飞控答案结果
    /// </summary>
    public class FCAnswerResult
    {
        public string QuestionId { get; set; } = "";
        public string StudentAnswer { get; set; } = "";
        public string ActualValue { get; set; } = "";
        public string CorrectValue { get; set; } = "";
        public bool IsCorrect { get; set; }
        public string ErrorMessage { get; set; } = "";
        public DateTime AnswerTime { get; set; } = DateTime.Now;
    }

    #endregion
}