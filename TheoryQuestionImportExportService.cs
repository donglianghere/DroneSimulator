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
        /// 从JSON文件导入理论题库
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="overwriteExisting">是否覆盖已存在的题目</param>
        /// <returns>导入结果</returns>
        public static ImportExportResult ImportFromJson(string filePath, bool overwriteExisting = false)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "文件不存在",
                        ProcessedCount = 0
                    };
                }

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

                // 验证和导入题目
                int successCount = 0;
                int skipCount = 0;
                var errors = new List<string>();

                foreach (var question in questions)
                {
                    try
                    {
                        // 验证题目数据
                        if (!question.IsValid())
                        {
                            errors.Add($"题目 '{question.QuestionStatement}' 数据不完整，已跳过");
                            skipCount++;
                            continue;
                        }

                        // 检查是否已存在
                        var existingQuestion = TheoryQuestionBankManager.GetQuestionById(question.Id);
                        if (existingQuestion != null && !overwriteExisting)
                        {
                            errors.Add($"题目 '{question.QuestionStatement}' 已存在，已跳过");
                            skipCount++;
                            continue;
                        }

                        // 设置导入时间
                        question.LastModified = DateTime.Now;
                        if (string.IsNullOrEmpty(question.CreatedBy))
                        {
                            question.CreatedBy = "导入";
                        }

                        // 保存题目
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

                var message = $"导入完成：成功 {successCount} 题，跳过 {skipCount} 题";
                if (errors.Any())
                {
                    message += $"\n详细错误：\n{string.Join("\n", errors.Take(10))}";
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
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"JSON导入失败：{ex.Message}",
                    ProcessedCount = 0
                };
            }
        }

        #endregion

        #region Excel 格式导入导出

        /// <summary>
        /// 导出理论题库到Excel文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="questions">要导出的题目列表，为空时导出全部</param>
        /// <returns>导出结果</returns>
        public static ImportExportResult ExportToExcel(string filePath, List<TheoryQuestion>? questions = null)
        {
            try
            {
                // 注意：这需要安装 EPPlus NuGet 包
                // 暂时返回不支持的消息
                return new ImportExportResult
                {
                    Success = false,
                    Message = "Excel导出功能需要安装EPPlus包，当前版本暂不支持",
                    ProcessedCount = 0
                };
            }
            catch (Exception ex)
            {
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"Excel导出失败：{ex.Message}",
                    ProcessedCount = 0
                };
            }
        }

        /// <summary>
        /// 从Excel文件导入理论题库
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="overwriteExisting">是否覆盖已存在的题目</param>
        /// <returns>导入结果</returns>
        public static ImportExportResult ImportFromExcel(string filePath, bool overwriteExisting = false)
        {
            try
            {
                // 注意：这需要安装 EPPlus NuGet 包
                // 暂时返回不支持的消息
                return new ImportExportResult
                {
                    Success = false,
                    Message = "Excel导入功能需要安装EPPlus包，当前版本暂不支持",
                    ProcessedCount = 0
                };
            }
            catch (Exception ex)
            {
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"Excel导入失败：{ex.Message}",
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
                writer.WriteLine("题目ID,题目陈述,题目类型,题目分类,难度等级,分值,是否启用,选项A,选项B,选项C,选项D,选项E,选项F,正确答案,题目解析,出题人,创建时间,最后修改时间");

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
        /// 从CSV文件导入理论题库
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="overwriteExisting">是否覆盖已存在的题目</param>
        /// <returns>导入结果</returns>
        public static ImportExportResult ImportFromCsv(string filePath, bool overwriteExisting = false)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "文件不存在",
                        ProcessedCount = 0
                    };
                }

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

                int successCount = 0;
                int skipCount = 0;
                var errors = new List<string>();

                // 跳过标题行，从第二行开始处理
                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var fields = ParseCsvLine(lines[i]);
                        if (fields.Count < 8) // 最少需要基本字段
                        {
                            errors.Add($"第 {i + 1} 行：字段数量不足");
                            skipCount++;
                            continue;
                        }

                        var question = new TheoryQuestion();

                        // 解析基本字段
                        question.Id = string.IsNullOrWhiteSpace(fields[0]) ? Guid.NewGuid().ToString() : fields[0];
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
                            question.CreatedBy = string.IsNullOrWhiteSpace(fields[15]) ? "CSV导入" : fields[15];
                        }
                        else
                        {
                            question.CreatedBy = "CSV导入";
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

                        // 检查是否已存在
                        var existingQuestion = TheoryQuestionBankManager.GetQuestionById(question.Id);
                        if (existingQuestion != null && !overwriteExisting)
                        {
                            errors.Add($"第 {i + 1} 行：题目已存在，已跳过");
                            skipCount++;
                            continue;
                        }

                        // 保存题目
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

                var message = $"CSV导入完成：成功 {successCount} 题，跳过 {skipCount} 题";
                if (errors.Any())
                {
                    message += $"\n详细错误：\n{string.Join("\n", errors.Take(10))}";
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
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"CSV导入失败：{ex.Message}",
                    ProcessedCount = 0
                };
            }
        }

        /// <summary>
        /// 从 TQ4.csv 格式导入理论题库（改进版）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="overwriteExisting">是否覆盖已存在的题目</param>
        /// <returns>导入结果</returns>
        public static ImportExportResult ImportFromTQ4Csv(string filePath, bool overwriteExisting = false)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new ImportExportResult
                    {
                        Success = false,
                        Message = "文件不存在",
                        ProcessedCount = 0
                    };
                }

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

                int successCount = 0;
                int skipCount = 0;
                var errors = new List<string>();
                var detailedErrors = new List<string>();

                // 从第2行开始处理数据（第1行是标题）
                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var line = lines[i].Trim();

                        // 跳过空行
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var fields = ParseCsvLine(line);

                        // 调试信息：记录字段数量
                        if (fields.Count < 15)
                        {
                            detailedErrors.Add($"第 {i + 1} 行：字段数量为 {fields.Count}，少于15个字段");

                            // 如果字段太少，尝试补全
                            while (fields.Count < 25) // 确保有足够的字段
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

                        // 检查题目编号是否为空（第2列，索引1）
                        if (fields.Count <= 1 || string.IsNullOrWhiteSpace(fields[1]))
                        {
                            detailedErrors.Add($"第 {i + 1} 行：题目编号为空，跳过");
                            skipCount++;
                            continue;
                        }

                        var question = new TheoryQuestion();

                        // 解析题目ID
                        question.Id = fields[1].Trim();

                        // 解析题目陈述
                        question.QuestionStatement = fields[4].Trim();

                        // 解析题目类型（第3列，索引2）
                        var typeCode = fields.Count > 2 ? fields[2].Trim() : "B";
                        question.Type = ParseQuestionType(typeCode);

                        // 解析题目分类（根据题目编号推断）
                        question.Category = ParseQuestionCategory(question.Id);

                        // 解析难度（第14列，索引13）
                        var difficultyCode = fields.Count > 13 ? fields[13].Trim() : "3（中等）";
                        question.Difficulty = ParseDifficulty(difficultyCode);

                        // 设置默认分值
                        question.Points = question.Type == TheoryQuestionType.MultipleChoice ? 3 : 2;

                        // 解析选项（字段6-11对应选项A-F，索引5-10）
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

                        // 解析正确答案（第13列，索引12）
                        var correctAnswer = fields.Count > 12 ? fields[12].Trim() : "A";
                        question.CorrectAnswers = ConvertAnswerLettersToText(correctAnswer, question);

                        // 如果没有正确答案，设置默认值
                        if (!question.CorrectAnswers.Any() && question.Options.Any())
                        {
                            question.CorrectAnswers.Add(question.Options[0].Text);
                        }

                        // 设置默认值
                        question.Explanation = fields.Count > 14 ? fields[14] : "";
                        question.CreatedBy = "TQ4.csv导入";
                        question.CreatedTime = DateTime.Now;
                        question.LastModified = DateTime.Now;
                        question.LastModifiedBy = "系统导入";
                        question.IsActive = true;

                        // 验证题目（使用更宽松的验证）
                        if (!IsValidTQ4Question(question))
                        {
                            detailedErrors.Add($"第 {i + 1} 行：题目验证失败 - ID: {question.Id}, 陈述: {question.QuestionStatement.Take(20)}...");
                            skipCount++;
                            continue;
                        }

                        // 检查是否已存在
                        var existingQuestion = TheoryQuestionBankManager.GetQuestionById(question.Id);
                        if (existingQuestion != null && !overwriteExisting)
                        {
                            detailedErrors.Add($"第 {i + 1} 行：题目已存在，已跳过 - {question.Id}");
                            skipCount++;
                            continue;
                        }

                        // 保存题目
                        if (TheoryQuestionBankManager.SaveQuestion(question))
                        {
                            successCount++;
                        }
                        else
                        {
                            detailedErrors.Add($"第 {i + 1} 行：保存题目失败 - {question.Id}");
                            skipCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        detailedErrors.Add($"第 {i + 1} 行：处理出错 - {ex.Message}");
                        skipCount++;
                    }
                }

                // 构建详细的返回消息
                var message = $"TQ4.csv导入完成：\n";
                message += $"成功导入：{successCount} 题\n";
                message += $"跳过/失败：{skipCount} 题\n";
                message += $"总处理行数：{lines.Length - 1} 行\n";

                if (detailedErrors.Any())
                {
                    message += $"\n详细信息（前20条）：\n";
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
                return new ImportExportResult
                {
                    Success = false,
                    Message = $"TQ4.csv导入失败：{ex.Message}\n\n堆栈跟踪：{ex.StackTrace}",
                    ProcessedCount = 0
                };
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
        /// 智能检测文件编码并读取文件（增强版，支持.NET Core/.NET 5+）
        /// </summary>
        private static string[] ReadFileWithCorrectEncoding(string filePath)
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
        /// 检测文件编码
        /// </summary>
        public static string DetectFileEncoding(string filePath)
        {
            try
            {
                var encodingsToTry = new[]
                {
            ("GB2312", Encoding.GetEncoding("GB2312")),
            ("GBK", Encoding.GetEncoding("GBK")),
            ("GB18030", Encoding.GetEncoding("GB18030")),
            ("UTF-8", Encoding.UTF8),
            ("Big5", Encoding.GetEncoding("Big5")),
            ("系统默认", Encoding.Default)
        };

                var results = new List<(string Name, int ChineseCount, string Sample)>();

                foreach (var (name, encoding) in encodingsToTry)
                {
                    try
                    {
                        var lines = File.ReadAllLines(filePath, encoding).Take(5).ToArray();
                        var sample = string.Join(" ", lines);
                        var chineseCount = CountChineseCharacters(sample);
                        var containsGarbled = ContainsGarbledText(sample);

                        if (!containsGarbled)
                        {
                            results.Add((name, chineseCount, sample.Length > 100 ? sample.Substring(0, 100) + "..." : sample));
                        }
                    }
                    catch
                    {
                        // 忽略编码错误
                    }
                }

                // 返回检测结果
                var bestMatch = results.OrderByDescending(r => r.ChineseCount).FirstOrDefault();
                return bestMatch.Name ?? "未知编码";
            }
            catch (Exception ex)
            {
                return $"检测失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 将答案字母转换为选项文本
        /// </summary>
        private static List<string> ConvertAnswerLettersToText(string correctAnswer, TheoryQuestion question)
        {
            var answers = new List<string>();

            if (string.IsNullOrWhiteSpace(correctAnswer) || !question.Options.Any())
                return answers;

            correctAnswer = correctAnswer.Trim();

            // 对于判断题的特殊处理
            if (question.Type == TheoryQuestionType.MultipleChoice &&
                question.Options.Count == 2 &&
                (correctAnswer == "A" || correctAnswer == "B"))
            {
                if (correctAnswer == "A")
                    answers.Add(question.Options[0].Text); // 通常是"正确"
                else if (correctAnswer == "B")
                    answers.Add(question.Options[1].Text); // 通常是"错误"
                return answers;
            }

            // 解析答案字母（A、B、C等）
            var answerChars = correctAnswer.ToCharArray()
                .Where(c => c >= 'A' && c <= 'F')
                .ToList();

            foreach (var answerChar in answerChars)
            {
                int index = answerChar - 'A'; // A=0, B=1, C=2...
                if (index >= 0 && index < question.Options.Count)
                {
                    answers.Add(question.Options[index].Text);
                }
            }

            // 如果无法解析，使用第一个选项作为默认答案
            if (!answers.Any() && question.Options.Any())
            {
                answers.Add(question.Options[0].Text);
            }

            return answers;
        }

        /// <summary>
        /// 针对TQ4格式的宽松题目验证
        /// </summary>
        private static bool IsValidTQ4Question(TheoryQuestion question)
        {
            // 基本验证
            if (string.IsNullOrWhiteSpace(question.QuestionStatement))
                return false;

            if (string.IsNullOrWhiteSpace(question.Id))
                return false;

            // 选项验证（更宽松）
            if (!question.Options.Any())
                return false;

            // 需要有正确答案
            if (question.CorrectAnswers == null || !question.CorrectAnswers.Any())
                return false;

            // 验证正确答案是否在选项中
            var optionTexts = question.Options.Select(o => o.Text).ToList();
            foreach (var answer in question.CorrectAnswers)
            {
                if (!optionTexts.Contains(answer))
                    return false;
            }

            return true;
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
        /// 创建示例题目用于测试
        /// </summary>
        public static List<TheoryQuestion> CreateSampleQuestions()
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
        /// 解析题目类型
        /// </summary>
        private static TheoryQuestionType ParseQuestionType(string typeCode)
        {
            return typeCode.ToUpper() switch
            {
                "B" => TheoryQuestionType.SingleChoice,      // B表示单选题
                "C" => TheoryQuestionType.MultipleChoice,    // C表示判断题（作为多选题处理）
                "B（单选题）" => TheoryQuestionType.SingleChoice,
                "C（判断题）" => TheoryQuestionType.MultipleChoice,
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

        /// <summary>
        /// 解析正确答案
        /// </summary>
        private static List<string> ParseCorrectAnswers(string correctAnswer, TheoryQuestionType questionType)
        {
            var answers = new List<string>();

            if (string.IsNullOrWhiteSpace(correctAnswer))
                return answers;

            correctAnswer = correctAnswer.Trim();

            // 对于判断题
            if (questionType == TheoryQuestionType.MultipleChoice &&
                (correctAnswer == "A" || correctAnswer == "B"))
            {
                // A通常表示"正确"，B通常表示"错误"
                answers.Add(correctAnswer);
                return answers;
            }

            // 对于选择题，解析答案选项
            var answerChars = correctAnswer.ToCharArray()
                .Where(c => c >= 'A' && c <= 'F')
                .Select(c => c.ToString())
                .ToList();

            if (answerChars.Any())
            {
                answers.AddRange(answerChars);
            }
            else
            {
                // 如果无法解析，尝试直接使用原始答案
                answers.Add(correctAnswer);
            }

            return answers;
        }

        /// <summary>
        /// 验证题目是否有效
        /// </summary>
        private static bool IsValidQuestion(TheoryQuestion question)
        {
            // 基本验证
            if (string.IsNullOrWhiteSpace(question.QuestionStatement))
                return false;

            if (string.IsNullOrWhiteSpace(question.Id))
                return false;

            // 对于单选题，至少需要1个选项（可以是判断题）
            if (question.Type == TheoryQuestionType.SingleChoice)
            {
                if (question.Options.Count < 1)
                    return false;
            }

            // 需要有正确答案
            if (question.CorrectAnswers == null || !question.CorrectAnswers.Any())
                return false;

            return true;
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