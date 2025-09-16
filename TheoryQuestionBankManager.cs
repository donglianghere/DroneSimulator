using System.Text.Json;
using System.Text;
using System.IO;

namespace DroneSimulator
{
    /// <summary>
    /// 理论题库管理器
    /// </summary>
    public static class TheoryQuestionBankManager
    {
        private const string THEORY_BANK_DIRECTORY = "TheoryQuestionBank";
        private const string THEORY_BANK_FILE = "theory_questions.json";
        private const string THEORY_EXAMS_DIRECTORY = "TheoryExams";
        private const string BACKUP_DIRECTORY = "TheoryQuestionBank/Backups";
        
        private static List<TheoryQuestion> _questionCache = new();
        private static bool _cacheLoaded = false;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 获取所有理论题目
        /// </summary>
        public static List<TheoryQuestion> GetAllQuestions()
        {
            lock (_lockObject)
            {
                if (!_cacheLoaded)
                {
                    LoadQuestions();
                }
                return new List<TheoryQuestion>(_questionCache);
            }
        }

        /// <summary>
        /// 按条件获取题目
        /// </summary>
        public static List<TheoryQuestion> GetQuestions(
            TheoryQuestionType? type = null,
            TheoryQuestionCategory? category = null,
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
                    q.Options.Any(o => o.Text.ToLower().Contains(lowerSearchText))
                ).ToList();
            }

            return questions;
        }

        /// <summary>
        /// 随机选择题目
        /// </summary>
        public static List<TheoryQuestion> SelectRandomQuestions(int count, 
            TheoryQuestionType? type = null, 
            TheoryQuestionCategory? category = null, 
            QuestionDifficulty? difficulty = null)
        {
            var questions = GetQuestions(type, category, difficulty, true);
            
            if (!questions.Any()) return new List<TheoryQuestion>();
            
            var random = new Random();
            return questions.OrderBy(x => random.Next()).Take(count).ToList();
        }

        // 在 TheoryQuestionBankManager 类中添加以下方法

        /// <summary>
        /// 智能平衡选题
        /// </summary>
        public static List<TheoryQuestion> SelectBalancedQuestions(int totalCount)
        {
            var result = new List<TheoryQuestion>();
            var random = new Random();

            // 定义各分类题目的比例
            var categoryDistribution = new Dictionary<TheoryQuestionCategory, double>
    {
        { TheoryQuestionCategory.FlightPrinciples, 0.25 },   // 25% 飞行原理
        { TheoryQuestionCategory.Structure, 0.20 },          // 20% 结构组成
        { TheoryQuestionCategory.ControlAlgorithm, 0.20 },   // 20% 控制算法
        { TheoryQuestionCategory.SensorFusion, 0.15 },       // 15% 传感器融合
        { TheoryQuestionCategory.FlightSafety, 0.15 },       // 15% 飞行安全
        { TheoryQuestionCategory.LawsRegulations, 0.05 }     // 5% 法律法规
    };

            foreach (var (category, ratio) in categoryDistribution)
            {
                int count = Math.Max(1, (int)Math.Round(totalCount * ratio));
                var questionsOfCategory = SelectRandomQuestions(count, category: category);
                result.AddRange(questionsOfCategory);
            }

            // 如果总数不足，补充随机题目
            while (result.Count < totalCount)
            {
                var allQuestions = GetAllQuestions().Where(q => q.IsActive && !result.Contains(q)).ToList();
                if (allQuestions.Any())
                {
                    var randomQuestion = allQuestions[random.Next(allQuestions.Count)];
                    result.Add(randomQuestion);
                }
                else
                {
                    break;
                }
            }

            return result.Take(totalCount).OrderBy(x => random.Next()).ToList();
        }

        /// <summary>
        /// 保存题目
        /// </summary>
        public static bool SaveQuestion(TheoryQuestion question)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_cacheLoaded) LoadQuestions();

                    if (!question.IsValid())
                    {
                        throw new ArgumentException("题目数据不完整或不正确");
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

                    return SaveQuestions();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"保存理论题目失败: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"删除理论题目失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 批量删除题目
        /// </summary>
        /// <param name="questionIds">要删除的题目ID列表</param>
        /// <returns>删除结果，包含成功和失败的数量</returns>
        public static BatchDeleteResult BatchDeleteQuestions(List<string> questionIds)
        {
            lock (_lockObject)
            {
                try
                {
                    if (!_cacheLoaded) LoadQuestions();

                    var result = new BatchDeleteResult();
                    var deletedQuestions = new List<TheoryQuestion>();

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
                        // 创建备份
                        CreateBackup($"before_batch_delete_{deletedQuestions.Count}_questions");

                        // 从缓存中移除
                        foreach (var question in deletedQuestions)
                        {
                            _questionCache.Remove(question);
                        }

                        // 保存到文件
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
                    return new BatchDeleteResult
                    {
                        Success = false,
                        Message = $"批量删除失败：{ex.Message}",
                        FailCount = questionIds.Count
                    };
                }
            }
        }

        /// <summary>
        /// 批量删除结果
        /// </summary>
        public class BatchDeleteResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public List<string> Errors { get; set; } = new();
        }

        /// <summary>
        /// 根据ID获取题目
        /// </summary>
        public static TheoryQuestion? GetQuestionById(string questionId)
        {
            var questions = GetAllQuestions();
            return questions.FirstOrDefault(q => q.Id == questionId);
        }

        private static void LoadQuestions()
        {
            try
            {
                if (!Directory.Exists(THEORY_BANK_DIRECTORY))
                {
                    Directory.CreateDirectory(THEORY_BANK_DIRECTORY);
                }

                string filePath = Path.Combine(THEORY_BANK_DIRECTORY, THEORY_BANK_FILE);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    var questions = JsonSerializer.Deserialize<List<TheoryQuestion>>(json) ?? new List<TheoryQuestion>();
                    _questionCache = questions;
                }
                else
                {
                    // 首次运行，创建示例题目
                    _questionCache = CreateSampleTheoryQuestions();
                    SaveQuestions();
                }

                _cacheLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载理论题库失败: {ex.Message}");
                _questionCache = new List<TheoryQuestion>();
                _cacheLoaded = true;
            }
        }

        private static bool SaveQuestions()
        {
            try
            {
                if (!Directory.Exists(THEORY_BANK_DIRECTORY))
                {
                    Directory.CreateDirectory(THEORY_BANK_DIRECTORY);
                }

                string filePath = Path.Combine(THEORY_BANK_DIRECTORY, THEORY_BANK_FILE);
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
                System.Diagnostics.Debug.WriteLine($"保存理论题库失败: {ex.Message}");
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
                    ? $"backup_{timestamp}.json" 
                    : $"backup_{backupName}_{timestamp}.json";
                
                string backupPath = Path.Combine(BACKUP_DIRECTORY, fileName);
                string sourcePath = Path.Combine(THEORY_BANK_DIRECTORY, THEORY_BANK_FILE);

                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, backupPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建理论题库备份失败: {ex.Message}");
            }
            return false;
        }

        private static List<TheoryQuestion> CreateSampleTheoryQuestions()
        {
            var questions = new List<TheoryQuestion>();

            // 示例飞行原理题目
            questions.Add(new TheoryQuestion
            {
                QuestionStatement = "多旋翼无人机的升力主要来源于什么？",
                Type = TheoryQuestionType.SingleChoice,
                Category = TheoryQuestionCategory.FlightPrinciples,
                Difficulty = QuestionDifficulty.Easy,
                Points = 2,
                Options = new List<TheoryOption>
                {
                    new TheoryOption { Text = "机翼产生的升力", IsCorrect = false },
                    new TheoryOption { Text = "螺旋桨向下推动空气产生的反作用力", IsCorrect = true },
                    new TheoryOption { Text = "热气球原理", IsCorrect = false },
                    new TheoryOption { Text = "磁悬浮力", IsCorrect = false }
                },
                CorrectAnswers = new List<string> { "螺旋桨向下推动空气产生的反作用力" },
                Explanation = "多旋翼无人机通过螺旋桨旋转推动空气向下，根据牛顿第三定律产生向上的反作用力，这就是升力的来源。",
                CreatedBy = "系统",
                CreatedTime = DateTime.Now
            });

            // 示例多选题
            questions.Add(new TheoryQuestion
            {
                QuestionStatement = "影响多旋翼无人机飞行稳定性的主要因素包括哪些？",
                Type = TheoryQuestionType.MultipleChoice,
                Category = TheoryQuestionCategory.ControlAlgorithm,
                Difficulty = QuestionDifficulty.Medium,
                Points = 3,
                Options = new List<TheoryOption>
                {
                    new TheoryOption { Text = "重心位置", IsCorrect = true },
                    new TheoryOption { Text = "风力大小", IsCorrect = true },
                    new TheoryOption { Text = "电池电量", IsCorrect = false },
                    new TheoryOption { Text = "飞控算法", IsCorrect = true },
                    new TheoryOption { Text = "螺旋桨平衡", IsCorrect = true },
                    new TheoryOption { Text = "外壳颜色", IsCorrect = false }
                },
                CorrectAnswers = new List<string> { "重心位置", "风力大小", "飞控算法", "螺旋桨平衡" },
                Explanation = "无人机的飞行稳定性主要受重心位置、外部风力、飞控算法和螺旋桨平衡等因素影响。",
                CreatedBy = "系统",
                CreatedTime = DateTime.Now
            });

            return questions;
        }

        /// <summary>
        /// 强制重新加载题库
        /// </summary>
        public static void ReloadQuestions()
        {
            lock (_lockObject)
            {
                _cacheLoaded = false;
                LoadQuestions();
            }
        }
    }
}