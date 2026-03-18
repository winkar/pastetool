namespace PasteTool.Core.Services;

public interface IClipboardMonitor : IDisposable
{
    event EventHandler<ClipboardPayloadCapturedEventArgs>? ClipboardCaptured;

    void Start();

    void Stop();
}
