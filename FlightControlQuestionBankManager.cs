using AutoPilot.Parameters;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

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

        #region 导入导出功能

        /// <summary>
        /// 导出题库到CSV文件
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <param name="questions">要导出的题目列表，为空则导出所有题目</param>
        /// <returns>导出结果</returns>
        public static FCImportExportResult ExportToCSV(string filePath, List<FlightControlQuestion>? questions = null)
        {
            var result = new FCImportExportResult();

            try
            {
                if (!_cacheLoaded) LoadQuestions();

                var questionsToExport = questions ?? _questionCache;

                if (!questionsToExport.Any())
                {
                    result.Success = false;
                    result.Message = "没有题目可导出";
                    return result;
                }

                // 创建备份
                CreateBackup($"before_export_{questionsToExport.Count}_questions");

                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                // 写入CSV表头
                writer.WriteLine(GetCSVHeader());

                // 写入题目数据
                foreach (var question in questionsToExport)
                {
                    try
                    {
                        writer.WriteLine(QuestionToCSVLine(question));
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        result.FailCount++;
                        result.Errors.Add($"导出题目 {question.Id} 失败: {ex.Message}");
                    }
                }

                result.Success = true;
                result.Message = $"成功导出 {result.SuccessCount} 道题目到 {filePath}";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"导出失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 从CSV文件导入题库
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <param name="overwriteExisting">是否覆盖现有题目</param>
        /// <returns>导入结果</returns>
        public static FCImportExportResult ImportFromCSV(string filePath, bool overwriteExisting = false)
        {
            var result = new FCImportExportResult();

            try
            {
                if (!File.Exists(filePath))
                {
                    result.Success = false;
                    result.Message = "导入文件不存在";
                    return result;
                }

                if (!_cacheLoaded) LoadQuestions();

                // 创建导入前备份
                CreateBackup($"before_import_{Path.GetFileNameWithoutExtension(filePath)}");

                var lines = File.ReadAllLines(filePath, Encoding.UTF8);

                if (lines.Length < 2)
                {
                    result.Success = false;
                    result.Message = "CSV文件格式错误或为空";
                    return result;
                }

                // 验证表头
                var header = lines[0];
                if (!IsValidCSVHeader(header))
                {
                    result.Success = false;
                    result.Message = "CSV文件表头格式不正确";
                    return result;
                }

                // 解析题目数据
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    try
                    {
                        var question = ParseCSVLine(line, i + 1);
                        if (question != null)
                        {
                            // 🔧 修改：由于系统自动分配ID，不再检查重复，直接添加
                            // 检查是否有相同题目陈述的题目（防止导入完全相同的题目）
                            var duplicateQuestion = _questionCache.FirstOrDefault(q =>
                                q.QuestionStatement.Trim().Equals(question.QuestionStatement.Trim(), StringComparison.OrdinalIgnoreCase));

                            if (duplicateQuestion != null)
                            {
                                if (overwriteExisting)
                                {
                                    // 覆盖现有题目（保持原ID）
                                    question.Id = duplicateQuestion.Id;
                                    var index = _questionCache.IndexOf(duplicateQuestion);
                                    _questionCache[index] = question;
                                    result.OverwriteCount++;
                                }
                                else
                                {
                                    // 跳过重复题目
                                    result.SkipCount++;
                                    result.Warnings.Add($"第{i + 1}行: 题目陈述已存在，已跳过");
                                    continue;
                                }
                            }
                            else
                            {
                                // 添加新题目（使用系统分配的新ID）
                                _questionCache.Add(question);
                                result.SuccessCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailCount++;
                        result.Errors.Add($"第{i + 1}行解析失败: {ex.Message}");
                    }
                }

                // 保存导入结果
                if (result.SuccessCount > 0 || result.OverwriteCount > 0)
                {
                    if (SaveQuestions())
                    {
                        result.Success = true;
                        result.Message = $"导入完成: 新增 {result.SuccessCount} 题, 覆盖 {result.OverwriteCount} 题, " +
                                        $"跳过 {result.SkipCount} 题, 失败 {result.FailCount} 题\n\n" +
                                        $"注意：所有新题目已由系统自动分配新的ID";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "题目解析成功但保存失败";
                    }
                }
                else
                {
                    result.Success = false;
                    result.Message = "没有成功导入任何题目";
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"导入失败: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 生成CSV表头
        /// </summary>
        private static string GetCSVHeader()
        {
            return "题目ID,题目陈述,题目类型,题目分类,难度等级,分值,参数名称,参数描述,数据类型,正确答案," +
                   "容差值,最小值,最大值,单位,需要飞控读取,验证方法,解释说明,创建人,创建时间,是否启用";
        }

        /// <summary>
        /// 验证CSV表头是否正确
        /// </summary>
        private static bool IsValidCSVHeader(string header)
        {
            var expectedHeader = GetCSVHeader();
            return string.Equals(header.Trim(), expectedHeader, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将题目转换为CSV行
        /// </summary>
        private static string QuestionToCSVLine(FlightControlQuestion question)
        {
            var fields = new string[]
            {
                EscapeCSVField(question.Id),
                EscapeCSVField(question.QuestionStatement),
                EscapeCSVField(question.Type.ToString()),
                EscapeCSVField(question.Category.ToString()),
                EscapeCSVField(question.Difficulty.ToString()),
                question.Points.ToString(),
                EscapeCSVField(question.ParameterName),
                EscapeCSVField(question.ParameterDescription),
                EscapeCSVField(question.DataType.ToString()),
                EscapeCSVField(question.CorrectValue),
                question.Tolerance.ToString("F6"),
                EscapeCSVField(question.MinValue),
                EscapeCSVField(question.MaxValue),
                EscapeCSVField(question.Unit),
                question.RequireFlightControllerRead.ToString(),
                EscapeCSVField(question.VerifyMethod.ToString()),
                EscapeCSVField(question.Explanation),
                EscapeCSVField(question.CreatedBy),
                question.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                question.IsActive.ToString()
            };

            return string.Join(",", fields);
        }

        /// <summary>
        /// 解析CSV行为题目对象
        /// </summary>
        private static FlightControlQuestion? ParseCSVLine(string line, int lineNumber)
        {
            var fields = ParseCSVFields(line);

            if (fields.Length != 20)
            {
                throw new FormatException($"CSV行字段数量不正确，期望20个字段，实际{fields.Length}个");
            }

            try
            {
                var question = new FlightControlQuestion
                {
                    // 🔧 修改：不使用CSV文件中的ID，由系统自动分配
                    // Id = fields[0],  // 删除这一行
                    Id = GetNextQuestionId(), // 🔧 新增：系统自动分配ID
                    QuestionStatement = fields[1],
                    Type = Enum.Parse<FCQuestionType>(fields[2]),
                    Category = Enum.Parse<FCQuestionCategory>(fields[3]),
                    Difficulty = Enum.Parse<QuestionDifficulty>(fields[4]),
                    Points = int.Parse(fields[5]),
                    ParameterName = fields[6],
                    ParameterDescription = fields[7],
                    DataType = Enum.Parse<ParameterDataType>(fields[8]),
                    CorrectValue = fields[9],
                    Tolerance = double.Parse(fields[10]),
                    MinValue = fields[11],
                    MaxValue = fields[12],
                    Unit = fields[13],
                    RequireFlightControllerRead = bool.Parse(fields[14]),
                    VerifyMethod = Enum.Parse<ParameterVerifyMethod>(fields[15]),
                    Explanation = fields[16],
                    CreatedBy = fields[17],
                    CreatedTime = DateTime.Parse(fields[18]),
                    IsActive = bool.Parse(fields[19]),
                    LastModified = DateTime.Now,
                    LastModifiedBy = "导入系统"
                };

                // 验证题目数据
                if (!question.IsValid())
                {
                    throw new ArgumentException("题目数据验证失败");
                }

                return question;
            }
            catch (Exception ex)
            {
                throw new FormatException($"解析题目数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 转义CSV字段（处理包含逗号、引号、换行符的字段）
        /// </summary>
        private static string EscapeCSVField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // 如果字段包含逗号、引号或换行符，需要用引号包围并转义内部引号
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }

        /// <summary>
        /// 解析CSV字段（处理引号包围和转义）
        /// </summary>
        private static string[] ParseCSVFields(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            bool nextCharIsEscaped = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (nextCharIsEscaped)
                {
                    currentField.Append(c);
                    nextCharIsEscaped = false;
                }
                else if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 转义的引号
                        currentField.Append('"');
                        nextCharIsEscaped = true;
                    }
                    else
                    {
                        // 开始或结束引号
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // 字段分隔符
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // 添加最后一个字段
            fields.Add(currentField.ToString());

            return fields.ToArray();
        }

        /// <summary>
        /// 显示导入对话框
        /// </summary>
        public static void ShowImportDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "导入飞控题库",
                Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                DefaultExt = "csv",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var importWindow = new FCImportProgressWindow();
                    importWindow.StartImport(openFileDialog.FileName);
                    importWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开导入窗口失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 显示导出对话框
        /// </summary>
        public static void ShowExportDialog(List<FlightControlQuestion>? selectedQuestions = null)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = "导出飞控题库",
                Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"飞控题库_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var result = ExportToCSV(saveFileDialog.FileName, selectedQuestions);

                if (result.Success)
                {
                    MessageBox.Show(result.Message, "导出成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"导出失败：\n{result.Message}", "导出失败",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 公共的CSV行解析方法（供导入窗口使用）
        /// </summary>
        public static FlightControlQuestion? ParseCSVLinePublic(string line, int lineNumber)
        {
            try
            {
                return ParseCSVLine(line, lineNumber);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解析CSV行失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验证CSV表头（公共方法）
        /// </summary>
        public static bool ValidateCSVHeader(string header)
        {
            return IsValidCSVHeader(header);
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

    #region 导入导出辅助类
    /// <summary>
    /// 飞控题库导入导出结果
    /// </summary>
    public class FCImportExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int OverwriteCount { get; set; }
        public int SkipCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
    #endregion
}
