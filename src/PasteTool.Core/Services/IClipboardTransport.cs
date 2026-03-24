using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

internal interface IClipboardTransport
{
    Task SetClipboardPayloadAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken = default);
}
