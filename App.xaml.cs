using ModernWpf;
using System.Windows;

namespace LOL_abilities_sound
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
        }
    }
}
