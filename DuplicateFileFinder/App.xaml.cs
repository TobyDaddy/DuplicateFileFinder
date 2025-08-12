using System;
using System.Windows;
using ModernWpf;

namespace DuplicateFileFinderWPF
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 根据用户设置应用主题（System/Light/Dark）
            try
            {
                var pref = DuplicateFileFinderWPF.Properties.Settings.Default.Theme;
                if (string.Equals(pref, "Light", StringComparison.OrdinalIgnoreCase))
                {
                    ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                }
                else if (string.Equals(pref, "Dark", StringComparison.OrdinalIgnoreCase))
                {
                    ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                }
                else
                {
                    // 跟随系统
                    ThemeManager.Current.ApplicationTheme = null;
                }
            }
            catch
            {
                ThemeManager.Current.ApplicationTheme = null; // 失败时跟随系统
            }

            base.OnStartup(e);
        }
    }
}
