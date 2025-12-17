using System;
using System.IO;
using TaleWorlds.Library;

namespace QuickStartMod
{
    /// <summary>
    /// 日志辅助类：将日志写入文件并显示在游戏中
    /// </summary>
    public static class QuickStartLogger
    {
        private static string _logFilePath = null;
        private static object _lockObject = new object();

        private static string GetLogFilePath()
        {
            if (_logFilePath == null)
            {
                try
                {
                    // 尝试多个可能的日志目录
                    var possiblePaths = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "Logs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount & Blade II Bannerlord", "Logs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mount and Blade II Bannerlord", "Logs"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mount & Blade II Bannerlord", "Logs"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Modules", "QuickStartMod", "Logs")
                    };

                    foreach (var basePath in possiblePaths)
                    {
                        try
                        {
                            var logDir = Path.GetFullPath(basePath);
                            if (!Directory.Exists(logDir))
                            {
                                Directory.CreateDirectory(logDir);
                            }
                            _logFilePath = Path.Combine(logDir, $"QuickStartMod_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                            break;
                        }
                        catch
                        {
                            // 继续尝试下一个路径
                        }
                    }

                    // 如果所有路径都失败，使用临时文件
                    if (_logFilePath == null)
                    {
                        _logFilePath = Path.Combine(Path.GetTempPath(), $"QuickStartMod_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                    }
                }
                catch
                {
                    _logFilePath = Path.Combine(Path.GetTempPath(), $"QuickStartMod_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                }
            }
            return _logFilePath;
        }

        /// <summary>
        /// 写入日志（同时输出到文件、Debug 和游戏内消息）
        /// </summary>
        public static void Log(string message, bool showInGame = false)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] [QuickStartMod] {message}";

            // 1. 写入文件
            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(GetLogFilePath(), logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // 文件写入失败，继续其他输出方式
            }

            // 2. 输出到 Debug 和控制台（确保能看到）
            System.Diagnostics.Debug.WriteLine(logMessage);
            System.Console.WriteLine(logMessage); // 同时输出到控制台

            // 3. 显示在游戏中（可选）
            if (showInGame)
            {
                try
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[QuickStartMod] {message}", new Color(0.5f, 0.8f, 1.0f)));
                }
                catch
                {
                    // 游戏内消息显示失败，忽略
                }
            }
        }

        /// <summary>
        /// 记录步骤（带步骤编号）
        /// </summary>
        public static void LogStep(int step, string description, bool showInGame = false)
        {
            Log($"【步骤 {step}】{description}", showInGame);
        }

        /// <summary>
        /// 记录错误（带异常信息）
        /// </summary>
        public static void LogError(string step, Exception ex, bool showInGame = true)
        {
            var errorMessage = $"【错误 - {step}】{ex.GetType().Name}: {ex.Message}";
            Log(errorMessage, showInGame);
            Log($"堆栈跟踪: {ex.StackTrace}", false);
            
            if (ex.InnerException != null)
            {
                Log($"内部异常: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", false);
            }
        }

        /// <summary>
        /// 记录成功消息
        /// </summary>
        public static void LogSuccess(string message, bool showInGame = false)
        {
            Log($"✅ {message}", showInGame);
        }

        /// <summary>
        /// 记录警告消息
        /// </summary>
        public static void LogWarning(string message, bool showInGame = false)
        {
            Log($"⚠️ {message}", showInGame);
        }
    }
}

