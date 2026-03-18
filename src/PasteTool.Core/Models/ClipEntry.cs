namespace PasteTool.Core.Models;

public sealed class ClipEntry
{
    public long Id { get; init; }

    public ClipKind Kind { get; init; }

    public DateTime CapturedAtUtc { get; init; }

    public string SearchText { get; init; } = string.Empty;

    public string PreviewText { get; init; } = string.Empty;

    public string ContentHash { get; init; } = string.Empty;

    public string Formats { get; init; } = string.Empty;

    public string? BlobPath { get; init; }

    public string? ThumbnailPath { get; init; }

    public long BlobSizeBytes { get; init; }

    public int? ImagePixelWidth { get; init; }

    public int? ImagePixelHeight { get; init; }
}
