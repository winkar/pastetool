using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

public interface IPasteService
{
    Task PasteAsync(CapturedClipboardPayload payload, IntPtr targetWindowHandle, CancellationToken cancellationToken = default);
}
