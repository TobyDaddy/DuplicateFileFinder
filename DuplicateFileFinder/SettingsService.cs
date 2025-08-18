using System.Text.Json;
using System.IO;

namespace DuplicateFileFinderWPF
{
    public static class SettingsService
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DuplicateFileFinderWPF");
        private static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");

        public class AppSettings
        {
            public string? MoveTargetFolder { get; set; }
            public bool EnableDebugLogging { get; set; } = false;
        }

        private static AppSettings _settings = new AppSettings();

        public static string GetDefaultMoveFolder()
        {
            var doc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(doc, "DuplicatedFiles");
        }

        public static string GetMoveTargetFolder()
        {
            var folder = _settings.MoveTargetFolder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = GetDefaultMoveFolder();
            }
            return folder!;
        }

        public static void SetMoveTargetFolder(string folder)
        {
            _settings.MoveTargetFolder = folder;
            Save();
        }

        public static bool GetEnableDebugLogging()
        {
            return _settings.EnableDebugLogging;
        }

        public static void SetEnableDebugLogging(bool enabled)
        {
            _settings.EnableDebugLogging = enabled;
            Save();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null) _settings = loaded;
                }
            }
            catch { /* ignore */ }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* ignore */ }
        }

    }
}