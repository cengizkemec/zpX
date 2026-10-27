using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using zpX.Core.Archives;
using zpX.UI.Helpers;

namespace zpX.UI
{
    public partial class MainWindow : Window
    {
        private string? _currentPath;

        public MainWindow()
        {
            InitializeComponent();
            
            Loaded += (s, e) => 
            {
                var openBtn = FindName("OpenBtn") as System.Windows.Controls.Button;
                if (openBtn != null) openBtn.Click += (s2, e2) => OpenArchive();
                
                var extractBtn = FindName("ExtractBtn") as System.Windows.Controls.Button;
                if (extractBtn != null) extractBtn.Click += (s2, e2) => MessageBox.Show("Extract feature coming soon", "zpX");
                
                var testBtn = FindName("TestBtn") as System.Windows.Controls.Button;
                if (testBtn != null) testBtn.Click += (s2, e2) => TestArchive();
                
                var themeBtn = FindName("ThemeBtn") as System.Windows.Controls.Button;
                if (themeBtn != null) themeBtn.Click += (s2, e2) => ThemeManager.Toggle();
                
                var langBtn = FindName("LangBtn") as System.Windows.Controls.Button;
                if (langBtn != null) langBtn.Click += (s2, e2) => ToggleLanguage();
            };
            
            AllowDrop = true;
            Drop += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files.Length > 0) OpenArchive(files[0]);
                }
            };
            PreviewDragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        }

        private void ToggleLanguage()
        {
            if (Thread.CurrentThread.CurrentUICulture.Name.StartsWith("tr"))
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            else
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("tr-TR");
            MessageBox.Show("Language changed. Restart for full effect.\nDil değiştirildi.", "zpX");
        }

        private void OpenArchive(string? path = null)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    var dlg = new OpenFileDialog
                    {
                        Filter = "Archives (*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz;*.zst;*.zpx)|*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz;*.zst;*.zpx|All Files (*.*)|*.*",
                        Title = "Open Archive"
                    };
                    if (dlg.ShowDialog() != true) return;
                    path = dlg.FileName;
                }
                
                _currentPath = path;
                using var reader = new SharpCompressArchiveReader(path);
                
                var list = FindName("FileList") as System.Windows.Controls.ListView;
                if (list != null)
                {
                    list.Items.Clear();
                    foreach (var ent in reader.Entries.OrderBy(x => x.IsDirectory ? 0 : 1).ThenBy(x => x.FullName))
                    {
                        list.Items.Add(new 
                        { 
                            Ad = ent.FullName, 
                            Boyut = ent.IsDirectory ? "--" : ent.Size.ToString("N0"), 
                            Sıkıştırılmış = ent.IsDirectory ? "--" : ent.CompressedSize.ToString("N0"), 
                            Tür = ent.IsDirectory ? "Folder" : (Path.GetExtension(ent.FullName).TrimStart('.')?.ToUpperInvariant() ?? "FILE"), 
                            Değiştirme = ent.LastModified?.ToString("yyyy-MM-dd HH:mm") ?? "--" 
                        });
                    }
                }
                
                var status = FindName("StatusText") as System.Windows.Controls.TextBlock;
                if (status != null) 
                    status.Text = $"{reader.Entries.Count()} items • {Path.GetFileName(path)}";
                
                var addr = FindName("AddressBox") as System.Windows.Controls.TextBox;
                if (addr != null) addr.Text = path;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "zpX Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestArchive()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentPath))
                {
                    MessageBox.Show("Please open an archive first.", "zpX", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                using var reader = new SharpCompressArchiveReader(_currentPath);
                using var ms = new MemoryStream();
                var ok = reader.Test(ms);
                var log = Encoding.UTF8.GetString(ms.ToArray());
                
                var status = FindName("StatusText") as System.Windows.Controls.TextBlock;
                if (status != null) 
                    status.Text = (ok ? "Test: OK" : "Test: FAILED") + " • " + Path.GetFileName(_currentPath);
                
                if (ok)
                    MessageBox.Show("Archive integrity: OK", "zpX Test", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Archive integrity: FAILED\n\n" + log, "zpX Test", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "zpX Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
