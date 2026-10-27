using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace zpX.UI.Helpers;

/// <summary>User preferences persisted in %AppData%\zpX\settings.json.</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#3390EC";
    public string Language { get; set; } = "en";
    public bool ShellIntegration { get; set; } = true;
    public bool ShowHiddenFiles { get; set; } = false;

    public static AppSettings Current { get; private set; } = new();

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "zpX");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null)
                {
                    loaded.Theme = string.Equals(loaded.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
                    loaded.Accent = string.IsNullOrWhiteSpace(loaded.Accent) ? "#3390EC" : loaded.Accent;
                    loaded.Language = NormalizeLanguage(loaded.Language);
                    Current = loaded;
                    return loaded;
                }
            }
        }
        catch
        {
            // A broken settings file must never stop the app from starting.
        }

        Current = new AppSettings
        {
            Theme = "Dark",
            Accent = "#3390EC",
            Language = NormalizeLanguage(string.Empty)
        };
        return Current;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Saving preferences is best-effort.
        }
    }

    private static string NormalizeLanguage(string? code)
    {
        var supported = new[] { "en", "tr", "es", "fr", "de", "it", "pt", "ru", "ja", "zh" };
        if (!string.IsNullOrWhiteSpace(code) &&
            supported.Contains(code, StringComparer.OrdinalIgnoreCase))
            return code.ToLowerInvariant();

        var system = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return supported.Contains(system, StringComparer.OrdinalIgnoreCase) ? system : "en";
    }
}
