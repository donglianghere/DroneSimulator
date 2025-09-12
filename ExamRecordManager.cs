using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DroneSimulator
{
    /// <summary>
    /// 考试记录管理器
    /// </summary>
    public static class ExamRecordManager
    {
        private const string RECORDS_DIRECTORY = "ExamRecords";
        private const string SEQUENCE_FILE = "exam_sequence.json";

        /// <summary>
        /// 保存考试记录
        /// </summary>
        public static bool SaveExamRecord(DetailedExamRecord record)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(RECORDS_DIRECTORY))
                {
                    Directory.CreateDirectory(RECORDS_DIRECTORY);
                }

                // 获取下一个序号
                record.ExamSequence = GetNextSequenceNumber();

                // 生成文件名：考试序号_学生姓名_提交时间.json
                string fileName = $"{record.ExamSequence:D4}_{record.StudentName}_{record.SubmitTime:yyyyMMdd_HHmmss}.json";
                string filePath = Path.Combine(RECORDS_DIRECTORY, fileName);

                // 序列化并保存
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string jsonString = JsonSerializer.Serialize(record, options);
                File.WriteAllText(filePath, jsonString, System.Text.Encoding.UTF8);

                // 更新序号文件
                UpdateSequenceNumber(record.ExamSequence);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存考试记录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有考试记录
        /// </summary>
        public static List<DetailedExamRecord> GetAllExamRecords()
        {
            var records = new List<DetailedExamRecord>();

            try
            {
                if (!Directory.Exists(RECORDS_DIRECTORY))
                    return records;

                var files = Directory.GetFiles(RECORDS_DIRECTORY, "*.json")
                    .Where(f => !f.EndsWith(SEQUENCE_FILE));

                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var record = JsonSerializer.Deserialize<DetailedExamRecord>(json);
                        if (record != null)
                        {
                            records.Add(record);
                        }
                    }
                    catch
                    {
                        // 跳过损坏的文件
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取考试记录失败: {ex.Message}");
            }

            return records.OrderBy(r => r.ExamSequence).ToList();
        }

        /// <summary>
        /// 获取下一个序号
        /// </summary>
        private static int GetNextSequenceNumber()
        {
            try
            {
                string sequenceFilePath = Path.Combine(RECORDS_DIRECTORY, SEQUENCE_FILE);
                if (File.Exists(sequenceFilePath))
                {
                    string json = File.ReadAllText(sequenceFilePath);
                    var sequenceData = JsonSerializer.Deserialize<SequenceData>(json);
                    return sequenceData?.LastSequence + 1 ?? 1;
                }
            }
            catch
            {
                // 如果读取失败，从现有文件中推断
                return GetSequenceFromExistingFiles();
            }

            return 1;
        }

        /// <summary>
        /// 从现有文件推断序号
        /// </summary>
        private static int GetSequenceFromExistingFiles()
        {
            try
            {
                if (!Directory.Exists(RECORDS_DIRECTORY))
                    return 1;

                var files = Directory.GetFiles(RECORDS_DIRECTORY, "*.json")
                    .Where(f => !f.EndsWith(SEQUENCE_FILE))
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => name.Length >= 4 && int.TryParse(name.Substring(0, 4), out _))
                    .Select(name => int.Parse(name.Substring(0, 4)))
                    .ToList();

                return files.Count > 0 ? files.Max() + 1 : 1;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// 更新序号文件
        /// </summary>
        private static void UpdateSequenceNumber(int lastSequence)
        {
            try
            {
                if (!Directory.Exists(RECORDS_DIRECTORY))
                {
                    Directory.CreateDirectory(RECORDS_DIRECTORY);
                }

                string sequenceFilePath = Path.Combine(RECORDS_DIRECTORY, SEQUENCE_FILE);
                var sequenceData = new SequenceData { LastSequence = lastSequence, UpdateTime = DateTime.Now };

                string json = JsonSerializer.Serialize(sequenceData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(sequenceFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新序号文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 序号数据
        /// </summary>
        private class SequenceData
        {
            public int LastSequence { get; set; }
            public DateTime UpdateTime { get; set; }
        }

        // 在 ExamRecordManager 类中添加删除方法
        /// <summary>
        /// 删除指定序号的考试记录
        /// </summary>
        public static bool DeleteExamRecord(int examSequence)
        {
            try
            {
                if (!Directory.Exists(RECORDS_DIRECTORY))
                    return false;

                // 查找匹配序号的文件
                var files = Directory.GetFiles(RECORDS_DIRECTORY, "*.json")
                    .Where(f => !f.EndsWith(SEQUENCE_FILE))
                    .Where(f =>
                    {
                        string fileName = Path.GetFileNameWithoutExtension(f);
                        return fileName.Length >= 4 &&
                               int.TryParse(fileName.Substring(0, 4), out int seq) &&
                               seq == examSequence;
                    })
                    .ToList();

                if (files.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"未找到序号为 {examSequence} 的考试记录文件");
                    return false;
                }

                // 删除找到的文件（理论上应该只有一个）
                foreach (var file in files)
                {
                    File.Delete(file);
                    System.Diagnostics.Debug.WriteLine($"已删除考试记录文件: {file}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除考试记录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量删除考试记录
        /// </summary>
        public static int DeleteExamRecords(List<int> examSequences)
        {
            int deletedCount = 0;
            foreach (var sequence in examSequences)
            {
                if (DeleteExamRecord(sequence))
                {
                    deletedCount++;
                }
            }
            return deletedCount;
        }
    }
}