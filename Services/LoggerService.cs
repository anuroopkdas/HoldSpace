using System;
using System.IO;
using System.Text.RegularExpressions;

namespace HoldSpace.Services
{
    public static class LoggerService
    {
        private static readonly string LogDir = "";
        private static readonly string LogFile = "";
        private static readonly object LockObj = new object();
        private const long MaxLogSize = 1 * 1024 * 1024; // 1 MB

        static LoggerService()
        {
            try
            {
                LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HoldSpace", "Logs");
                LogFile = Path.Combine(LogDir, "app.log");
            }
            catch
            {
                // Fallback
            }
        }

        public static void Log(string level, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(LogDir)) return;

                lock (LockObj)
                {
                    if (!Directory.Exists(LogDir))
                    {
                        Directory.CreateDirectory(LogDir);
                    }

                    FileInfo fileInfo = new FileInfo(LogFile);
                    if (fileInfo.Exists && fileInfo.Length > MaxLogSize)
                    {
                        string archiveFile = Path.Combine(LogDir, $"app_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
                        File.Move(LogFile, archiveFile);
                        
                        var files = Directory.GetFiles(LogDir, "app_*.log");
                        if (files.Length > 3)
                        {
                            Array.Sort(files);
                            for (int i = 0; i < files.Length - 3; i++)
                            {
                                File.Delete(files[i]);
                            }
                        }
                    }

                    string sanitized = SanitizeMessage(message);
                    string logLine = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {sanitized}{Environment.NewLine}";
                    File.AppendAllText(LogFile, logLine);
                }
            }
            catch
            {
                // Do not crash
            }
        }

        public static string SanitizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;

            try
            {
                // Redact query parameters in URLs
                message = Regex.Replace(message, @"(https?://[^\s?/]+)(/[^\s?]*)?(\?[^\s]*)?", m =>
                {
                    string host = m.Groups[1].Value;
                    string path = m.Groups[2].Value;
                    string query = m.Groups[3].Value;
                    if (!string.IsNullOrEmpty(query))
                    {
                        return host + path + "?[REDACTED]";
                    }
                    return m.Value;
                });
            }
            catch
            {
                // Ignore regex failures
            }

            return message;
        }

        public static void Info(string message) => Log("INFO", message);
        public static void Warn(string message) => Log("WARN", message);
        public static void Error(string message, Exception? ex = null)
        {
            string detail = ex != null ? $" | Exception: {ex.GetType().Name} - {ex.Message}" : "";
            Log("ERROR", message + detail);
        }

        public static string GetLogsDirectory() => LogDir;
        public static string GetLogFilePath() => LogFile;
    }
}
