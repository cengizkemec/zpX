using System;
using Microsoft.Win32;

namespace zpX.UI.Helpers;

/// <summary>
/// Registers zpX verbs in the current user's Explorer context menus.
/// Uses HKCU so it works without administrator rights.
/// </summary>
public static class ContextMenuRegistrar
{
    private static string ExePath => Environment.ProcessPath ?? string.Empty;

    public static void Register()
    {
        if (string.IsNullOrWhiteSpace(ExePath)) return;

        try
        {
            RegisterVerb(@"Software\Classes\*\shell\zpX", "Compress with zpX",
                $"\"{ExePath}\" --compress \"%1\"", multiSelect: true);
            RegisterVerb(@"Software\Classes\Directory\shell\zpX", "Compress with zpX",
                $"\"{ExePath}\" --compress \"%1\"", multiSelect: true);
            RegisterVerb(@"Software\Classes\Directory\Background\shell\zpX", "Compress this folder with zpX",
                $"\"{ExePath}\" --compress \"%V\"", multiSelect: false);

            RegisterVerb(@"Software\Classes\*\shell\zpX.Extract", "Extract here with zpX",
                $"\"{ExePath}\" --extract-here \"%1\"", multiSelect: false);
        }
        catch
        {
            // Registry access is best-effort; never block startup.
        }
    }

    public static void Unregister()
    {
        try
        {
            DeleteTree(@"Software\Classes\*\shell\zpX");
            DeleteTree(@"Software\Classes\*\shell\zpX.Extract");
            DeleteTree(@"Software\Classes\Directory\shell\zpX");
            DeleteTree(@"Software\Classes\Directory\Background\shell\zpX");
        }
        catch { }
    }

    private static void RegisterVerb(string path, string label, string command, bool multiSelect)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(path))
        {
            key.SetValue("", label);
            key.SetValue("Icon", ExePath);
            if (multiSelect) key.SetValue("MultiSelectModel", "Player");
        }

        using (var commandKey = Registry.CurrentUser.CreateSubKey(path + @"\command"))
        {
            commandKey.SetValue("", command);
        }
    }

    private static void DeleteTree(string path)
    {
        try { Registry.CurrentUser.DeleteSubKeyTree(path, false); } catch { }
    }
}
