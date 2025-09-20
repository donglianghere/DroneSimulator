using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DroneSimulator
{
    public partial class FCImportProgressWindow : Window
    {
        private string _importFilePath = "";
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _importCompleted = false;
        private DateTime _startTime;
        private DispatcherTimer? _timer;
        private int _totalRows = 0;
        private int _processedRows = 0;

        public FCImportProgressWindow()
        {
            InitializeComponent();
            InitializeTimer();

            // 禁止用户关闭窗口（除非导入完成或取消）
            this.Closing += Window_Closing;
        }

        private void InitializeTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_startTime != default)
            {
                var elapsed = DateTime.Now - _startTime;
                // 可以在此处更新时间显示
            }
        }

        /// <summary>
        /// 开始导入操作
        /// </summary>
        public void StartImport(string filePath)
        {
            _importFilePath = filePath;

            // 显示文件信息
            FilePathText.Text = filePath;

            try
            {
                var fileInfo = new FileInfo(filePath);
                FileSizeText.Text = FormatFileSize(fileInfo.Length);

                // 预估行数
                var lines = File.ReadAllLines(filePath);
                _totalRows = Math.Max(0, lines.Length - 1); // 减去表头
                TotalRowsText.Text = _totalRows.ToString();
            }
            catch (Exception ex)
            {
                FileSizeText.Text = "无法获取";
                AddLog($"❌ 读取文件信息失败: {ex.Message}", LogLevel.Error);
            }

            // 🔧 新增：显示ID分配说明
            AddLog("ℹ️ 重要提示：导入时将忽略CSV文件中的题目ID，系统将自动为每道题目分配新的唯一ID", LogLevel.Info);
            AddLog("", LogLevel.Info);
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            return $"{bytes / (1024 * 1024 * 1024):F1} GB";
        }

        private async void StartImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_importFilePath))
            {
                MessageBox.Show("请先选择要导入的文件！", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 禁用开始按钮，启用取消按钮
            StartImportButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            CloseButton.IsEnabled = false;

            // 初始化状态
            _startTime = DateTime.Now;
            _timer?.Start();

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                AddLog("🚀 开始导入飞控实操题库...", LogLevel.Info);
                AddLog($"📁 文件路径: {_importFilePath}", LogLevel.Info);
                AddLog($"⚙️ 覆盖现有题目: {(OverwriteExistingCheckBox.IsChecked == true ? "是" : "否")}", LogLevel.Info);
                AddLog($"⚙️ 跳过无效行: {(SkipInvalidRowsCheckBox.IsChecked == true ? "是" : "否")}", LogLevel.Info);
                AddLog("", LogLevel.Info);

                CurrentStatusText.Text = "正在读取CSV文件...";

                // 执行导入
                var result = await ImportWithProgressAsync(_cancellationTokenSource.Token);

                // 导入完成
                _importCompleted = true;
                _timer?.Stop();

                if (result.Success)
                {
                    AddLog("", LogLevel.Info);
                    AddLog("✅ 导入完成！", LogLevel.Success);
                    AddLog($"📊 导入结果: {result.Message}", LogLevel.Success);

                    CurrentStatusText.Text = "导入成功完成";
                    OverallProgressBar.Value = 100;
                    OverallProgressText.Text = "100%";

                    // 显示结果对话框
                    MessageBox.Show(result.Message, "导入完成",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    AddLog("", LogLevel.Info);
                    AddLog("❌ 导入失败！", LogLevel.Error);
                    AddLog($"❌ 错误信息: {result.Message}", LogLevel.Error);

                    CurrentStatusText.Text = "导入失败";

                    MessageBox.Show($"导入失败：\n{result.Message}", "导入失败",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                AddLog("", LogLevel.Info);
                AddLog("🛑 导入已被用户取消", LogLevel.Warning);
                CurrentStatusText.Text = "导入已取消";
            }
            catch (Exception ex)
            {
                AddLog("", LogLevel.Info);
                AddLog($"💥 导入过程中发生异常: {ex.Message}", LogLevel.Error);
                CurrentStatusText.Text = "导入异常";

                MessageBox.Show($"导入过程中发生错误：\n{ex.Message}", "导入错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                StartImportButton.IsEnabled = true;
                CancelButton.IsEnabled = false;
                CloseButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 🔧 修改：执行导入操作 - 支持JSON和CSV格式
        /// </summary>
        private async Task<FCImportExportResult> ImportWithProgressAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    bool overwriteExisting = false;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        overwriteExisting = OverwriteExistingCheckBox.IsChecked == true;
                    });

                    // 🔧 新增：检测文件格式
                    string fileExtension = Path.GetExtension(_importFilePath).ToLower();

                    if (fileExtension == ".json")
                    {
                        // JSON 导入
                        await AddLogAsync("🔄 开始解析JSON文件...", LogLevel.Info);
                        await AddLogAsync("📋 系统将自动为每道题目分配新的ID", LogLevel.Info);

                        // 🚀 使用带进度回调的JSON导入方法
                        var result = await ImportFromJSONWithProgress(_importFilePath, overwriteExisting, cancellationToken);

                        // 更新UI进度
                        await Dispatcher.InvokeAsync(() =>
                        {
                            OverallProgressBar.Value = 100;
                            OverallProgressText.Text = "100%";

                            if (result.Success)
                            {
                                SuccessCountText.Text = result.SuccessCount.ToString();
                                SkipOverwriteCountText.Text = (result.OverwriteCount + result.SkipCount).ToString();
                                FailCountText.Text = result.FailCount.ToString();
                            }
                        });

                        return result;
                    }
                    else if (fileExtension == ".csv")
                    {
                        // CSV 导入（保持原有逻辑）
                        await AddLogAsync("🔄 开始解析CSV文件...", LogLevel.Info);
                        await AddLogAsync("📋 系统将自动为每道题目分配新的ID", LogLevel.Info);

                        var result = FlightControlQuestionBankManager.ImportFromCSV(_importFilePath, overwriteExisting);

                        // 更新UI进度
                        await Dispatcher.InvokeAsync(() =>
                        {
                            OverallProgressBar.Value = 100;
                            OverallProgressText.Text = "100%";

                            if (result.Success)
                            {
                                SuccessCountText.Text = result.SuccessCount.ToString();
                                SkipOverwriteCountText.Text = (result.OverwriteCount + result.SkipCount).ToString();
                                FailCountText.Text = result.FailCount.ToString();
                            }
                        });

                        return result;
                    }
                    else
                    {
                        return new FCImportExportResult
                        {
                            Success = false,
                            Message = "不支持的文件格式。请选择 JSON 或 CSV 文件。"
                        };
                    }
                }
                catch (Exception ex)
                {
                    return new FCImportExportResult
                    {
                        Success = false,
                        Message = $"导入失败: {ex.Message}"
                    };
                }
            });
        }
        /// <summary>
        /// 🚀 新增：带进度回调的JSON导入方法
        /// </summary>
        private async Task<FCImportExportResult> ImportFromJSONWithProgress(string filePath, bool overwriteExisting, CancellationToken cancellationToken)
        {
            // 进度回调委托
            Action<double, string, string> progressCallback = (progress, message, detail) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    OverallProgressBar.Value = progress;
                    OverallProgressText.Text = $"{progress:F1}%";
                    CurrentStatusText.Text = message;

                    if (!string.IsNullOrEmpty(detail))
                    {
                        AddLog(detail, LogLevel.Info);
                    }
                });
            };

            // 调用带进度回调的导入方法
            return await Task.Run(() =>
                FlightControlQuestionBankManager.ImportFromJSONWithProgress(filePath, overwriteExisting, progressCallback, cancellationToken));
        }

        private enum LogLevel
        {
            Info,
            Success,
            Warning,
            Error
        }

        /// <summary>
        /// 异步添加日志（线程安全）
        /// </summary>
        private async Task AddLogAsync(string message, LogLevel level)
        {
            await Dispatcher.InvokeAsync(() => AddLog(message, level));
        }

        private void AddLog(string message, LogLevel level)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = level switch
            {
                LogLevel.Success => "✅",
                LogLevel.Warning => "⚠️",
                LogLevel.Error => "❌",
                _ => "ℹ️"
            };

            var logMessage = string.IsNullOrEmpty(message) ? "\n" : $"[{timestamp}] {prefix} {message}\n";

            LogTextBlock.Text += logMessage;

            // 自动滚动到底部
            if (AutoScrollCheckBox.IsChecked == true)
            {
                LogScrollViewer.ScrollToEnd();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                var result = MessageBox.Show("确定要取消导入吗？\n已导入的数据将会保留。",
                    "确认取消", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _cancellationTokenSource.Cancel();
                    AddLog("🛑 用户请求取消导入...", LogLevel.Warning);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Text = "";
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // 如果导入正在进行中，阻止关闭窗口
            if (_cancellationTokenSource != null &&
                !_cancellationTokenSource.IsCancellationRequested &&
                !_importCompleted)
            {
                var result = MessageBox.Show("导入正在进行中，确定要关闭窗口吗？\n这将取消导入操作。",
                    "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _cancellationTokenSource.Cancel();
            }

            _timer?.Stop();
        }
    }
}