using System.Globalization;
using System.Resources;

namespace zpX.Localization;

public sealed record LanguageOption(string Code, string DisplayName);

public static class Loc
{
    private static readonly ResourceManager Resources =
        new("zpX.Localization.Resources.Strings", typeof(Loc).Assembly);

    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English"),
        new("tr", "Türkçe"),
        new("es", "Español"),
        new("fr", "Français"),
        new("de", "Deutsch"),
        new("it", "Italiano"),
        new("pt", "Português"),
        new("ru", "Русский"),
        new("ja", "日本語"),
        new("zh", "中文")
    ];

    public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

    public static string CurrentLanguage { get; private set; } = "en";

    public static void SetLanguage(string code)
    {
        var supported = Languages.Any(l => l.Code == code) ? code : "en";
        CurrentLanguage = supported;
        Culture = new CultureInfo(supported);
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }

    public static string T(string key)
    {
        try
        {
            return Resources.GetString(key, Culture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    public static string Format(string key, params object[] args)
    {
        try
        {
            return string.Format(Culture, T(key), args);
        }
        catch
        {
            return T(key);
        }
    }
}
