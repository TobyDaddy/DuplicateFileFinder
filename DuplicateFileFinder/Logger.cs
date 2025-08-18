using System;
using System.IO;
using System.Text;

namespace DuplicateFileFinderWPF
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DuplicateFileFinderWPF");
        private static readonly string LogFile = Path.Combine(AppDataFolder, "app.log");
        private static bool _initialized = false;

        public static void Init()
        {
            try
            {
                if (_initialized) return;
                Directory.CreateDirectory(AppDataFolder);
                // Keep last few logs by rolling daily filename as well
                var today = DateTime.Now.ToString("yyyyMMdd");
                var daily = Path.Combine(AppDataFolder, $"app-{today}.log");
                if (!File.Exists(daily))
                {
                    File.AppendAllText(daily, $"===== New Session {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====\n");
                }
                _initialized = true;
            }
            catch { /* ignore */ }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                if (!SettingsService.GetEnableDebugLogging()) return;
                Init();
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"[{ts}] {level} {message}\n";
                lock (_lock)
                {
                    var today = DateTime.Now.ToString("yyyyMMdd");
                    var daily = Path.Combine(AppDataFolder, $"app-{today}.log");
                    File.AppendAllText(daily, line, Encoding.UTF8);
                }
            }
            catch { /* ignore logging failures */ }
        }
    }
}
