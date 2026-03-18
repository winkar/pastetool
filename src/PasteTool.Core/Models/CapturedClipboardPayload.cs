namespace PasteTool.Core.Models;

public sealed class CapturedClipboardPayload
{
    public string? UnicodeText { get; init; }

    public string? Rtf { get; init; }

    public string? Html { get; init; }

    public byte[]? ImageBytes { get; init; }

    public int? ImagePixelWidth { get; init; }

    public int? ImagePixelHeight { get; init; }

    public IReadOnlyList<string> SourceFormats { get; init; } = Array.Empty<string>();

    public bool HasContent =>
        !string.IsNullOrWhiteSpace(UnicodeText) ||
        !string.IsNullOrWhiteSpace(Rtf) ||
        !string.IsNullOrWhiteSpace(Html) ||
        (ImageBytes is { Length: > 0 });
}
