using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using zpX.Core.Abstractions;
using zpX.Core.Archives;
using zpX.Localization;
using zpX.UI.Helpers;
using zpX.UI.Models;

namespace zpX.UI;

public partial class MainWindow : Window
{
    private sealed class Location
    {
        public bool IsArchive { get; }
        public string FsPath { get; }
        public string? ArchivePath { get; }
        public string ArchivePrefix { get; }

        public Location(bool isArchive, string fsPath, string? archivePath, string archivePrefix)
        {
            IsArchive = isArchive;
            FsPath = fsPath;
            ArchivePath = archivePath;
            ArchivePrefix = archivePrefix;
        }
    }

    private Location _location;
    private readonly Stack<Location> _backStack = new();
    private readonly Stack<Location> _forwardStack = new();

    private readonly List<ArchiveEntry> _entries = new();
    private readonly List<FileItem> _allItems = new();
    private string? _archivePath;
    private bool _busy;

    private readonly List<string> _clipboardPaths = new();
    private bool _clipboardCut;

    public MainWindow()
    {
        InitializeComponent();

        _location = new Location(false, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), null, "");

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

        ContextMenuRegistrar.Register(); // idempotent-ish

        _ = RefreshViewAsync();

        HandleStartupArgs(App.StartupArgs);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        Mouse.OverrideCursor = null;
    }

    private void ThemeManager_ThemeChanged(object? sender, EventArgs e) => UpdateThemeGlyph();

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
        UpdateThemeGlyph();
    }

    // ===================== Chrome =====================

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        try { DragMove(); } catch { }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var maximized = WindowState == WindowState.Maximized;
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

        EmptyTitleText.Text = _location.IsArchive ? Loc.T("EmptyTitle") : Loc.T("EmptyFolder");
        EmptyHintText.Text = _location.IsArchive ? Loc.T("EmptyHint") : Loc.T("EmptyFolderHint");
        EmptyOpenText.Text = Loc.T("OpenArchiveTitle");
        DropText.Text = Loc.T("DropToOpen");

        SearchPlaceholder.Text = Loc.T("Search");
        LanguageHeader.Text = Loc.T("Language");
        LanguageButtonText.Text = Loc.CurrentLanguage.ToUpperInvariant();
        ThemeButton.ToolTip = Loc.T("Theme");
        LanguageButton.ToolTip = Loc.T("Language");

        // context menu headers
        CtxOpen.Header = Loc.T("Open");
        CtxCopy.Header = Loc.T("Copy");
        CtxCut.Header = Loc.T("Cut");
        CtxPaste.Header = Loc.T("Paste");
        CtxDelete.Header = Loc.T("Delete");
        CtxRename.Header = Loc.T("Rename");
        CtxNewFolder.Header = Loc.T("NewFolder");
        CtxCompress.Header = Loc.T("Compress");
        CtxExtract.Header = Loc.T("Extract");
        CtxProperties.Header = Loc.T("Properties");
        CtxHash.Header = Loc.T("Hash");

        UpdateLanguageChecks();
        _ = RefreshViewAsync();
        if (_location.IsArchive == false && _archivePath is null)
            SetStatus(Loc.T("Ready"));
    }

    private void UpdateLanguageChecks()
    {
        LangEnCheck.Visibility = Loc.CurrentLanguage == "en" ? Visibility.Visible : Visibility.Collapsed;
        LangTrCheck.Visibility = Loc.CurrentLanguage == "tr" ? Visibility.Visible : Visibility.Collapsed;
        LangDeCheck.Visibility = Loc.CurrentLanguage == "de" ? Visibility.Visible : Visibility.Collapsed;
        LangFrCheck.Visibility = Loc.CurrentLanguage == "fr" ? Visibility.Visible : Visibility.Collapsed;
        LangEsCheck.Visibility = Loc.CurrentLanguage == "es" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===================== Navigation =====================

    private async Task RefreshViewAsync()
    {
        if (_location.IsArchive)
            await RefreshArchiveAsync();
        else
            await RefreshFileSystemAsync();

        UpdatePathBar();
        UpdateEmptyState();
        UpdateToolbarState();
    }

    private async Task RefreshFileSystemAsync()
    {
        var dir = _location.FsPath;
        List<FileSystemInfo> infos = new();
        try
        {
            infos = await Task.Run(() =>
            {
                var list = new List<FileSystemInfo>();
                try { list.AddRange(new DirectoryInfo(dir).EnumerateDirectories()); } catch { }
                try { list.AddRange(new DirectoryInfo(dir).EnumerateFiles()); } catch { }
                return list;
            });
        }
        catch { }

        if (!AppSettings.Current.ShowHiddenFiles)
            infos = infos.Where(f => (f.Attributes & FileAttributes.Hidden) == 0).ToList();

        var items = infos.Select(fsi =>
        {
            bool isDir = fsi is DirectoryInfo;
            long size = isDir ? 0 : ((FileInfo)fsi).Length;
            var ext = isDir ? "" : Path.GetExtension(fsi.Name);
            return new FileItem
            {
                FullName = fsi.FullName,
                IsDirectory = isDir,
                Size = size,
                Modified = fsi.LastWriteTime,
                SizeText = isDir ? "—" : FormatSize(size),
                PackedText = "",
                TypeText = isDir ? Loc.T("Folder") : (string.IsNullOrEmpty(ext) ? Loc.T("File") : ext.TrimStart('.').ToUpperInvariant()),
                ModifiedText = fsi.LastWriteTime.ToString("g", Loc.Culture)
            };
        })
        .OrderBy(i => i.IsDirectory ? 0 : 1)
        .ThenBy(i => Path.GetFileName(i.FullName), StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        _allItems.Clear();
        _allItems.AddRange(items);
        FileList.ItemsSource = items;
    }

    private async Task RefreshArchiveAsync()
    {
        if (_entries.Count == 0 && _location.ArchivePath is not null)
        {
            var ap = _location.ArchivePath;
            try
            {
                var loaded = await Task.Run(() =>
                {
                    using var reader = new SharpCompressArchiveReader(ap);
                    return reader.Entries.ToList();
                });
                _entries.Clear();
                _entries.AddRange(loaded);
                _archivePath = ap;
            }
            catch (Exception ex)
            {
                ShowError(ex);
                await NavigateFileSystemAsync(Path.GetDirectoryName(ap) ?? _location.FsPath, false);
                return;
            }
        }

        var prefix = _location.ArchivePrefix;
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var isDirDict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var fileEntryMap = new Dictionary<string, ArchiveEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in _entries)
        {
            if (!e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = e.FullName.Substring(prefix.Length).TrimStart('/');
            if (rest.Length == 0) continue;
            var slash = rest.IndexOf('/');
            string name;
            bool isDir;
            if (slash >= 0)
            {
                name = rest.Substring(0, slash);
                isDir = true;
            }
            else
            {
                name = rest;
                isDir = e.IsDirectory;
                if (!isDir) fileEntryMap[name] = e;
            }

            names.Add(name);
            if (isDirDict.TryGetValue(name, out var existing))
                isDirDict[name] = existing || isDir;
            else
                isDirDict[name] = isDir;
        }

        var items = new List<FileItem>();
        foreach (var name in names)
        {
            var isDir = isDirDict.TryGetValue(name, out var d) && d;
            ArchiveEntry? fe = fileEntryMap.TryGetValue(name, out var f) ? f : null;
            var fullName = prefix + name + (isDir ? "/" : "");
            items.Add(new FileItem
            {
                FullName = fullName,
                IsDirectory = isDir,
                Size = isDir ? 0 : (fe?.Size ?? 0),
                CompressedSize = isDir ? 0 : (fe?.CompressedSize ?? 0),
                Modified = isDir ? null : fe?.LastModified,
                SizeText = isDir ? "—" : FormatSize(fe?.Size ?? 0),
                PackedText = isDir ? "—" : FormatSize(fe?.CompressedSize ?? 0),
                TypeText = isDir ? Loc.T("Folder") : (string.IsNullOrEmpty(Path.GetExtension(name)) ? Loc.T("File") : Path.GetExtension(name).TrimStart('.').ToUpperInvariant()),
                ModifiedText = (isDir ? null : fe?.LastModified)?.ToLocalTime().ToString("g", Loc.Culture) ?? "—"
            });
        }

        items = items.OrderBy(i => i.IsDirectory ? 0 : 1)
                     .ThenBy(i => Path.GetFileName(i.FullName.TrimEnd('/')), StringComparer.CurrentCultureIgnoreCase)
                     .ToList();

        _allItems.Clear();
        _allItems.AddRange(items);
        FileList.ItemsSource = items;
    }

    private void UpdatePathBar()
    {
        if (_location.IsArchive)
        {
            var display = Path.GetFileName(_location.ArchivePath) + "\\" + _location.ArchivePrefix.TrimEnd('/');
            if (display.EndsWith("\\")) display = Path.GetFileName(_location.ArchivePath);
            PathBox.Text = display;
            PathBox.IsReadOnly = true;
            PathBox.ToolTip = _location.ArchivePath + "\\" + _location.ArchivePrefix;
        }
        else
        {
            PathBox.Text = _location.FsPath;
            PathBox.IsReadOnly = false;
            PathBox.ToolTip = null;
        }

        var hasArchive = _location.IsArchive;
        _archivePath = hasArchive ? _location.ArchivePath : null;
    }

    private void UpdateEmptyState()
    {
        var count = (FileList.ItemsSource as ICollection)?.Count ?? 0;
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_location.IsArchive)
        {
            EmptyTitleText.Text = Loc.T("EmptyTitle");
            EmptyHintText.Text = Loc.T("EmptyHint");
        }
        else
        {
            EmptyTitleText.Text = Loc.T("EmptyFolder");
            EmptyHintText.Text = Loc.T("EmptyFolderHint");
        }
    }

    private async Task NavigateToAsync(Location location, bool pushHistory = true)
    {
        if (pushHistory && _location is not null)
        {
            _backStack.Push(_location);
            _forwardStack.Clear();
        }

        _location = location;
        await RefreshViewAsync();
    }

    private async Task NavigateFileSystemAsync(string path, bool pushHistory = true)
    {
        if (!Directory.Exists(path)) return;
        await NavigateToAsync(new Location(false, path, null, ""), pushHistory);
    }

    private async Task NavigateArchiveAsync(string archivePath, string prefix, bool pushHistory = true)
    {
        if (_archivePath != archivePath || _entries.Count == 0)
        {
            // load entries
            try
            {
                var loaded = await Task.Run(() =>
                {
                    using var reader = new SharpCompressArchiveReader(archivePath);
                    return reader.Entries.ToList();
                });
                _entries.Clear();
                _entries.AddRange(loaded);
                _archivePath = archivePath;
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }
        }

        if (!prefix.EndsWith("/") && prefix.Length > 0) prefix += "/";
        await NavigateToAsync(new Location(true, Path.GetDirectoryName(archivePath) ?? "", archivePath, prefix), pushHistory);
    }

    private async Task GoUpAsync()
    {
        if (_location.IsArchive)
        {
            var prefix = _location.ArchivePrefix.TrimEnd('/');
            if (prefix.Length == 0)
            {
                // exit archive to containing folder
                await NavigateFileSystemAsync(Path.GetDirectoryName(_location.ArchivePath!) ?? _location.FsPath);
                _entries.Clear();
                _archivePath = null;
            }
            else
            {
                var parent = prefix.Substring(0, prefix.LastIndexOf('/') + 1);
                await NavigateArchiveAsync(_location.ArchivePath!, parent, false);
            }
        }
        else
        {
            var parent = Directory.GetParent(_location.FsPath);
            if (parent is not null)
                await NavigateFileSystemAsync(parent.FullName);
        }
    }

    private async Task GoBackAsync()
    {
        if (_backStack.Count == 0) return;
        var prev = _backStack.Pop();
        _forwardStack.Push(_location);
        _location = prev;
        await RefreshViewAsync();
    }

    private async Task GoForwardAsync()
    {
        if (_forwardStack.Count == 0) return;
        var next = _forwardStack.Pop();
        _backStack.Push(_location);
        _location = next;
        await RefreshViewAsync();
    }

    // ===================== Toolbar state =====================

    private void UpdateToolbarState()
    {
        var fs = !_location.IsArchive;
        var selected = FileList.SelectedItems.Cast<FileItem>().ToList();
        var hasSelection = selected.Count > 0;
        var hasFiles = selected.Any(i => !i.IsDirectory);

        NewButton.IsEnabled = !_busy && fs;
        OpenButton.IsEnabled = !_busy;
        ExtractButton.IsEnabled = !_busy && _location.IsArchive;
        TestButton.IsEnabled = !_busy && _location.IsArchive;

        CopyButton.IsEnabled = !_busy && fs && hasSelection;
        CutButton.IsEnabled = !_busy && fs && hasSelection;
        PasteButton.IsEnabled = !_busy && fs && _clipboardPaths.Count > 0;
        DeleteButton.IsEnabled = !_busy && fs && hasSelection;
        RenameButton.IsEnabled = !_busy && fs && selected.Count == 1;
        NewFolderButton.IsEnabled = !_busy && fs;
        PropertiesButton.IsEnabled = !_busy && selected.Count == 1;
        HashButton.IsEnabled = !_busy && fs && hasFiles;

        BackButton.IsEnabled = _backStack.Count > 0;
        ForwardButton.IsEnabled = _forwardStack.Count > 0;
        UpButton.IsEnabled = !_busy && (_location.IsArchive || Directory.GetParent(_location.FsPath) is not null);
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateToolbarState();

    // ===================== Opening =====================

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        if (_location.IsArchive)
        {
            // open selected archive entry as file
            if (FileList.SelectedItem is FileItem item && !item.IsDirectory)
            {
                await OpenArchiveEntryAsync(item);
            }
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = Loc.T("OpenArchiveTitle"),
            Filter = BuildOpenFilter()
        };

        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName;

        if (IsArchivePath(path))
        {
            await OpenArchiveFileAsync(path);
        }
        else
        {
            // open file with shell
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { ShowError(ex); }
        }
    }

    private bool IsArchivePath(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".zip" or ".zpx" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" or ".bz2" or ".xz" or ".zst" or ".lz" or ".cab" or ".iso";
    }

    private async Task OpenArchiveFileAsync(string path)
    {
        try
        {
            SetBusy(true);
            SetStatus(Loc.T("Opening"));
            await NavigateArchiveAsync(path, "");
            SetStatus(Loc.Format("ItemsCount", _entries.Count));
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

    private async Task OpenArchiveEntryAsync(FileItem item)
    {
        try
        {
            SetBusy(true);
            var archivePath = _location.ArchivePath!;
            var tempDir = Path.Combine(Path.GetTempPath(), "zpX", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            await Task.Run(() =>
            {
                using var reader = new SharpCompressArchiveReader(archivePath);
                var entry = reader.Entries.First(e => e.FullName == item.FullName.TrimEnd('/'));
                reader.ExtractAll(tempDir); // extracts all; acceptable for open
            });

            var extracted = Path.Combine(tempDir, item.FullName.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(extracted))
                Process.Start(new ProcessStartInfo(extracted) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); }
    }

    // ===================== Extract / test =====================

    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_location.IsArchive)
        {
            // compress selected files instead? handled by New
            return;
        }

        var dialog = new OpenFolderDialog { Title = Loc.T("ExtractTitle") };
        if (dialog.ShowDialog(this) != true) return;

        var archivePath = _location.ArchivePath!;
        var prefix = _location.ArchivePrefix;
        var target = Path.Combine(dialog.FolderName, Path.GetFileNameWithoutExtension(archivePath));
        if (!string.IsNullOrEmpty(prefix.Trim('/')))
            target = Path.Combine(target, prefix.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));

        try
        {
            SetBusy(true);
            var progress = CreateThrottledProgress(p => SetStatus(Loc.Format("Extracting", p)));
            var count = await Task.Run(() =>
            {
                using var reader = new SharpCompressArchiveReader(archivePath);
                var toExtract = reader.Entries
                    .Where(en => en.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var done = 0;
                foreach (var entry in toExtract)
                {
                    var relative = entry.FullName.Substring(prefix.Length).TrimStart('/');
                    if (string.IsNullOrEmpty(relative)) continue;

                    var destPath = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (entry.IsDirectory)
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    progress.Report(relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    using var input = reader.OpenEntry(entry);
                    using var output = File.Create(destPath);
                    input.CopyTo(output);
                    done++;
                }
                return done;
            });
            var done = Loc.Format("ExtractDone", count);
            SetStatus(done);
            MessageBox.Show(this, done + Environment.NewLine + target, "zpX", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError(ex); SetStatus(Loc.T("Ready"), true); }
        finally { SetBusy(false); }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_location.IsArchive) return;
        var archivePath = _location.ArchivePath!;
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
                var msg = Loc.Format("TestOk", result.CheckedEntries);
                SetStatus(msg);
                MessageBox.Show(this, msg, Loc.T("TestTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var msg = Loc.Format("TestFailed", result.Error ?? string.Empty);
                SetStatus(msg, true);
                MessageBox.Show(this, msg, Loc.T("TestTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex) { ShowError(ex); SetStatus(Loc.T("Ready"), true); }
        finally { SetBusy(false); }
    }

    // ===================== Creating =====================

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_location.IsArchive)
            NewPopup.IsOpen = true;
        else
        {
            // compress selected archive entries? not supported; create from current extracted? skip
            NewPopup.IsOpen = true;
        }
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        NewPopup.IsOpen = false;
        var dialog = new OpenFileDialog { Title = Loc.T("CreateTitle"), Multiselect = true, Filter = Loc.T("AllFiles") + " (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0) return;
        await CreateArchiveAsync(dialog.FileNames.ToList(), DefaultArchiveName(dialog.FileNames[0]));
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        NewPopup.IsOpen = false;
        var dialog = new OpenFolderDialog { Title = Loc.T("AddFolder") };
        if (dialog.ShowDialog(this) != true) return;
        var folder = dialog.FolderName;
        await CreateArchiveAsync(new List<string> { folder }, new DirectoryInfo(folder).Name);
    }

    private async void CtxCompress_Click(object sender, RoutedEventArgs e)
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;
        await CreateArchiveAsync(paths, DefaultArchiveName(paths[0]));
    }

    private async Task CreateArchiveAsync(List<string> sources, string suggestedName)
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
                ArchiveCreator.Create(sources, new ArchiveCreateOptions { OutputPath = output }, progress);
            });
            var saved = ArchiveCreator.EnsureExtension(output);
            var done = Loc.Format("CreateDone", Path.GetFileName(saved));
            SetStatus(done);
            MessageBox.Show(this, done + Environment.NewLine + saved, "zpX", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { ShowError(ex); SetStatus(Loc.T("Ready"), true); }
        finally { SetBusy(false); }
    }

    private static string DefaultArchiveName(string source)
    {
        var name = Directory.Exists(source) ? new DirectoryInfo(source).Name : Path.GetFileNameWithoutExtension(source);
        return string.IsNullOrWhiteSpace(name) ? "archive" : name;
    }

    // ===================== File operations =====================

    private List<string> GetSelectedPaths()
    {
        return FileList.SelectedItems.Cast<FileItem>()
            .Select(i => i.FullName.TrimEnd('/'))
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        _clipboardPaths.Clear();
        _clipboardPaths.AddRange(GetSelectedPaths());
        _clipboardCut = false;
        SetStatus(Loc.T("Ready"));
        UpdateToolbarState();
    }

    private async void CutButton_Click(object sender, RoutedEventArgs e)
    {
        _clipboardPaths.Clear();
        _clipboardPaths.AddRange(GetSelectedPaths());
        _clipboardCut = true;
        SetStatus(Loc.T("Ready"));
        UpdateToolbarState();
    }

    private async void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_location.IsArchive) return;
        if (_clipboardPaths.Count == 0) return;

        try
        {
            SetBusy(true);
            await Task.Run(() =>
            {
                foreach (var src in _clipboardPaths)
                {
                    var dest = Path.Combine(_location.FsPath, Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar)));
                    if (Directory.Exists(src) || File.Exists(src))
                    {
                        if (_clipboardCut)
                        {
                            if (Directory.Exists(src)) MoveDirectory(src, dest);
                            else File.Move(src, dest, overwrite: false);
                        }
                        else
                        {
                            if (Directory.Exists(src)) CopyDirectory(src, dest);
                            else File.Copy(src, dest, overwrite: false);
                        }
                    }
                }
            });

            if (_clipboardCut)
            {
                _clipboardPaths.Clear();
                _clipboardCut = false;
            }

            await RefreshViewAsync();
            SetStatus(Loc.T("Done"));
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_location.IsArchive) return;
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;

        var result = MessageBox.Show(this, $"Delete {paths.Count} item(s)?", "zpX", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            SetBusy(true);
            await Task.Run(() =>
            {
                foreach (var p in paths)
                {
                    if (Directory.Exists(p)) Directory.Delete(p, recursive: true);
                    else if (File.Exists(p)) File.Delete(p);
                }
            });
            await RefreshViewAsync();
            SetStatus(Loc.T("Done"));
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); }
    }

    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (_location.IsArchive) return;
        var item = FileList.SelectedItem as FileItem;
        if (item is null) return;

        var oldPath = item.FullName.TrimEnd(Path.DirectorySeparatorChar);
        var oldName = Path.GetFileName(oldPath);
        var newName = Interaction.InputBox(Loc.T("Name"), "zpX", oldName);
        if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

        var newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);
        try
        {
            SetBusy(true);
            await Task.Run(() =>
            {
                if (Directory.Exists(oldPath)) Directory.Move(oldPath, newPath);
                else File.Move(oldPath, newPath);
            });
            await RefreshViewAsync();
            SetStatus(Loc.T("Done"));
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); }
    }

    private async void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_location.IsArchive) return;
        var name = Interaction.InputBox(Loc.T("NewFolderName"), "zpX", "New folder");
        if (string.IsNullOrWhiteSpace(name)) return;
        var path = Path.Combine(_location.FsPath, name);
        try
        {
            SetBusy(true);
            await Task.Run(() => Directory.CreateDirectory(path));
            await RefreshViewAsync();
            SetStatus(Loc.T("Done"));
        }
        catch (Exception ex) { ShowError(ex); }
        finally { SetBusy(false); }
    }

    private void PropertiesButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is not FileItem item) return;
        var dlg = new PropertiesWindow(item, _location.IsArchive, _location.IsArchive ? _location.ArchivePath! : _location.FsPath);
        dlg.Owner = this;
        dlg.ShowDialog();
    }

    private void HashButton_Click(object sender, RoutedEventArgs e)
    {
        var paths = GetSelectedPaths().Where(File.Exists).ToList();
        if (paths.Count == 0) return;
        var dlg = new HashWindow(paths);
        dlg.Owner = this;
        dlg.Show();
    }

    // ===================== Navigation handlers =====================

    private async void BackButton_Click(object sender, RoutedEventArgs e) => await GoBackAsync();
    private async void ForwardButton_Click(object sender, RoutedEventArgs e) => await GoForwardAsync();
    private async void UpButton_Click(object sender, RoutedEventArgs e) => await GoUpAsync();
    private async void HomeButton_Click(object sender, RoutedEventArgs e) => await NavigateFileSystemAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshViewAsync();

    private async void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var text = PathBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (_location.IsArchive) return;

        if (Directory.Exists(text))
            await NavigateFileSystemAsync(text);
        else
            MessageBox.Show(this, Loc.T("OpenFirst"), "zpX", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is not FileItem item) return;

        if (item.IsDirectory)
        {
            if (_location.IsArchive)
            {
                var prefix = item.FullName;
                if (!prefix.EndsWith("/")) prefix += "/";
                await NavigateArchiveAsync(_location.ArchivePath!, prefix);
            }
            else
            {
                await NavigateFileSystemAsync(item.FullName.TrimEnd(Path.DirectorySeparatorChar));
            }
        }
        else
        {
            if (_location.IsArchive)
            {
                await OpenArchiveEntryAsync(item);
            }
            else
            {
                try { Process.Start(new ProcessStartInfo(item.FullName) { UseShellExecute = true }); }
                catch (Exception ex) { ShowError(ex); }
            }
        }
    }

    private async void CtxOpen_Click(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is FileItem item && !item.IsDirectory)
        {
            if (_location.IsArchive) await OpenArchiveEntryAsync(item);
            else try { Process.Start(new ProcessStartInfo(item.FullName) { UseShellExecute = true }); } catch (Exception ex) { ShowError(ex); }
        }
        else if (FileList.SelectedItem is FileItem dir && dir.IsDirectory)
        {
            if (_location.IsArchive)
            {
                var prefix = dir.FullName; if (!prefix.EndsWith("/")) prefix += "/";
                await NavigateArchiveAsync(_location.ArchivePath!, prefix);
            }
            else await NavigateFileSystemAsync(dir.FullName.TrimEnd(Path.DirectorySeparatorChar));
        }
    }

    // ===================== Drag & drop =====================

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

        if (files.Length == 1 && IsArchivePath(files[0]))
            await OpenArchiveFileAsync(files[0]);
        else if (!_location.IsArchive)
        {
            // copy dropped files into current directory
            try
            {
                SetBusy(true);
                await Task.Run(() =>
                {
                    foreach (var f in files)
                    {
                        var dest = Path.Combine(_location.FsPath, Path.GetFileName(f.TrimEnd(Path.DirectorySeparatorChar)));
                        if (Directory.Exists(f)) CopyDirectory(f, dest);
                        else File.Copy(f, dest, overwrite: true);
                    }
                });
                await RefreshViewAsync();
            }
            catch (Exception ex) { ShowError(ex); }
            finally { SetBusy(false); }
        }
    }

    // ===================== Keyboard =====================

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            OpenButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (!_location.IsArchive) NewPopup.IsOpen = true;
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CopyButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.X && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            CutButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            PasteButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            RenameButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            if (_location.IsArchive) TestButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Back && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            await GoUpAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            LanguagePopup.IsOpen = false;
            NewPopup.IsOpen = false;
        }
    }

    // ===================== Search =====================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchPlaceholder();
        _ = ApplySearchFilterAsync();
    }

    private async Task ApplySearchFilterAsync()
    {
        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
        {
            FileList.ItemsSource = _allItems;
        }
        else
        {
            FileList.ItemsSource = _allItems.Where(i => i.FullName.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }
        UpdateEmptyState();
    }

    private void UpdateSearchPlaceholder()
    {
        if (SearchPlaceholder is null || SearchBox is null) return;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===================== Helpers =====================

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Mouse.OverrideCursor = busy ? Cursors.Wait : null;
        NewButton.IsEnabled = !busy && !_location.IsArchive;
        OpenButton.IsEnabled = !busy;
        EmptyOpenButton.IsEnabled = !busy;
        ExtractButton.IsEnabled = !busy && _location.IsArchive;
        TestButton.IsEnabled = !busy && _location.IsArchive;
        CopyButton.IsEnabled = !busy && !_location.IsArchive;
        CutButton.IsEnabled = !busy && !_location.IsArchive;
        PasteButton.IsEnabled = !busy && !_location.IsArchive && _clipboardPaths.Count > 0;
        DeleteButton.IsEnabled = !busy && !_location.IsArchive;
        RenameButton.IsEnabled = !busy && !_location.IsArchive;
        NewFolderButton.IsEnabled = !busy && !_location.IsArchive;
        PropertiesButton.IsEnabled = !busy;
        HashButton.IsEnabled = !busy && !_location.IsArchive;
        BackButton.IsEnabled = !busy && _backStack.Count > 0;
        ForwardButton.IsEnabled = !busy && _forwardStack.Count > 0;
        UpButton.IsEnabled = !busy;
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
        const string archives = "*.zpx;*.zip;*.7z;*.rar;*.tar;*.tar.gz;*.tgz;*.gz;*.bz2;*.xz;*.zst;*.lz;*.cab;*.iso";
        return $"{Loc.T("ArchiveFilter")} ({archives})|{archives}|{Loc.T("AllFiles")} (*.*)|*.*";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        var format = unit == 0 ? "N0" : value >= 100 ? "N0" : "N1";
        return value.ToString(format, Loc.Culture) + " " + units[unit];
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void MoveDirectory(string source, string destination)
    {
        CopyDirectory(source, destination);
        Directory.Delete(source, recursive: true);
    }

    // ===================== CLI =====================

    private async void HandleStartupArgs(string[] args)
    {
        if (args is null || args.Length == 0) return;

        if (args.Contains("--compress"))
        {
            var idx = Array.IndexOf(args, "--compress");
            var paths = args.Skip(idx + 1).Where(File.Exists).Concat(args.Skip(idx + 1).Where(Directory.Exists)).ToList();
            if (paths.Count > 0)
                await CreateArchiveAsync(paths, DefaultArchiveName(paths[0]));
            return;
        }

        if (args.Contains("--extract-here"))
        {
            var idx = Array.IndexOf(args, "--extract-here");
            if (idx >= 0 && idx + 1 < args.Length)
            {
                var archive = args[idx + 1];
                if (File.Exists(archive) && IsArchivePath(archive))
                {
                    var dir = Path.GetDirectoryName(archive)!;
                    var target = Path.Combine(dir, Path.GetFileNameWithoutExtension(archive));
                    try
                    {
                        SetBusy(true);
                        await Task.Run(() =>
                        {
                            using var reader = new SharpCompressArchiveReader(archive);
                            reader.ExtractAll(target);
                        });
                        MessageBox.Show(this, Loc.Format("ExtractDone", 0) + Environment.NewLine + target, "zpX", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex) { ShowError(ex); }
                    finally { SetBusy(false); }
                }
            }
            return;
        }

        // plain path
        var first = args.FirstOrDefault(File.Exists) ?? args.FirstOrDefault(Directory.Exists);
        if (first is null) return;
        if (Directory.Exists(first))
            await NavigateFileSystemAsync(first);
        else if (IsArchivePath(first))
            await OpenArchiveFileAsync(first);
        else
            await NavigateFileSystemAsync(Path.GetDirectoryName(first) ?? _location.FsPath);
    }
}
