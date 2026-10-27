using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using zpX.Core.Abstractions;
using zpX.Core.Archives;
using zpX.Localization;
using zpX.UI.Helpers;
using zpX.UI.Models;

namespace zpX.UI;

public partial class MainWindow : Window
{
    private readonly List<ArchiveEntry> _entries = new();
    private string? _archivePath;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        UpdateThemeGlyph();
        UpdateSearchPlaceholder();

        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && File.Exists(args[1]))
        {
            _ = OpenArchiveAsync(args[1]);
        }
        else
        {
            UpdateArchiveUi();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        Mouse.OverrideCursor = null;
    }

    private void ThemeManager_ThemeChanged(object? sender, EventArgs e)
    {
        UpdateThemeGlyph();
    }

    private void UpdateThemeGlyph()
    {
        if (ThemeButton != null)
        {
            ThemeButton.Content = ThemeManager.IsDark ? "\uE706" : "\uE708";
            ThemeButton.ToolTip = Loc.T("Theme");
        }
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        AppSettings.Current.Theme = ThemeManager.CurrentTheme;
        AppSettings.Current.Save();
    }

    // ===================== Chrome =====================

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove throws when the mouse button was already released.
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;

        // WindowChrome leaves the resize handle outside the visible area when maximized.
        WindowFrame.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
        WindowFrame.Margin = maximized ? new Thickness(7) : new Thickness(0);
        MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
    }

    // ===================== Language =====================

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateLanguageChecks();
        LanguagePopup.IsOpen = true;
    }

    private void LanguageItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string code }) return;

        Loc.SetLanguage(code);
        AppSettings.Current.Language = code;
        AppSettings.Current.Save();

        LanguagePopup.IsOpen = false;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        NewText.Text = Loc.T("Create");
        OpenText.Text = Loc.T("Open");
        ExtractText.Text = Loc.T("Extract");
        TestText.Text = Loc.T("Test");

        AddFilesText.Text = Loc.T("AddFiles");
        AddFolderText.Text = Loc.T("AddFolder");

        HeaderName.Text = Loc.T("Name");
        HeaderSize.Text = Loc.T("Size");
        HeaderPacked.Text = Loc.T("Packed");
        HeaderType.Text = Loc.T("Type");
        HeaderModified.Text = Loc.T("Modified");

        EmptyTitleText.Text = Loc.T("EmptyTitle");
        EmptyHintText.Text = Loc.T("EmptyHint");
        EmptyOpenText.Text = Loc.T("OpenArchiveTitle");
        DropText.Text = Loc.T("DropToOpen");

        SearchPlaceholder.Text = Loc.T("Search");
        LanguageHeader.Text = Loc.T("Language");
        LanguageButtonText.Text = Loc.CurrentLanguage.ToUpperInvariant();
        ThemeButton.ToolTip = Loc.T("Theme");
        LanguageButton.ToolTip = Loc.T("Language");

        UpdateLanguageChecks();
        UpdateArchiveUi();
        RenderEntries();

        if (_archivePath is null)
            SetStatus(Loc.T("Ready"));
    }

    private void UpdateLanguageChecks()
    {
        var visible = Visibility.Visible;
        var hidden = Visibility.Collapsed;

        LangEnCheck.Visibility = Loc.CurrentLanguage == "en" ? visible : hidden;
        LangTrCheck.Visibility = Loc.CurrentLanguage == "tr" ? visible : hidden;
        LangDeCheck.Visibility = Loc.CurrentLanguage == "de" ? visible : hidden;
        LangFrCheck.Visibility = Loc.CurrentLanguage == "fr" ? visible : hidden;
        LangEsCheck.Visibility = Loc.CurrentLanguage == "es" ? visible : hidden;
    }

    // ===================== Opening =====================

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
        => await OpenArchiveAsync();

    private async Task OpenArchiveAsync(string? path = null)
    {
        if (_busy) return;

        if (path is null)
        {
            var dialog = new OpenFileDialog
            {
                Title = Loc.T("OpenArchiveTitle"),
                Filter = BuildOpenFilter()
            };

            if (dialog.ShowDialog(this) != true) return;
            path = dialog.FileName;
        }

        if (!File.Exists(path)) return;

        try
        {
            SetBusy(true);
            SetStatus(Loc.T("Opening"));

            var target = path;
            var entries = await Task.Run(() =>
            {
                using var reader = new SharpCompressArchiveReader(target);
                return reader.Entries.ToList();
            });

            _entries.Clear();
            _entries.AddRange(entries);
            _archivePath = path;

            if (SearchBox.Text.Length > 0)
                SearchBox.Clear();

            UpdateArchiveUi();
            RenderEntries();
            SetStatus(Loc.Format("ItemsCount", _entries.Count(e => !e.IsDirectory)));
        }
        catch (Exception ex)
        {
            ShowError(ex);
            SetStatus(Loc.T("Ready"), true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ===================== Extract / test =====================

    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        if (_archivePath is null)
        {
            MessageBox.Show(this, Loc.T("OpenFirst"), "zpX", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = Loc.T("ExtractTitle"),
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        var archivePath = _archivePath;
        var target = Path.Combine(dialog.FolderName, Path.GetFileNameWithoutExtension(archivePath));

        try
        {
            SetBusy(true);
            var progress = CreateThrottledProgress(p => SetStatus(Loc.Format("Extracting", p)));

            var count = await Task.Run(() =>
            {
                using var reader = new SharpCompressArchiveReader(archivePath);
                reader.ExtractAll(target, progress);
                return reader.Entries.Count(entry => !entry.IsDirectory);
            });

            var done = Loc.Format("ExtractDone", count);
            SetStatus(done);
            MessageBox.Show(this, done + Environment.NewLine + target, "zpX",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            SetStatus(Loc.T("Ready"), true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (_archivePath is null)
        {
            MessageBox.Show(this, Loc.T("OpenFirst"), "zpX", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var archivePath = _archivePath;

        try
        {
            SetBusy(true);
            SetStatus(Loc.T("Testing"));
            var progress = CreateThrottledProgress(p => SetStatus(Loc.Format("Testing", p)));

            var result = await Task.Run(() =>
            {
                using var reader = new SharpCompressArchiveReader(archivePath);
                return reader.Test(progress);
            });

            if (result.Success)
            {
                var message = Loc.Format("TestOk", result.CheckedEntries);
                SetStatus(message);
                MessageBox.Show(this, message, Loc.T("TestTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var message = Loc.Format("TestFailed", result.Error ?? string.Empty);
                SetStatus(message, true);
                MessageBox.Show(this, message, Loc.T("TestTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
            SetStatus(Loc.T("Ready"), true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ===================== Creating =====================

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        NewPopup.IsOpen = true;
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        NewPopup.IsOpen = false;

        var dialog = new OpenFileDialog
        {
            Title = Loc.T("CreateTitle"),
            Multiselect = true,
            Filter = Loc.T("AllFiles") + " (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0) return;
        await CreateArchiveAsync(dialog.FileNames, DefaultArchiveName(dialog.FileNames[0]));
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        NewPopup.IsOpen = false;

        var dialog = new OpenFolderDialog
        {
            Title = Loc.T("AddFolder"),
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        var folder = dialog.FolderName;
        await CreateArchiveAsync(new[] { folder }, new DirectoryInfo(folder).Name);
    }

    private async Task CreateArchiveAsync(IReadOnlyCollection<string> sources, string suggestedName)
    {
        var dialog = new SaveFileDialog
        {
            Title = Loc.T("SaveTitle"),
            FileName = suggestedName + ArchiveCreator.Extension,
            DefaultExt = ArchiveCreator.Extension,
            AddExtension = true,
            Filter = "zpX (*.zpx)|*.zpx|ZIP (*.zip)|*.zip"
        };

        if (dialog.ShowDialog(this) != true) return;

        var output = dialog.FileName;

        try
        {
            SetBusy(true);
            var progress = CreateThrottledProgress(p => SetStatus(Loc.Format("Creating", p)));

            await Task.Run(() =>
            {
                var options = new ArchiveCreateOptions
                {
                    OutputPath = output
                };
                ArchiveCreator.Create(sources, options, progress);
            });

            var saved = ArchiveCreator.EnsureExtension(output);
            var done = Loc.Format("CreateDone", Path.GetFileName(saved));
            SetStatus(done);
            MessageBox.Show(this, done + Environment.NewLine + saved, "zpX",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            SetStatus(Loc.T("Ready"), true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static string DefaultArchiveName(string source)
    {
        var name = Directory.Exists(source)
            ? new DirectoryInfo(source).Name
            : Path.GetFileNameWithoutExtension(source);

        return string.IsNullOrWhiteSpace(name) ? "archive" : name;
    }

    // ===================== Listing =====================

    private void UpdateArchiveUi()
    {
        var hasArchive = _archivePath is not null;
        var fileCount = _entries.Count;
        var totalSize = _entries.Where(entry => !entry.IsDirectory).Sum(entry => entry.Size);

        PathText.Text = _archivePath ?? string.Empty;
        PathText.ToolTip = _archivePath;
        ArchiveInfoText.Text = hasArchive && !string.IsNullOrEmpty(_archivePath)
            ? $"{Path.GetExtension(_archivePath).TrimStart('.').ToUpperInvariant()}  ·  {FormatSize(totalSize)}"
            : string.Empty;

        WindowSubtitle.Text = hasArchive
            ? Path.GetFileName(_archivePath)
            : Loc.T("ArchiveManager");
        Title = hasArchive ? $"zpX — {Path.GetFileName(_archivePath)}" : "zpX";

        EmptyState.Visibility = hasArchive ? Visibility.Collapsed : Visibility.Visible;
        ExtractButton.IsEnabled = hasArchive && !_busy;
        TestButton.IsEnabled = hasArchive && !_busy;

        ItemsCountText.Text = hasArchive ? Loc.Format("ItemsCount", fileCount) : string.Empty;
        TotalSizeText.Text = hasArchive ? FormatSize(totalSize) : string.Empty;
    }

    private void RenderEntries()
    {
        var query = SearchBox.Text.Trim();

        IEnumerable<ArchiveEntry> filtered = _entries;
        if (query.Length > 0)
        {
            filtered = filtered.Where(entry =>
                entry.FullName.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        var items = filtered
            .OrderBy(entry => entry.IsDirectory ? 0 : 1)
            .ThenBy(entry => entry.FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToFileItem)
            .ToList();

        FileList.ItemsSource = items;
    }

    private static FileItem ToFileItem(ArchiveEntry entry)
    {
        var extension = Path.GetExtension(entry.FullName);

        return new FileItem
        {
            FullName = entry.FullName,
            IsDirectory = entry.IsDirectory,
            SizeText = entry.IsDirectory ? "—" : FormatSize(entry.Size),
            PackedText = entry.IsDirectory ? "—" : FormatSize(entry.CompressedSize),
            TypeText = entry.IsDirectory
                ? Loc.T("Folder")
                : string.IsNullOrEmpty(extension) ? Loc.T("File") : extension.TrimStart('.').ToUpperInvariant(),
            ModifiedText = entry.LastModified.HasValue
                ? entry.LastModified.Value.ToLocalTime().ToString("g", Loc.Culture)
                : "—"
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        RenderEntries();
    }

    private void UpdateSearchPlaceholder()
    {
        if (SearchPlaceholder is null || SearchBox is null) return;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ===================== Helpers =====================

    private void SetBusy(bool busy)
    {
        _busy = busy;

        Mouse.OverrideCursor = busy ? Cursors.Wait : null;

        NewButton.IsEnabled = !busy;
        OpenButton.IsEnabled = !busy;
        EmptyOpenButton.IsEnabled = !busy;
        ExtractButton.IsEnabled = !busy && _archivePath is not null;
        TestButton.IsEnabled = !busy && _archivePath is not null;
    }

    private void SetStatus(string message, bool error = false)
    {
        StatusText.Text = message;
        StatusDot.Fill = (Brush)FindResource(error ? "DangerBrush" : "AccentBrush");
    }

    private void ShowError(Exception ex)
    {
        MessageBox.Show(this, ex.Message, Loc.T("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static IProgress<string> CreateThrottledProgress(Action<string> report)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastUpdate = 0L;

        return new Progress<string>(value =>
        {
            var now = stopwatch.ElapsedMilliseconds;
            if (now - lastUpdate < 70) return;
            lastUpdate = now;
            report(value);
        });
    }

    private static string BuildOpenFilter()
    {
        const string archives =
            "*.zpx;*.zip;*.7z;*.rar;*.tar;*.tar.gz;*.tgz;*.gz;*.bz2;*.xz;*.zst;*.lz;*.cab;*.iso";

        return $"{Loc.T("ArchiveFilter")} ({archives})|{archives}|{Loc.T("AllFiles")} (*.*)|*.*";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var format = unit == 0 ? "N0" : value >= 100 ? "N0" : "N1";
        return value.ToString(format, Loc.Culture) + " " + units[unit];
    }

    // ===================== Drag & drop / keyboard =====================

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        DropOverlay.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        await OpenArchiveAsync(files[0]);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            OpenButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            NewPopup.IsOpen = true;
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            TestButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            LanguagePopup.IsOpen = false;
            NewPopup.IsOpen = false;
        }
    }
}
