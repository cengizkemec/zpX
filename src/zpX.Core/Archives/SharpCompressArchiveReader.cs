using SharpCompress.Archives;
using SharpCompress.Readers;
using zpX.Core.Abstractions;

namespace zpX.Core.Archives;

public sealed class SharpCompressArchiveReader : IArchiveReader
{
    private readonly IArchive _archive;
    private readonly Dictionary<string, IArchiveEntry> _index = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public SharpCompressArchiveReader(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Archive not found.", path);

        _archive = ArchiveFactory.OpenArchive(path, new ReaderOptions());

        var entries = new List<ArchiveEntry>();
        foreach (var entry in _archive.Entries)
        {
            var key = NormalizeKey(entry.Key);
            _index[key] = entry;

            entries.Add(new ArchiveEntry
            {
                Key = key,
                FullName = key,
                Name = GetLeafName(key, entry.IsDirectory),
                IsDirectory = entry.IsDirectory,
                Size = entry.Size,
                CompressedSize = entry.CompressedSize,
                LastModified = entry.LastModifiedTime
            });
        }

        Entries = entries;
    }

    public IReadOnlyList<ArchiveEntry> Entries { get; }

    public ArchiveTestResult Test(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[81920];
        var checkedEntries = 0;

        try
        {
            foreach (var entry in Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.IsDirectory) continue;

                progress?.Report(entry.FullName);
                if (!_index.TryGetValue(entry.Key, out var source)) continue;

                using var stream = source.OpenEntryStream();
                while (stream.Read(buffer, 0, buffer.Length) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                checkedEntries++;
            }

            return new ArchiveTestResult(true, checkedEntries, null);
        }
        catch (Exception ex)
        {
            return new ArchiveTestResult(false, checkedEntries, ex.Message);
        }
    }

    public void ExtractAll(string destination, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destination);
        var root = Path.GetFullPath(destination);

        foreach (var entry in Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_index.TryGetValue(entry.Key, out var source)) continue;

            var target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Blocked unsafe archive path: " + entry.FullName);

            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            progress?.Report(entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            using var input = source.OpenEntryStream();
            using var output = File.Create(target);
            input.CopyTo(output);
        }
    }

    public Stream OpenEntry(ArchiveEntry entry)
    {
        if (!_index.TryGetValue(entry.Key, out var source))
            throw new FileNotFoundException(entry.Key);

        var buffer = new MemoryStream();
        using (var stream = source.OpenEntryStream())
        {
            stream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static string NormalizeKey(string? key)
        => string.IsNullOrEmpty(key) ? string.Empty : key.Replace('\\', '/').TrimStart('/');

    private static string GetLeafName(string key, bool isDirectory)
    {
        var trimmed = key.TrimEnd('/');
        var slash = trimmed.LastIndexOf('/');
        var name = slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
        return string.IsNullOrEmpty(name) ? (isDirectory ? "/" : key) : name;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _archive.Dispose();
    }
}
