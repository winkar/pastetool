using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

public interface IClipRepository : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClipEntry>> LoadEntriesAsync(CancellationToken cancellationToken = default);

    Task<SaveClipResult?> SaveAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken = default);

    Task<CapturedClipboardPayload?> LoadPayloadAsync(ClipEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClipEntry>> SearchEntriesAsync(string query, int limit, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
