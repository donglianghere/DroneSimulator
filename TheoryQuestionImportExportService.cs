using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DroneSimulator
{
    /// <summary>
    /// 理论题库导入导出服务
    /// </summary>
    public static class TheoryQuestionImportExportService
    {
        /// <summary>
        /// 进度回调委托
        /// </summary>
        /// <param name="progress">进度百分比 (0-100)</param>
        /// <param name="message">进度消息</param>
        /// <param name="detail">详细信息</param>
        public delegate void ProgressCallback(double progress, string message, string detail = "");

        #region JSON 格式导入导出

        /// <summary>
        /// 导出理论题库到JSON文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="questions">要导出的题目列表，为空时导出全部</param>
        /// <returns>导出结果</returns>
        public static ImportExportResult ExportToJson(string filePath, List<TheoryQuestion>? questions = null)
        {
            try
            {
                var questionsToExport = questions ?? TheoryQuestionBankManager.GetAllQuestions();

                var exportData = new TheoryQuestionExportData
                {
                    ExportTime = DateTime.Now,
                    ExportVersion = "1.0",
                    TotalQuestions = questionsToExport.Count,
                    Questions = questionsToExport
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(exportData, options);
                File.WriteAllText(filePath, json, Encoding.UTF8);

                return new ImportExportResult
                {
                    Success = true,
                    Message = $"成功导出 {questionsToExport.Count} 道题目到 {Path.GetFileName(filePath)}",
                    ProcessedCount = questionsToExport.Count
                };
            }
            catch (Exception ex)
            {
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"JSON导出失败：{ex.Message}",
                    ProcessedCount = 0
                };
            }
        }

        /// <summary>
        /// 从JSON文件导入理论题库 - 支持进度回调
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="overwriteExisting">是否覆盖已存在的题目</param>
        /// <param name="currentUser">当前导入操作的用户名</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>导入结果</returns>
        public static ImportExportResult ImportFromJson(string filePath, bool overwriteExisting = false, string? currentUser = null, ProgressCallback? progressCallback = null)
        {
            try
            {
                progressCallback?.Invoke(5, "正在读取JSON文件...", "");

                if (!File.Exists(filePath))
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "文件不存在",
                        ProcessedCount = 0
                    };
                }

                progressCallback?.Invoke(10, "正在解析JSON内容...", "");

                string json = File.ReadAllText(filePath, Encoding.UTF8);

                // 尝试解析为导出数据格式
                TheoryQuestionExportData? exportData = null;
                List<TheoryQuestion>? questions = null;

                try
                {
                    exportData = JsonSerializer.Deserialize<TheoryQuestionExportData>(json);
                    questions = exportData?.Questions;
                }
                catch
                {
                    // 如果解析导出格式失败，尝试直接解析为题目列表
                    questions = JsonSerializer.Deserialize<List<TheoryQuestion>>(json);
                }

                if (questions == null || !questions.Any())
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "文件中没有找到有效的题目数据",
                        ProcessedCount = 0
                    };
                }

                progressCallback?.Invoke(20, "正在验证和导入题目...", $"共找到 {questions.Count} 道题目");

                // 验证和导入题目
                int successCount = 0;
                int skipCount = 0;
                var errors = new List<string>();
                int totalQuestions = questions.Count;

                for (int i = 0; i < questions.Count; i++)
                {
                    var question = questions[i];

                    try
                    {
                        // 更新进度 (20% + 70% * 处理进度)
                        double currentProgress = 20 + (70.0 * i / totalQuestions);
                        progressCallback?.Invoke(currentProgress, "正在导入题目...", $"正在处理第 {i + 1}/{totalQuestions} 道题目");

                        // 验证题目数据
                        if (!question.IsValid())
                        {
                            errors.Add($"题目 '{question.QuestionStatement}' 数据不完整，已跳过");
                            skipCount++;
                            continue;
                        }

                        // 系统自动分配新ID，忽略文件中的原始ID
                        TheoryQuestionBankManager.AssignSystemId(question, forceNewId: true);

                        // 设置导入时间
                        question.LastModified = DateTime.Now;
                        if (string.IsNullOrEmpty(question.CreatedBy))
                        {
                            question.CreatedBy = !string.IsNullOrWhiteSpace(currentUser) ? currentUser : "JSON导入";
                        }

                        // 保存题目（使用新ID，不会有重复问题）
                        if (TheoryQuestionBankManager.SaveQuestion(question))
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"保存题目 '{question.QuestionStatement}' 失败");
                            skipCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"处理题目 '{question.QuestionStatement}' 时出错：{ex.Message}");
                        skipCount++;
                    }
                }

                progressCallback?.Invoke(95, "正在完成导入...", "");

                var message = $"JSON导入完成：成功 {successCount} 题，跳过 {skipCount} 题\n";
                message += $"💡 说明：所有题目已分配新的系统ID（5位数字），从 00001 开始递增。";

                if (errors.Any())
                {
                    message += $"\n\n详细错误：\n{string.Join("\n", errors.Take(10))}";
                    if (errors.Count > 10)
                    {
                        message += $"\n... 还有 {errors.Count - 10} 个错误";
                    }
                }

                progressCallback?.Invoke(100, "导入完成", $"成功导入 {successCount} 道题目");

                return new ImportExportResult
                {
                    Success = successCount > 0,
                    Message = message,
                    ProcessedCount = successCount
                };
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke(0, "导入失败", ex.Message);
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"JSON导入失败：{ex.Message}",
                    ProcessedCount = 0
                };
            }
        }

        #endregion

        
        #region CSV 格式导入导出

        /// <summary>
        /// 导出理论题库到CSV文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="questions">要导出的题目列表，为空时导出全部</param>
        /// <returns>导出结果</returns>
        public static ImportExportResult ExportToCsv(string filePath, List<TheoryQuestion>? questions = null)
        {
            try
            {
                var questionsToExport = questions ?? TheoryQuestionBankManager.GetAllQuestions();

                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

                // 写入CSV标题行
                writer.WriteLine("题目ID,题目陈述,题目类型,内容分类,难度等级,分值,是否启用,选项A,选项B,选项C,选项D,选项E,选项F,正确答案,题目解析,出题人,创建时间,最后修改时间");

                foreach (var question in questionsToExport)
                {
                    var line = new List<string>
                    {
                        EscapeCsvField(question.Id),
                        EscapeCsvField(question.QuestionStatement),
                        EscapeCsvField(question.GetTypeDisplayName()),
                        EscapeCsvField(question.GetCategoryDisplayName()),
                        EscapeCsvField(question.GetDifficultyDisplayName()),
                        question.Points.ToString(),
                        question.IsActive ? "是" : "否"
                    };

                    // 添加选项（最多6个）
                    for (int i = 0; i < 6; i++)
                    {
                        if (i < question.Options.Count)
                        {
                            line.Add(EscapeCsvField(question.Options[i].Text));
                        }
                        else
                        {
                            line.Add("");
                        }
                    }

                    // 正确答案（用分号分隔多个答案）
                    line.Add(EscapeCsvField(string.Join(";", question.CorrectAnswers)));

                    // 其他信息
                    line.Add(EscapeCsvField(question.Explanation));
                    line.Add(EscapeCsvField(question.CreatedBy));
                    line.Add(question.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    line.Add(question.LastModified.ToString("yyyy-MM-dd HH:mm:ss"));

                    writer.WriteLine(string.Join(",", line));
                }

                return new ImportExportResult
                {
                    Success = true,
                    Message = $"成功导出 {questionsToExport.Count} 道题目到 {Path.GetFileName(filePath)}",
                    ProcessedCount = questionsToExport.Count
                };
            }
            catch (Exception ex)
            {
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"CSV导出失败：{ex.Message}",
                    ProcessedCount = 0
                };
            }
        }

        /// <summary>
        /// 从CSV文件导入理论题库 - 支持进度回调
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="overwriteExisting">是否覆盖已存在的题目</param>
        /// <param name="currentUser">当前导入操作的用户名</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>导入结果</returns>
        public static ImportExportResult ImportFromCsv(string filePath, bool overwriteExisting = false, string? currentUser = null, ProgressCallback? progressCallback = null)
        {
            try
            {
                progressCallback?.Invoke(5, "正在读取CSV文件...", "");

                if (!File.Exists(filePath))
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "文件不存在",
                        ProcessedCount = 0
                    };
                }

                progressCallback?.Invoke(10, "正在解析CSV内容...", "");

                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length < 2) // 至少要有标题行和一行数据
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "CSV文件格式不正确或没有数据",
                        ProcessedCount = 0
                    };
                }

                progressCallback?.Invoke(15, "正在验证CSV格式...", $"文件共 {lines.Length} 行");

                int successCount = 0;
                int skipCount = 0;
                var errors = new List<string>();
                int totalLines = lines.Length - 1; // 减去标题行

                // 跳过标题行，从第二行开始处理
                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        // 更新进度 (15% + 80% * 处理进度)
                        double currentProgress = 15 + (80.0 * (i - 1) / totalLines);
                        progressCallback?.Invoke(currentProgress, "正在导入CSV题目...", $"正在处理第 {i}/{lines.Length} 行");

                        var fields = ParseCsvLine(lines[i]);
                        if (fields.Count < 8) // 最少需要基本字段
                        {
                            errors.Add($"第 {i + 1} 行：字段数量不足");
                            skipCount++;
                            continue;
                        }

                        var question = new TheoryQuestion();

                        // 系统自动分配ID，忽略文件中的原始ID（字段0）
                        TheoryQuestionBankManager.AssignSystemId(question, forceNewId: true);

                        question.QuestionStatement = fields[1];

                        // 解析题目类型
                        question.Type = fields[2] switch
                        {
                            "多选题" => TheoryQuestionType.MultipleChoice,
                            _ => TheoryQuestionType.SingleChoice
                        };

                        // 解析分类
                        question.Category = fields[3] switch
                        {
                            "结构组成" => TheoryQuestionCategory.Structure,
                            "控制算法" => TheoryQuestionCategory.ControlAlgorithm,
                            "传感器融合" => TheoryQuestionCategory.SensorFusion,
                            "飞行安全" => TheoryQuestionCategory.FlightSafety,
                            "法律法规" => TheoryQuestionCategory.LawsRegulations,
                            _ => TheoryQuestionCategory.FlightPrinciples
                        };

                        // 解析难度
                        question.Difficulty = fields[4] switch
                        {
                            "简单" => QuestionDifficulty.Easy,
                            "困难" => QuestionDifficulty.Hard,
                            _ => QuestionDifficulty.Medium
                        };

                        // 解析分值
                        if (int.TryParse(fields[5], out int points))
                        {
                            question.Points = points;
                        }
                        else
                        {
                            question.Points = 2;
                        }

                        // 解析是否启用
                        question.IsActive = fields[6] == "是";

                        // 解析选项（字段7-12）
                        question.Options = new List<TheoryOption>();
                        for (int j = 7; j < 13 && j < fields.Count; j++)
                        {
                            if (!string.IsNullOrWhiteSpace(fields[j]))
                            {
                                question.Options.Add(new TheoryOption { Text = fields[j] });
                            }
                        }

                        // 解析正确答案
                        if (fields.Count > 13 && !string.IsNullOrWhiteSpace(fields[13]))
                        {
                            question.CorrectAnswers = fields[13].Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                        }

                        // 解析题目解析
                        if (fields.Count > 14)
                        {
                            question.Explanation = fields[14];
                        }

                        // 解析出题人
                        if (fields.Count > 15)
                        {
                            question.CreatedBy = string.IsNullOrWhiteSpace(fields[15]) ?
                                (!string.IsNullOrWhiteSpace(currentUser) ? currentUser : "CSV导入") : fields[15];
                        }
                        else
                        {
                            question.CreatedBy = !string.IsNullOrWhiteSpace(currentUser) ? currentUser : "CSV导入";
                        }

                        // 解析时间
                        if (fields.Count > 16 && DateTime.TryParse(fields[16], out DateTime createdTime))
                        {
                            question.CreatedTime = createdTime;
                        }
                        else
                        {
                            question.CreatedTime = DateTime.Now;
                        }

                        question.LastModified = DateTime.Now;

                        // 验证题目
                        if (!question.IsValid())
                        {
                            errors.Add($"第 {i + 1} 行：题目数据不完整");
                            skipCount++;
                            continue;
                        }

                        // 保存题目（使用新ID，不会重复）
                        if (TheoryQuestionBankManager.SaveQuestion(question))
                        {
                            successCount++;
                        }
                        else
                        {
                            errors.Add($"第 {i + 1} 行：保存题目失败");
                            skipCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"第 {i + 1} 行：处理出错 - {ex.Message}");
                        skipCount++;
                    }
                }

                progressCallback?.Invoke(100, "CSV导入完成", $"成功导入 {successCount} 道题目");

                var message = $"CSV导入完成：成功 {successCount} 题，跳过 {skipCount} 题\n";
                message += $"💡 说明：所有题目已分配新的系统ID（5位数字），从 00001 开始递增。";

                if (errors.Any())
                {
                    message += $"\n\n详细错误：\n{string.Join("\n", errors.Take(10))}";
                    if (errors.Count > 10)
                    {
                        message += $"\n... 还有 {errors.Count - 10} 个错误";
                    }
                }

                return new ImportExportResult
                {
                    Success = successCount > 0,
                    Message = message,
                    ProcessedCount = successCount
                };
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke(0, "导入失败", ex.Message);
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"CSV导入失败：{ex.Message}",
                    ProcessedCount = 0
                };
            }
        }

        /// <summary>
        /// 从 TQ4.csv 格式导入理论题库 - 支持进度回调（完整版）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="overwriteExisting">是否覆盖已存在的题目</param>
        /// <param name="currentUser">当前导入操作的用户名</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>导入结果</returns>
        public static ImportExportResult ImportFromTQ4Csv(string filePath, bool overwriteExisting = false, string? currentUser = null, ProgressCallback? progressCallback = null)
        {
            try
            {
                progressCallback?.Invoke(5, "正在读取TQ4.csv文件...", "");

                if (!File.Exists(filePath))
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "文件不存在",
                        ProcessedCount = 0
                    };
                }

                progressCallback?.Invoke(10, "正在检测文件编码...", "");

                // 智能检测和读取文件编码
                string[] lines = ReadFileWithCorrectEncoding(filePath);

                if (lines.Length < 2)
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "CSV文件没有数据或格式不正确",
                        ProcessedCount = 0
                    };
                }

                progressCallback?.Invoke(15, "正在分析文件内容...", $"文件共 {lines.Length} 行");

                int successCount = 0;
                int skipCount = 0;
                var errors = new List<string>();
                var detailedErrors = new List<string>();

                // 收集所有有效题目到临时列表
                var validQuestions = new List<TheoryQuestion>();
                var tempAssignedIds = new HashSet<int>(); // 临时ID跟踪，避免重复

                int totalLines = lines.Length - 1; // 减去标题行

                // 从第2行开始处理数据（第1行是标题）
                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        // 更新进度 (15% + 70% * 处理进度)
                        double currentProgress = 15 + (70.0 * (i - 1) / totalLines);
                        progressCallback?.Invoke(currentProgress, "正在解析TQ4题目...", $"正在处理第 {i}/{lines.Length} 行");

                        var line = lines[i].Trim();

                        // 跳过空行
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var fields = ParseCsvLine(line);

                        // 补全字段
                        if (fields.Count < 15)
                        {
                            while (fields.Count < 25)
                            {
                                fields.Add("");
                            }
                        }

                        // 检查题目陈述是否为空（第5列，索引4）
                        if (fields.Count <= 4 || string.IsNullOrWhiteSpace(fields[4]))
                        {
                            detailedErrors.Add($"第 {i + 1} 行：题目陈述为空，跳过");
                            skipCount++;
                            continue;
                        }

                        var question = new TheoryQuestion();

                        // 保存原始ID仅用于分类推断和日志记录
                        var originalId = fields.Count > 1 ? fields[1].Trim() : $"行{i}";

                        // 手动分配ID
                        int nextId = 1;
                        while (tempAssignedIds.Contains(nextId) && nextId <= 99999)
                        {
                            nextId++;
                        }

                        if (nextId > 99999)
                        {
                            detailedErrors.Add($"第 {i + 1} 行：题目数量已达到上限，无法分配更多ID");
                            skipCount++;
                            continue;
                        }

                        tempAssignedIds.Add(nextId);
                        question.Id = nextId.ToString("D5");

                        // ★★★ 完整的TQ4题目解析逻辑 ★★★
                        // 解析题目陈述 (字段5，索引4)
                        question.QuestionStatement = fields[4].Trim();

                        // 解析题目类型 (字段3，索引2)
                        var typeCode = fields.Count > 2 ? fields[2].Trim() : "B";
                        question.Type = ParseQuestionType(typeCode);

                        // 根据原始ID推断分类（仅用于分类，不用作题目ID）
                        question.Category = ParseQuestionCategory(originalId);

                        // 设置难度 (字段13，索引12)
                        var difficultyCode = fields.Count > 12 ? fields[12].Trim() : "3（中等）";
                        question.Difficulty = ParseDifficulty(difficultyCode);

                        // 设置分值
                        question.Points = question.Type == TheoryQuestionType.MultipleChoice ? 3 : 2;

                        // 解析选项 (字段6-11对应选项A-F，索引5-10)
                        question.Options = new List<TheoryOption>();
                        for (int optionIndex = 5; optionIndex <= 10; optionIndex++)
                        {
                            if (optionIndex < fields.Count && !string.IsNullOrWhiteSpace(fields[optionIndex]))
                            {
                                question.Options.Add(new TheoryOption
                                {
                                    Text = fields[optionIndex].Trim()
                                });
                            }
                        }

                        // 如果没有选项，为判断题创建默认选项
                        if (!question.Options.Any())
                        {
                            if (question.Type == TheoryQuestionType.MultipleChoice) // 判断题
                            {
                                question.Options.Add(new TheoryOption { Text = "正确" });
                                question.Options.Add(new TheoryOption { Text = "错误" });
                            }
                            else // 单选题至少需要两个选项
                            {
                                question.Options.Add(new TheoryOption { Text = "选项A" });
                                question.Options.Add(new TheoryOption { Text = "选项B" });
                            }
                        }

                        // ★★★ 关键修复：正确答案解析逻辑 ★★★
                        var correctAnswerField = fields.Count > 11 ? fields[11].Trim() : "A";
                        detailedErrors.Add($"第 {i + 1} 行：原始正确答案字段 = '{correctAnswerField}'");

                        question.CorrectAnswers = new List<string>();

                        if (!string.IsNullOrWhiteSpace(correctAnswerField))
                        {
                            // 提取所有A-F的字母
                            var answerChars = correctAnswerField.ToCharArray()
                                .Where(c => c >= 'A' && c <= 'F')
                                .Select(c => c.ToString())
                                .Distinct()
                                .ToList();

                            detailedErrors.Add($"第 {i + 1} 行：提取的答案代号 = [{string.Join(", ", answerChars)}]");

                            // 将答案代号转换为对应的选项文本
                            foreach (var code in answerChars)
                            {
                                int optionIndex = code[0] - 'A'; // A=0, B=1, C=2...
                                if (optionIndex >= 0 && optionIndex < question.Options.Count)
                                {
                                    question.CorrectAnswers.Add(question.Options[optionIndex].Text);
                                    detailedErrors.Add($"第 {i + 1} 行：答案代号 {code} -> 选项文本 '{question.Options[optionIndex].Text}'");
                                }
                                else
                                {
                                    detailedErrors.Add($"第 {i + 1} 行：答案代号 {code} 超出选项范围 (选项数={question.Options.Count})");
                                }
                            }
                        }

                        // 如果仍然没有正确答案，设置默认值
                        if (!question.CorrectAnswers.Any() && question.Options.Any())
                        {
                            question.CorrectAnswers.Add(question.Options[0].Text);
                            detailedErrors.Add($"第 {i + 1} 行：使用默认答案 '{question.Options[0].Text}'");
                        }

                        detailedErrors.Add($"第 {i + 1} 行：最终正确答案 = [{string.Join(", ", question.CorrectAnswers)}]");

                        // 设置其他属性
                        question.Explanation = fields.Count > 14 ? fields[14] : "";
                        question.CreatedBy = !string.IsNullOrWhiteSpace(currentUser) ? currentUser : "TQ4.csv导入";
                        question.CreatedTime = DateTime.Now;
                        question.LastModified = DateTime.Now;
                        question.LastModifiedBy = !string.IsNullOrWhiteSpace(currentUser) ? currentUser : "系统导入";
                        question.IsActive = true;

                        // 验证题目
                        if (!question.IsValid())
                        {
                            detailedErrors.Add($"第 {i + 1} 行：题目验证失败，系统ID: {question.Id}, 原始ID: {originalId}");
                            detailedErrors.Add($"  - 验证详情：题目='{question.QuestionStatement.Substring(0, Math.Min(20, question.QuestionStatement.Length))}...', 选项数={question.Options.Count}, 答案数={question.CorrectAnswers.Count}");
                            skipCount++;
                            continue;
                        }

                        detailedErrors.Add($"第 {i + 1} 行：题目验证通过，系统ID: {question.Id}, 原始ID: {originalId}");

                        // 添加到临时列表
                        validQuestions.Add(question);
                    }
                    catch (Exception ex)
                    {
                        detailedErrors.Add($"第 {i + 1} 行：处理出错 - {ex.Message}");
                        skipCount++;
                    }
                }

                progressCallback?.Invoke(85, "正在保存题目到数据库...", $"准备保存 {validQuestions.Count} 道题目");

                // 批量保存所有题目
                if (validQuestions.Any())
                {
                    detailedErrors.Add($"开始批量保存 {validQuestions.Count} 个题目...");

                    try
                    {
                        // 获取当前题库，确保不会与现有题目ID冲突
                        var existingQuestions = TheoryQuestionBankManager.GetAllQuestions();
                        var existingIds = new HashSet<int>();

                        foreach (var q in existingQuestions)
                        {
                            if (int.TryParse(q.Id, out int existingNumericId))
                            {
                                existingIds.Add(existingNumericId);
                            }
                        }

                        // 重新分配ID以避免冲突
                        int currentId = 1;
                        foreach (var question in validQuestions)
                        {
                            // 找到下一个不冲突的ID
                            while (existingIds.Contains(currentId))
                            {
                                currentId++;
                            }

                            question.Id = currentId.ToString("D5");
                            existingIds.Add(currentId);
                            currentId++;
                        }

                        // 逐个保存题目
                        for (int i = 0; i < validQuestions.Count; i++)
                        {
                            var question = validQuestions[i];

                            // 更新保存进度 (85% + 10% * 保存进度)
                            double saveProgress = 85 + (10.0 * i / validQuestions.Count);
                            progressCallback?.Invoke(saveProgress, "正在保存题目...", $"正在保存第 {i + 1}/{validQuestions.Count} 道题目");

                            if (TheoryQuestionBankManager.SaveQuestion(question))
                            {
                                successCount++;
                                detailedErrors.Add($"保存成功 - ID: {question.Id}, 正确答案: [{string.Join(", ", question.CorrectAnswers)}]");
                            }
                            else
                            {
                                detailedErrors.Add($"保存失败 - ID: {question.Id}");
                                skipCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        detailedErrors.Add($"批量保存过程出错: {ex.Message}");
                        skipCount += validQuestions.Count;
                    }
                }

                progressCallback?.Invoke(100, "TQ4.csv导入完成", $"成功导入 {successCount} 道题目");

                // 构建详细的返回消息
                var message = $"TQ4.csv导入完成：\n";
                message += $"成功导入：{successCount} 题\n";
                message += $"跳过/失败：{skipCount} 题\n";
                message += $"总处理行数：{lines.Length - 1} 行\n";
                message += $"导入用户：{(!string.IsNullOrWhiteSpace(currentUser) ? currentUser : "未指定")}\n";
                message += $"\n💡 说明：\n";
                message += $"• 所有题目已分配新的系统ID（5位数字），从 00001 开始递增\n";
                message += $"• 正确答案已从选项代号转换为选项文本格式\n";
                message += $"• 完全忽略文件中的原始ID，每条记录都作为新题目导入";

                if (detailedErrors.Any())
                {
                    message += $"\n\n详细信息（前20条）：\n";
                    message += string.Join("\n", detailedErrors.Take(20));
                    if (detailedErrors.Count > 20)
                    {
                        message += $"\n... 还有 {detailedErrors.Count - 20} 条信息";
                    }
                }

                return new ImportExportResult
                {
                    Success = successCount > 0,
                    Message = message,
                    ProcessedCount = successCount
                };
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke(0, "导入失败", ex.Message);
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"TQ4.csv导入失败：{ex.Message}\n\n堆栈跟踪：{ex.StackTrace}",
                    ProcessedCount = 0
                };
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 转义CSV字段
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // 如果包含逗号、引号或换行符，需要用引号包围并转义内部引号
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return field;
        }

        /// <summary>
        /// 解析CSV行（现在为public，可以被其他类访问）
        /// </summary>
        public static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            if (string.IsNullOrEmpty(line))
            {
                return fields;
            }

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 转义的引号
                        currentField.Append('"');
                        i++; // 跳过下一个引号
                    }
                    else
                    {
                        // 切换引号状态
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // 字段分隔符
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // 添加最后一个字段
            fields.Add(currentField.ToString().Trim());

            return fields;
        }

        /// <summary>
        /// 智能检测文件编码并读取文件（增强版，支持.NET Core/.NET 5+）
        /// </summary>
        public static string[] ReadFileWithCorrectEncoding(string filePath)
        {
            // 首先注册编码提供程序（.NET Core/.NET 5+需要）
            RegisterEncodingProvider();

            // 尝试不同的编码方式，按优先级排序
            var encodingsToTry = new List<(string Name, Func<Encoding> GetEncoding)>
            {
                ("UTF-8", () => Encoding.UTF8),
                ("UTF-8 with BOM", () => new UTF8Encoding(true)),
                ("GB18030", () => TryGetEncoding("GB18030")),
                ("GBK", () => TryGetEncoding("GBK")),
                ("GB2312", () => TryGetEncoding("GB2312")),
                ("Big5", () => TryGetEncoding("Big5")),
                ("系统默认", () => Encoding.Default),
                ("ASCII", () => Encoding.ASCII),
                ("Unicode", () => Encoding.Unicode),
                ("UTF-32", () => Encoding.UTF32)
            };

            string[] bestResult = null;
            var maxScore = 0.0;
            string bestEncodingName = "";

            foreach (var (name, getEncoding) in encodingsToTry)
            {
                try
                {
                    var encoding = getEncoding();
                    if (encoding == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"编码 {name} 不可用，跳过");
                        continue;
                    }

                    var lines = File.ReadAllLines(filePath, encoding);

                    if (lines.Length > 1)
                    {
                        // 合并前几行进行分析
                        var sampleText = string.Join("", lines.Take(Math.Min(5, lines.Length)));

                        // 计算编码质量得分
                        var score = CalculateEncodingScore(sampleText, name);

                        System.Diagnostics.Debug.WriteLine($"编码 {name}: 得分 {score:F2}");

                        // 如果找到更好的编码
                        if (score > maxScore)
                        {
                            maxScore = score;
                            bestResult = lines;
                            bestEncodingName = name;
                        }

                        // 如果得分很高，认为找到了正确编码
                        if (score > 85.0) // 提高阈值确保质量
                        {
                            System.Diagnostics.Debug.WriteLine($"找到高质量编码: {name}");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"尝试编码 {name} 失败: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"最终选择编码: {bestEncodingName}, 得分: {maxScore:F2}");

            // 如果所有编码都失败，使用UTF-8并显示警告
            if (bestResult == null)
            {
                System.Diagnostics.Debug.WriteLine("所有编码尝试失败，使用UTF-8作为后备方案");
                bestResult = File.ReadAllLines(filePath, Encoding.UTF8);
            }

            return bestResult;
        }

        /// <summary>
        /// 注册编码提供程序（.NET Core/.NET 5+需要）
        /// </summary>
        private static void RegisterEncodingProvider()
        {
            try
            {
                // 注册代码页编码提供程序
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                System.Diagnostics.Debug.WriteLine("编码提供程序注册成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册编码提供程序失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全地尝试获取编码
        /// </summary>
        private static Encoding TryGetEncoding(string encodingName)
        {
            try
            {
                return Encoding.GetEncoding(encodingName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取编码 {encodingName} 失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 计算编码质量得分（增强版）
        /// </summary>
        private static double CalculateEncodingScore(string text, string encodingName)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            double score = 0;
            var textLength = text.Length;

            // 1. 中文字符数量（权重35%）
            var chineseCount = CountChineseCharacters(text);
            var chineseRatio = textLength > 0 ? (double)chineseCount / textLength : 0;
            score += chineseRatio * 35;

            // 2. 检查是否包含乱码（权重25%）
            if (!ContainsGarbledText(text))
            {
                score += 25;
            }

            // 3. 可显示字符比例（权重20%）
            var printableCount = text.Count(c => !char.IsControl(c) || char.IsWhiteSpace(c));
            var printableRatio = textLength > 0 ? (double)printableCount / textLength : 0;
            score += printableRatio * 20;

            // 4. 常见中文词汇检测（权重10%）
            var commonWords = new[] { "题目", "选项", "答案", "解析", "考试", "单选", "多选", "判断", "正确", "错误" };
            var wordCount = commonWords.Count(word => text.Contains(word));
            score += (double)wordCount / commonWords.Length * 10;

            // 5. CSV格式特征检测（权重10%）
            var csvFeatures = new[] { ",", "\"", "A-A-A", "A-B-A", "B（单选题）", "C（判断题）" };
            var csvFeatureCount = csvFeatures.Count(feature => text.Contains(feature));
            score += (double)csvFeatureCount / csvFeatures.Length * 10;

            // 6. 特殊字符惩罚
            var specialChars = new[] { '�', '锟', '烫', '屯', '\uFFFD' };
            var specialCharCount = specialChars.Sum(c => text.Count(ch => ch == c));
            score -= Math.Min(specialCharCount * 3, 20); // 最多扣20分

            // 7. 编码特定加分
            if ((encodingName.Contains("GB") || encodingName.Contains("GBK")) && chineseCount > 0)
            {
                score += 5; // GB系列编码处理中文更好
            }
            else if (encodingName.Contains("UTF-8") && !ContainsGarbledText(text))
            {
                score += 3; // UTF-8通用性好
            }

            return Math.Max(0, Math.Min(100, score)); // 限制在0-100范围
        }

        /// <summary>
        /// 统计字符串中的中文字符数量（增强版）
        /// </summary>
        private static int CountChineseCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            foreach (char c in text)
            {
                // 扩展中文字符范围检测
                if ((c >= 0x4E00 && c <= 0x9FFF) ||   // CJK统一汉字
                    (c >= 0x3400 && c <= 0x4DBF) ||   // CJK扩展A
                    (c >= 0x20000 && c <= 0x2A6DF) || // CJK扩展B
                    (c >= 0x2A700 && c <= 0x2B73F) || // CJK扩展C
                    (c >= 0x2B740 && c <= 0x2B81F) || // CJK扩展D
                    (c >= 0x2B820 && c <= 0x2CEAF) || // CJK扩展E
                    (c >= 0xF900 && c <= 0xFAFF) ||   // CJK兼容汉字
                    (c >= 0x2F800 && c <= 0x2FA1F) || // CJK兼容汉字补充
                    (c >= 0x3000 && c <= 0x303F) ||   // CJK符号和标点
                    (c >= 0xFF00 && c <= 0xFFEF))     // 全角ASCII、全角标点
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 检测字符串是否包含乱码
        /// </summary>
        private static bool ContainsGarbledText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // 1. 检查Unicode替换字符
            if (text.Contains('\uFFFD'))
                return true;

            // 2. 检查常见乱码字符
            var garbledChars = new[] { '�', '锟', '烫', '屯', '鋟', '斤', '拷' };
            if (garbledChars.Any(c => text.Contains(c)))
                return true;

            // 3. 检查乱码模式
            var garbledPatterns = new[] { "锟斤拷", "烫烫烫", "屯屯屯", "��" };
            if (garbledPatterns.Any(pattern => text.Contains(pattern)))
                return true;

            // 4. 检查问号比例（可能是无法显示的字符）
            var questionMarkCount = text.Count(c => c == '?');
            if (questionMarkCount > text.Length * 0.15) // 超过15%是问号
                return true;

            // 5. 检查连续的控制字符
            int consecutiveControlChars = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsControl(text[i]) && text[i] != '\r' && text[i] != '\n' && text[i] != '\t')
                {
                    consecutiveControlChars++;
                    if (consecutiveControlChars > 3)
                        return true;
                }
                else
                {
                    consecutiveControlChars = 0;
                }
            }

            return false;
        }

        /// <summary>
        /// 解析题目类型
        /// </summary>
        private static TheoryQuestionType ParseQuestionType(string typeCode)
        {
            return typeCode.ToUpper() switch
            {
                "B" => TheoryQuestionType.SingleChoice,      // B表示单选题
                "C" => TheoryQuestionType.SingleChoice,    // C表示判断题（也是单选题，有两个选项：正确/错误）
                "G" => TheoryQuestionType.MultipleChoice,  // G表示多选题
                "B（单选题）" => TheoryQuestionType.SingleChoice,
                "C（判断题）" => TheoryQuestionType.SingleChoice, // 判断题本质是单选题
                "G（多选题）" => TheoryQuestionType.MultipleChoice,
                _ => TheoryQuestionType.SingleChoice
            };
        }

        /// <summary>
        /// 根据题目编号解析题目分类
        /// </summary>
        private static TheoryQuestionCategory ParseQuestionCategory(string questionId)
        {
            if (string.IsNullOrEmpty(questionId))
                return TheoryQuestionCategory.FlightPrinciples;

            // 根据题目编号的第一部分判断分类
            if (questionId.StartsWith("A-A-A"))
                return TheoryQuestionCategory.FlightSafety;      // 职业道德与安全
            else if (questionId.StartsWith("A-A-B"))
                return TheoryQuestionCategory.FlightSafety;      // 安全管理
            else if (questionId.StartsWith("A-B-A"))
                return TheoryQuestionCategory.FlightPrinciples;  // 飞行原理
            else if (questionId.StartsWith("A-B-B"))
                return TheoryQuestionCategory.Structure;         // 机械结构
            else if (questionId.StartsWith("A-B-C"))
                return TheoryQuestionCategory.ControlAlgorithm;  // 电子电路
            else if (questionId.StartsWith("A-B-D"))
                return TheoryQuestionCategory.SensorFusion;      // 计算机技术
            else if (questionId.StartsWith("A-B-E"))
                return TheoryQuestionCategory.FlightSafety;      // 安全生产
            else if (questionId.StartsWith("A-B-F"))
                return TheoryQuestionCategory.LawsRegulations;   // 法律法规
            else if (questionId.StartsWith("B-A"))
                return TheoryQuestionCategory.Structure;         // 机械装配
            else
                return TheoryQuestionCategory.FlightPrinciples;
        }

        /// <summary>
        /// 解析难度等级
        /// </summary>
        private static QuestionDifficulty ParseDifficulty(string difficultyCode)
        {
            return difficultyCode switch
            {
                "1（容易）" => QuestionDifficulty.Easy,
                "2（较难）" => QuestionDifficulty.Medium,
                "3（中等）" => QuestionDifficulty.Medium,
                "4（较难）" => QuestionDifficulty.Hard,
                "5（很难）" => QuestionDifficulty.Hard,
                _ when difficultyCode.Contains("容易") => QuestionDifficulty.Easy,
                _ when difficultyCode.Contains("较难") || difficultyCode.Contains("很难") => QuestionDifficulty.Hard,
                _ => QuestionDifficulty.Medium
            };
        }

        #endregion
    }

    /// <summary>
    /// 导入导出结果
    /// </summary>
    public class ImportExportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int ProcessedCount { get; set; }
    }

    /// <summary>
    /// 理论题库导出数据格式
    /// </summary>
    public class TheoryQuestionExportData
    {
        public DateTime ExportTime { get; set; } = DateTime.Now;
        public string ExportVersion { get; set; } = "1.0";
        public int TotalQuestions { get; set; }
        public string Description { get; set; } = "DroneSimulator理论题库导出";
        public List<TheoryQuestion> Questions { get; set; } = new();
    }
}