namespace zpX.Core.Abstractions;

public interface IArchiveReader : IDisposable
{
    IReadOnlyList<ArchiveEntry> Entries { get; }

    /// <summary>Reads every entry to the end and verifies the archive can be fully decoded.</summary>
    ArchiveTestResult Test(IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Extracts all entries into <paramref name="destination"/>.</summary>
    void ExtractAll(string destination, IProgress<string>? progress = null, CancellationToken cancellationToken = default);

    Stream OpenEntry(ArchiveEntry entry);
}

public sealed record ArchiveTestResult(bool Success, int CheckedEntries, string? Error);
