using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

public sealed class ClipboardPayloadCapturedEventArgs : EventArgs
{
    public ClipboardPayloadCapturedEventArgs(CapturedClipboardPayload payload)
    {
        Payload = payload;
    }

    public CapturedClipboardPayload Payload { get; }
}
