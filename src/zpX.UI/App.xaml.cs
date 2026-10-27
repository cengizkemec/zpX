using System;
using System.Windows;
using zpX.Localization;
using zpX.UI.Helpers;

namespace zpX.UI;

public partial class App : Application
{
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        StartupArgs = e.Args.Length > 0 ? e.Args : Environment.GetCommandLineArgs().Skip(1).ToArray();

        var settings = AppSettings.Load();
        ThemeManager.Apply(settings.Theme);
        ThemeManager.SetAccent(settings.Accent);
        Loc.SetLanguage(settings.Language);

        if (settings.ShellIntegration)
            ContextMenuRegistrar.Register();

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
