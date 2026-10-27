using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using zpX.UI.Models;

namespace zpX.UI;

public partial class PropertiesWindow : Window
{
    private readonly FileItem _item;
    private readonly bool _isArchive;
    private readonly string _location;

    public PropertiesWindow(FileItem item, bool isArchive, string location)
    {
        InitializeComponent();
        _item = item;
        _isArchive = isArchive;
        _location = location;

        NameText.Text = System.IO.Path.GetFileName(item.FullName.TrimEnd('/', '\\'));
        TypeText.Text = item.IsDirectory ? "Folder" : item.TypeText;
        LocationText.Text = location;
        SizeText.Text = item.IsDirectory ? "—" : FormatSize(item.Size);
        PackedText.Text = isArchive ? (item.IsDirectory ? "—" : FormatSize(item.CompressedSize)) : "—";
        ModifiedText.Text = item.ModifiedText;
        CreatedText.Text = item.Modified.HasValue ? item.Modified.Value.ToLocalTime().ToString("g") : "—";

        try
        {
            if (!_isArchive && File.Exists(item.FullName))
            {
                var info = new FileInfo(item.FullName);
                AttributesText.Text = info.Attributes.ToString();
                CreatedText.Text = info.CreationTime.ToString("g");
            }
            else if (!_isArchive && Directory.Exists(item.FullName))
            {
                var info = new DirectoryInfo(item.FullName);
                AttributesText.Text = info.Attributes.ToString();
                CreatedText.Text = info.CreationTime.ToString("g");
            }
            else
            {
                AttributesText.Text = "—";
            }
        }
        catch { AttributesText.Text = "—"; }

        if (item.IsDirectory || _isArchive)
            ComputeHashesButton.IsEnabled = false;
    }

    private async void ComputeHashesButton_Click(object sender, RoutedEventArgs e)
    {
        var path = _item.FullName.TrimEnd('/', '\\');
        if (!File.Exists(path)) return;

        ComputeHashesButton.IsEnabled = false;
        ComputeHashesButton.Content = "Computing...";

        try
        {
            var result = await Task.Run(() =>
            {
                using var md5 = MD5.Create();
                using var sha1 = SHA1.Create();
                using var sha256 = SHA256.Create();
                using var sha512 = SHA512.Create();
                using var stream = File.OpenRead(path);

                // Read once into memory for multiple hashes (small files typical)
                var data = ReadAllBytes(stream);

                return (
                    BytesToHex(md5.ComputeHash(data)),
                    BytesToHex(sha1.ComputeHash(data)),
                    BytesToHex(sha256.ComputeHash(data)),
                    BytesToHex(sha512.ComputeHash(data))
                );
            });

            Md5Box.Text = result.Item1;
            Sha1Box.Text = result.Item2;
            Sha256Box.Text = result.Item3;
            Sha512Box.Text = result.Item4;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "zpX", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ComputeHashesButton.Content = "Compute hashes";
            ComputeHashesButton.IsEnabled = true;
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string BytesToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        var format = unit == 0 ? "N0" : value >= 100 ? "N0" : "N1";
        return value.ToString(format, System.Globalization.CultureInfo.CurrentCulture) + " " + units[unit];
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
