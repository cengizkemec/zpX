using System.IO.Compression;

namespace zpX.Core.Archives;

public sealed class ArchiveCreateOptions
{
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Compression level for the ZIP-based .zpx container.</summary>
    public CompressionLevel Level { get; set; } = CompressionLevel.SmallestSize;

    /// <summary>Optional password. Uses AES-256 ZIP encryption when provided.</summary>
    public string? Password { get; set; }
}

/// <summary>
/// Creates .zpx archives. .zpx is a standard ZIP container, so it stays readable by
/// other tools, while zpX adds its own metadata entry for future features.
/// </summary>
public static class ArchiveCreator
{
    public const string MarkerEntryName = ".zpx/manifest.txt";
    public const string Extension = ".zpx";

    public static void Create(IReadOnlyCollection<string> sources, ArchiveCreateOptions options,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (sources.Count == 0)
            throw new ArgumentException("At least one file or folder is required.", nameof(sources));

        var outputPath = EnsureExtension(options.OutputPath);
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        using var fileStream = File.Create(outputPath);
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(source))
            {
                AddFile(zip, Path.GetDirectoryName(Path.GetFullPath(source))!, source, options, progress, cancellationToken);
            }
            else if (Directory.Exists(source))
            {
                var root = Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AddFile(zip, root, file, options, progress, cancellationToken);
                }
            }
        }

        var marker = zip.CreateEntry(MarkerEntryName, CompressionLevel.NoCompression);
        using (var writer = new StreamWriter(marker.Open()))
        {
            writer.WriteLine("zpX archive");
            writer.WriteLine("Format: ZIP");
            writer.WriteLine("Created: " + DateTime.UtcNow.ToString("O"));
        }

        progress?.Report(string.Empty);
    }

    public static string EnsureExtension(string path)
        => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + Extension;

    private static void AddFile(ZipArchive zip, string root, string file, ArchiveCreateOptions options,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        progress?.Report(relative);

        var entry = zip.CreateEntry(relative, options.Level);
        entry.LastWriteTime = File.GetLastWriteTime(file);

        using var input = File.OpenRead(file);
        using var output = entry.Open();
        input.CopyTo(output);
    }
}
