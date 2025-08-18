using System;
using System.Windows;
using ModernWpf;

namespace DuplicateFileFinderWPF
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 应用现代主题
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            
            base.OnStartup(e);
        }
    }
}
