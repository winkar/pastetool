using PasteTool.Core.Models;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Services;

internal sealed class DelayedClipboardPasteTransport : IClipboardTransport, IDisposable
{
    private readonly StaDispatcher _dispatcher;
    private readonly IClipboardPayloadWriter _clipboardWriter;
    private readonly ILogger _logger;
    private readonly bool _ownsDispatcher;

    public DelayedClipboardPasteTransport(ILogger logger)
        : this(logger, new StaDispatcher("PasteTool.ClipboardTransport"), new ClipboardPayloadWriterAdapter(), ownsDispatcher: true)
    {
    }

    internal DelayedClipboardPasteTransport(
        ILogger logger,
        StaDispatcher dispatcher,
        IClipboardPayloadWriter clipboardWriter,
        bool ownsDispatcher = false)
    {
        _logger = logger;
        _dispatcher = dispatcher;
        _clipboardWriter = clipboardWriter;
        _ownsDispatcher = ownsDispatcher;
    }

    public async Task SetClipboardPayloadAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Exception? primaryException = null;

        try
        {
            await _dispatcher.InvokeAsync(() => _clipboardWriter.Write(payload, copy: false));
            return;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            primaryException = ex;
            _logger.LogWarning("Delayed clipboard path failed; falling back to eager clipboard write.", ex);
        }

        try
        {
            await _dispatcher.InvokeAsync(() => _clipboardWriter.Write(payload, copy: true));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception fallbackException)
        {
            _logger.LogError("Fallback clipboard write failed.", fallbackException);
            throw new InvalidOperationException("Clipboard transport failed on both delayed and eager paths.", new AggregateException(primaryException!, fallbackException));
        }
    }

    public void Dispose()
    {
        if (_ownsDispatcher)
        {
            _dispatcher.Dispose();
        }
    }
}
