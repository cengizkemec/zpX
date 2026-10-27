using System;
using Microsoft.Win32;
using zpX.Localization;

namespace zpX.UI.Helpers;

public static class ContextMenuRegistrar
{
    private static string ExePath => Environment.ProcessPath ?? string.Empty;

    public static void Register()
    {
        if (string.IsNullOrWhiteSpace(ExePath)) return;

        try
        {
            RegisterVerb(@"Software\Classes\*\shell\zpX", Loc.T("CompressWithZpX"),
                $"\"{ExePath}\" --compress \"%1\"", multiSelect: true);
            RegisterVerb(@"Software\Classes\Directory\shell\zpX", Loc.T("CompressWithZpX"),
                $"\"{ExePath}\" --compress \"%1\"", multiSelect: true);
            RegisterVerb(@"Software\Classes\Directory\Background\shell\zpX", Loc.T("CompressFolderWithZpX"),
                $"\"{ExePath}\" --compress \"%V\"", multiSelect: false);

            RegisterVerb(@"Software\Classes\*\shell\zpX.Extract", Loc.T("ExtractHereWithZpX"),
                $"\"{ExePath}\" --extract-here \"%1\"", multiSelect: false);
        }
        catch { }
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
