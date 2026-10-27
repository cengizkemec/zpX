using System;

namespace zpX.UI.Models;

public sealed class FileItem
{
    public string FullName { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public long CompressedSize { get; set; }
    public DateTime? Modified { get; set; }
    public string SizeText { get; set; } = string.Empty;
    public string PackedText { get; set; } = string.Empty;
    public string TypeText { get; set; } = string.Empty;
    public string ModifiedText { get; set; } = string.Empty;

    public string DisplayName => IsDirectory ? FullName.TrimEnd('/') : FullName;

    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE7C3";
}
