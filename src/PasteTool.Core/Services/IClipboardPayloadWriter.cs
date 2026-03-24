using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

internal interface IClipboardPayloadWriter
{
    void Write(CapturedClipboardPayload payload, bool copy);
}
