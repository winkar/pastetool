using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

public sealed class SaveClipResult
{
    public required ClipEntry Entry { get; init; }

    public IReadOnlyList<long> RemovedEntryIds { get; init; } = Array.Empty<long>();
}
