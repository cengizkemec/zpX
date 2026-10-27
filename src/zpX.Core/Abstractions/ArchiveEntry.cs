namespace zpX.Core.Abstractions;

public sealed class ArchiveEntry
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public long CompressedSize { get; init; }
    public DateTime? LastModified { get; init; }
}
