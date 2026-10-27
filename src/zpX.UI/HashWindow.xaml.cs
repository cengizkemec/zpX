using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace zpX.UI;

public partial class HashWindow : Window
{
    private sealed class HashRow
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = "—";
    }

    private readonly List<string> _paths;
    private readonly List<HashRow> _rows = new();

    public HashWindow(List<string> paths)
    {
        InitializeComponent();
        _paths = paths;
        foreach (var p in paths)
            _rows.Add(new HashRow { Name = System.IO.Path.GetFileName(p), Path = p });
        HashList.ItemsSource = _rows;
    }

    private async void ComputeButton_Click(object sender, RoutedEventArgs e)
    {
        ComputeButton.IsEnabled = false;
        StatusText.Text = "Computing...";

        await Task.Run(() =>
        {
            foreach (var row in _rows)
            {
                try
                {
                    using var sha = SHA256.Create();
                    using var stream = File.OpenRead(row.Path);
                    var hash = sha.ComputeHash(stream);
                    row.Hash = BytesToHex(hash);
                }
                catch
                {
                    row.Hash = "error";
                }
            }
        });

        HashList.Items.Refresh();
        StatusText.Text = "Done";
        ComputeButton.IsEnabled = true;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine, _rows.Select(r => r.Path + "  " + r.Hash));
        try { Clipboard.SetText(text); StatusText.Text = "Copied"; } catch { }
    }

    private static string BytesToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
