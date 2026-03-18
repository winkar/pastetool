using System.Runtime.InteropServices;
using PasteTool.Core.Models;
using PasteTool.Core.Native;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Services;

public sealed class PasteService : IPasteService, IDisposable
{
    private readonly StaDispatcher _dispatcher = new("PasteTool.PasteWorker");
    private readonly ILogger _logger;

    public PasteService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task PasteAsync(CapturedClipboardPayload payload, IntPtr targetWindowHandle, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dispatcher.InvokeAsync(() => ClipboardPayloadWriter.Write(payload));
            await Task.Delay(50, cancellationToken);

            if (targetWindowHandle != IntPtr.Zero)
            {
                if (!NativeMethods.IsWindow(targetWindowHandle))
                {
                    _logger.LogWarning($"Target window handle is invalid: {targetWindowHandle}");
                    throw new InvalidOperationException("目标窗口无效");
                }

                NativeMethods.ShowWindowAsync(targetWindowHandle, NativeMethods.SwRestore);
                await Task.Delay(30, cancellationToken);

                if (!NativeMethods.SetForegroundWindow(targetWindowHandle))
                {
                    _logger.LogWarning($"Failed to set foreground window: {targetWindowHandle}");
                }

                await Task.Delay(30, cancellationToken);
            }

            SendPasteKeys();
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to paste clipboard content", ex);
            throw;
        }
    }

    public void Dispose()
    {
        _dispatcher.Dispose();
    }

    private static void SendPasteKeys()
    {
        var inputs = new[]
        {
            CreateKeyInput(0x11, false),
            CreateKeyInput(0x56, false),
            CreateKeyInput(0x56, true),
            CreateKeyInput(0x11, true),
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT CreateKeyInput(ushort virtualKey, bool keyUp)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.InputKeyboard,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = keyUp ? 0x0002u : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
    }
}
