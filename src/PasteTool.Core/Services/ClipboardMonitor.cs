using System.Windows.Interop;
using PasteTool.Core.Native;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Services;

public sealed class ClipboardMonitor : IClipboardMonitor
{
    private readonly StaDispatcher _dispatcher = new("PasteTool.ClipboardMonitor");
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly ILogger _logger;
    private HwndSource? _windowSource;
    private bool _isStarted;

    public ClipboardMonitor(ILogger logger)
    {
        _logger = logger;
    }

    public event EventHandler<ClipboardPayloadCapturedEventArgs>? ClipboardCaptured;

    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            if (_windowSource is not null)
            {
                return;
            }

            var parameters = new HwndSourceParameters("PasteToolClipboardMonitor")
            {
                PositionX = -32000,
                PositionY = -32000,
                Width = 0,
                Height = 0,
                ParentWindow = NativeMethods.HwndMessage,
                WindowStyle = 0,
            };

            _windowSource = new HwndSource(parameters);
            _windowSource.AddHook(WndProc);

            if (!NativeMethods.AddClipboardFormatListener(_windowSource.Handle))
            {
                _logger.LogError("Failed to register clipboard format listener");
                throw new InvalidOperationException("无法注册剪贴板监听器");
            }

            _logger.LogInfo("Clipboard monitor started successfully");
        });

        _isStarted = true;
    }

    public void Stop()
    {
        if (!_isStarted)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            if (_windowSource is null)
            {
                return;
            }

            if (!NativeMethods.RemoveClipboardFormatListener(_windowSource.Handle))
            {
                _logger.LogWarning("Failed to unregister clipboard format listener");
            }

            _windowSource.RemoveHook(WndProc);
            _windowSource.Dispose();
            _windowSource = null;
        });

        _isStarted = false;
        _logger.LogInfo("Clipboard monitor stopped");
    }

    public void Dispose()
    {
        Stop();
        _captureGate.Dispose();
        _dispatcher.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmClipboardUpdate)
        {
            _ = CaptureClipboardAsync();
        }

        return IntPtr.Zero;
    }

    private async Task CaptureClipboardAsync()
    {
        await _captureGate.WaitAsync();
        try
        {
            var payload = await _dispatcher.InvokeAsync(ClipboardPayloadReader.TryRead);
            if (payload is null)
            {
                return;
            }

            ClipboardCaptured?.Invoke(this, new ClipboardPayloadCapturedEventArgs(payload));
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to capture clipboard content", ex);
        }
        finally
        {
            _captureGate.Release();
        }
    }
}
