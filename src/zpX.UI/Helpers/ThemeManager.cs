using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace zpX.UI.Helpers;

/// <summary>Swaps the Dark/Light resource dictionaries at runtime.</summary>
public static class ThemeManager
{
    private const string AccentKey = "AccentBrush";
    private const string AccentHoverKey = "AccentHoverBrush";
    private const string AccentPressedKey = "AccentPressedBrush";
    private const string XAccentKey = "XAccentBrush";

    private static string _accent = "#3390EC";

    public static string CurrentTheme { get; private set; } = "Dark";

    public static bool IsDark => string.Equals(CurrentTheme, "Dark", StringComparison.OrdinalIgnoreCase);

    public static event EventHandler? ThemeChanged;

    public static void Apply(string theme)
    {
        CurrentTheme = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";

        var app = Application.Current;
        if (app is null) return;

        var dictionaries = app.Resources.MergedDictionaries;
        var replacement = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/zpX;component/Themes/{CurrentTheme}.xaml", UriKind.Absolute)
        };

        var existing = dictionaries.FirstOrDefault(d => IsThemeDictionary(d));
        if (existing is not null)
        {
            var index = dictionaries.IndexOf(existing);
            dictionaries[index] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }

        SetAccent(_accent);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void Toggle()
    {
        var next = IsDark ? "Light" : "Dark";
        Apply(next);

        AppSettings.Current.Theme = next;
        AppSettings.Current.Save();
    }

    public static void SetAccent(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) hex = "#3390EC";
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            _accent = hex;

            var resources = Application.Current?.Resources;
            if (resources is null) return;

            resources[AccentKey] = Frozen(color);
            resources[AccentHoverKey] = Frozen(Blend(color, Colors.White, IsDark ? 0.16 : 0.10));
            resources[AccentPressedKey] = Frozen(Blend(color, Colors.Black, 0.18));
            resources[XAccentKey] = Frozen(color);
        }
        catch
        {
            // Ignore malformed accent values and keep the current palette.
        }
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString ?? string.Empty;
        return source.Contains("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)
               || source.Contains("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase);
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        byte Channel(byte a, byte b) => (byte)Math.Clamp((int)Math.Round(a + (b - a) * amount), 0, 255);
        return Color.FromArgb(255, Channel(from.R, to.R), Channel(from.G, to.G), Channel(from.B, to.B));
    }
}
