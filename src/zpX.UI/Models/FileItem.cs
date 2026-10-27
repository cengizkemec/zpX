using System;

namespace zpX.UI.Models;

public sealed class FileItem
{
    public string FullName { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public string SizeText { get; init; } = string.Empty;
    public string PackedText { get; init; } = string.Empty;
    public string TypeText { get; init; } = string.Empty;
    public string ModifiedText { get; init; } = string.Empty;

    public string DisplayName => IsDirectory ? FullName.TrimEnd('/') : FullName;

    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE7C3";
}
