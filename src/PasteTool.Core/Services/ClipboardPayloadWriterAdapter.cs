using PasteTool.Core.Models;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Services;

internal sealed class ClipboardPayloadWriterAdapter : IClipboardPayloadWriter
{
    public void Write(CapturedClipboardPayload payload, bool copy)
    {
        ClipboardPayloadWriter.Write(payload, copy);
    }
}
