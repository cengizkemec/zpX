using System.IO.Compression;
using ICSharpCode.SharpZipLib.Zip;

namespace zpX.Core.Archives;

public sealed record ArchiveProgress(int Percent, string FileName, long ProcessedBytes, long TotalBytes);

public class ArchiveCreateOptions
{
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Compression level 0 (store) .. 9 (ultra).</summary>
    public int CompressionLevel { get; set; } = 5;

    /// <summary>Optional password. Uses AES-256 ZIP encryption when provided.</summary>
    public string? Password { get; set; }

    /// <summary>Encrypt file names (7z-style). Only effective with a password.</summary>
    public bool EncryptFileNames { get; set; }

    /// <summary>Create a self-extracting archive (appends to an SFX stub).</summary>
    public bool CreateSfx { get; set; }
}

public static class ArchiveCreator
{
    public const string MarkerEntryName = ".zpx/manifest.txt";
    public const string Extension = ".zpx";

    public static void Create(IReadOnlyCollection<string> sources, ArchiveCreateOptions options,
        IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (sources.Count == 0)
            throw new ArgumentException("At least one file or folder is required.", nameof(sources));

        var outputPath = EnsureExtension(options.OutputPath);
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        // Collect all files first for total size / progress.
        var filesToPack = new List<(string FullPath, string Relative)>();
        foreach (var source in sources)
        {
            if (File.Exists(source))
            {
                filesToPack.Add((source, Path.GetFileName(source)));
            }
            else if (Directory.Exists(source))
            {
                var root = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    filesToPack.Add((file, Path.GetRelativePath(root, file).Replace('\\', '/')));
                }
            }
        }

        if (filesToPack.Count == 0)
            throw new ArgumentException("No files to archive.", nameof(sources));

        var totalBytes = filesToPack.Sum(f => new FileInfo(f.FullPath).Length);
        var processedBytes = 0L;

        using var fs = File.Create(outputPath);
        using var zos = new ZipOutputStream(fs);
        zos.SetLevel(Math.Clamp(options.CompressionLevel, 0, 9));
        if (!string.IsNullOrWhiteSpace(options.Password))
            zos.Password = options.Password;

        foreach (var (fullPath, relative) in filesToPack)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new ArchiveProgress(
                totalBytes == 0 ? 100 : (int)(processedBytes * 100 / totalBytes),
                relative, processedBytes, totalBytes));

            var entry = new ZipEntry(relative)
            {
                DateTime = File.GetLastWriteTime(fullPath),
                AESKeySize = string.IsNullOrWhiteSpace(options.Password) ? 0 : 256,
                CompressionMethod = options.CompressionLevel == 0 ? CompressionMethod.Stored : CompressionMethod.Deflated
            };

            zos.PutNextEntry(entry);

            using (var input = File.OpenRead(fullPath))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    zos.Write(buffer, 0, read);
                    processedBytes += read;
                    progress?.Report(new ArchiveProgress(
                        totalBytes == 0 ? 100 : (int)(processedBytes * 100 / totalBytes),
                        relative, processedBytes, totalBytes));
                }
            }

            zos.CloseEntry();
        }

        // Manifest marker for .zpx
        if (outputPath.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            var marker = new ZipEntry(MarkerEntryName) { CompressionMethod = CompressionMethod.Stored };
            zos.PutNextEntry(marker);
            using (var writer = new StreamWriter(zos, leaveOpen: true))
            {
                writer.WriteLine("zpX archive");
                writer.WriteLine("Format: ZIP");
                writer.WriteLine("Created: " + DateTime.UtcNow.ToString("O"));
                writer.Flush();
            }
            zos.CloseEntry();
        }

        zos.Finish();
        zos.Close();

        progress?.Report(new ArchiveProgress(100, string.Empty, totalBytes, totalBytes));
    }

    public static string EnsureExtension(string path)
        => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + Extension;
}
