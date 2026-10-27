using System.Windows;
using zpX.Localization;
using zpX.UI.Helpers;

namespace zpX.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load();
        ThemeManager.Apply(settings.Theme);
        ThemeManager.SetAccent(settings.Accent);
        Loc.SetLanguage(settings.Language);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
