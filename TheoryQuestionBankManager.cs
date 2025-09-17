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
        // 添加一个静态集合来跟踪正在分配的ID
        private static readonly HashSet<int> _assignedIds = new HashSet<int>();

        /// <summary>
        /// 获取下一个可用的题目ID（5位数，从00001开始）
        /// </summary>
        public static string GetNextQuestionId()
        {
            lock (_lockObject)
            {
                if (!_cacheLoaded) LoadQuestions();

                // 获取所有现存的数字ID
                var existingIds = new HashSet<int>();

                foreach (var question in _questionCache)
                {
                    // 尝试解析现有ID为数字
                    if (int.TryParse(question.Id, out int numericId))
                    {
                        existingIds.Add(numericId);
                    }
                }

                // 合并已分配但尚未保存的ID
                existingIds.UnionWith(_assignedIds);

                // 找到下一个未使用的ID，从1开始
                int nextId = 1;
                while (existingIds.Contains(nextId) && nextId <= 99999)
                {
                    nextId++;
                }

                // 如果超过99999，抛出异常
                if (nextId > 99999)
                {
                    throw new InvalidOperationException("题目数量已达到上限（99999），无法添加更多题目");
                }

                // 将新分配的ID添加到跟踪集合中
                _assignedIds.Add(nextId);

                // 返回5位数字格式的ID
                return nextId.ToString("D5");
            }
        }

        /// <summary>
        /// 为题目分配新的系统ID（如果需要的话）
        /// </summary>
        /// <param name="question">要处理的题目</param>
        /// <param name="forceNewId">是否强制分配新ID，忽略原有ID</param>
        public static void AssignSystemId(TheoryQuestion question, bool forceNewId = false)
        {
            if (question == null) return;

            // 如果强制分配新ID，或者原ID不是5位数字格式，则分配新ID
            if (forceNewId || !IsValidSystemId(question.Id))
            {
                question.Id = GetNextQuestionId();
            }
        }

        /// <summary>
        /// 检查ID是否为有效的系统5位数ID格式
        /// </summary>
        /// <param name="id">要检查的ID</param>
        /// <returns>是否为有效格式</returns>
        public static bool IsValidSystemId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            // 检查是否为5位数字
            return id.Length == 5 && int.TryParse(id, out int numericId) && numericId >= 1 && numericId <= 99999;
        }

        /// <summary>
        /// 批量为题目分配系统ID（用于导入时）
        /// </summary>
        /// <param name="questions">题目列表</param>
        /// <param name="forceNewIds">是否强制为所有题目分配新ID</param>
        public static void AssignSystemIds(List<TheoryQuestion> questions, bool forceNewIds = false)
        {
            if (questions == null || !questions.Any()) return;

            foreach (var question in questions)
            {
                AssignSystemId(question, forceNewIds);
            }
        }

        /// <summary>
        /// 获取所有理论题目
        /// </summary>
        /// <exception cref="TheoryBankException">当题库加载失败时抛出</exception>
        public static List<TheoryQuestion> GetAllQuestions()
        {
            lock (_lockObject)
            {
                if (!_cacheLoaded)
                {
                    LoadQuestions(); // 这里可能会抛出异常
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

                    // 确保题目有有效的系统ID
                    if (string.IsNullOrEmpty(question.Id) || !IsValidSystemId(question.Id))
                    {
                        AssignSystemId(question);
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

                    // 保存成功后，从临时跟踪集合中移除该ID（如果存在）
                    if (int.TryParse(question.Id, out int numericId))
                    {
                        _assignedIds.Remove(numericId);
                    }

                    return SaveQuestions();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"保存理论题目失败: {ex.Message}");

                    // 保存失败时，也要从临时跟踪集合中移除该ID
                    if (int.TryParse(question.Id, out int numericId))
                    {
                        _assignedIds.Remove(numericId);
                    }

                    return false;
                }
            }
        }

        /// <summary>
        /// 清理临时分配的ID（可选方法，用于重置状态）
        /// </summary>
        public static void ClearAssignedIds()
        {
            lock (_lockObject)
            {
                _assignedIds.Clear();
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

        /// <summary>
        /// 加载题目 - 修复版：让异常向上传播
        /// </summary>
        /// <exception cref="TheoryBankException">当加载失败时抛出详细异常</exception>
        private static void LoadQuestions()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 开始加载理论题库 ===");

                // 1. 检查并创建目录
                if (!Directory.Exists(THEORY_BANK_DIRECTORY))
                {
                    System.Diagnostics.Debug.WriteLine($"创建理论题库目录: {THEORY_BANK_DIRECTORY}");
                    Directory.CreateDirectory(THEORY_BANK_DIRECTORY);
                }

                string filePath = Path.Combine(THEORY_BANK_DIRECTORY, THEORY_BANK_FILE);
                System.Diagnostics.Debug.WriteLine($"题库文件路径: {filePath}");

                if (File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("题库文件存在，开始读取...");

                    // 2. 检查文件权限和可读性
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        System.Diagnostics.Debug.WriteLine($"文件大小: {fileInfo.Length} 字节");
                        System.Diagnostics.Debug.WriteLine($"文件创建时间: {fileInfo.CreationTime}");
                        System.Diagnostics.Debug.WriteLine($"文件修改时间: {fileInfo.LastWriteTime}");
                    }
                    catch (Exception fileInfoEx)
                    {
                        throw new TheoryBankException($"无法获取题库文件信息：{fileInfoEx.Message}", fileInfoEx);
                    }

                    // 3. 读取文件内容
                    string json;
                    try
                    {
                        json = File.ReadAllText(filePath, Encoding.UTF8);
                        System.Diagnostics.Debug.WriteLine($"成功读取文件，内容长度: {json.Length} 字符");

                        if (string.IsNullOrWhiteSpace(json))
                        {
                            throw new TheoryBankException("题库文件为空或只包含空白字符");
                        }

                        // 显示文件内容的前100个字符用于调试
                        string preview = json.Length > 100 ? json.Substring(0, 100) + "..." : json;
                        System.Diagnostics.Debug.WriteLine($"文件内容预览: {preview}");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new TheoryBankException($"没有权限读取题库文件：{filePath}", ex);
                    }
                    catch (IOException ex)
                    {
                        throw new TheoryBankException($"读取题库文件时发生IO错误：{ex.Message}", ex);
                    }
                    catch (Exception ex)
                    {
                        throw new TheoryBankException($"读取题库文件失败：{ex.Message}", ex);
                    }

                    // 4. JSON 反序列化
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("开始JSON反序列化...");

                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            AllowTrailingCommas = true
                        };

                        var questions = JsonSerializer.Deserialize<List<TheoryQuestion>>(json, options);

                        if (questions == null)
                        {
                            System.Diagnostics.Debug.WriteLine("⚠️ JSON反序列化返回null，创建空列表");
                            questions = new List<TheoryQuestion>();
                        }

                        _questionCache = questions;
                        System.Diagnostics.Debug.WriteLine($"✅ 成功加载 {_questionCache.Count} 道理论题目");
                    }
                    catch (JsonException ex)
                    {
                        throw new TheoryBankException($"题库文件JSON格式错误：{ex.Message}\n" +
                                                     $"可能的原因：文件损坏、格式不正确或编码问题", ex);
                    }
                    catch (Exception ex)
                    {
                        throw new TheoryBankException($"JSON反序列化失败：{ex.Message}", ex);
                    }
                }
                else
                {
                    // 5. 首次运行，创建示例题目
                    System.Diagnostics.Debug.WriteLine("题库文件不存在，创建示例题目...");

                    try
                    {
                        _questionCache = CreateSampleTheoryQuestions();
                        System.Diagnostics.Debug.WriteLine($"创建了 {_questionCache.Count} 道示例题目");

                        // 保存示例题目
                        if (!SaveQuestions())
                        {
                            throw new TheoryBankException("创建示例题目后保存失败");
                        }

                        System.Diagnostics.Debug.WriteLine("✅ 示例题目保存成功");
                    }
                    catch (Exception ex)
                    {
                        throw new TheoryBankException($"创建示例题目失败：{ex.Message}", ex);
                    }
                }

                _cacheLoaded = true;
                System.Diagnostics.Debug.WriteLine("=== 理论题库加载完成 ===");
            }
            catch (TheoryBankException)
            {
                // 重新抛出自定义异常
                _cacheLoaded = false;
                throw;
            }
            catch (Exception ex)
            {
                _cacheLoaded = false;
                System.Diagnostics.Debug.WriteLine($"❌ 加载理论题库时发生未知错误：{ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪：{ex.StackTrace}");

                throw new TheoryBankException($"加载理论题库时发生未知错误：{ex.Message}", ex);
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
                Id = "00001", // 使用5位数ID
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
                Id = "00002", // 使用5位数ID
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

    /// <summary>
    /// 理论题库自定义异常类
    /// </summary>
    public class TheoryBankException : Exception
    {
        public TheoryBankException(string message) : base(message) { }
        public TheoryBankException(string message, Exception innerException) : base(message, innerException) { }
    }
}