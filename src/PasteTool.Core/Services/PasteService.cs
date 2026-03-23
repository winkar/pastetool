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
                    _logger.LogWarning($"Target window handle is invalid: 0x{targetWindowHandle.ToInt64():X}");
                    await SendPasteKeysAsync(cancellationToken);
                    return;
                }

                _logger.LogInfo($"Attempting paste to target window 0x{targetWindowHandle.ToInt64():X}");

                var placement = new NativeMethods.WINDOWPLACEMENT
                {
                    length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>()
                };
                NativeMethods.GetWindowPlacement(targetWindowHandle, ref placement);
                if (placement.showCmd == NativeMethods.SwShowMinimized)
                {
                    NativeMethods.ShowWindowAsync(targetWindowHandle, NativeMethods.SwRestore);
                    await Task.Delay(30, cancellationToken);
                }

                await RestoreTargetWindowAsync(targetWindowHandle, cancellationToken);
            }

            await SendPasteKeysAsync(cancellationToken);
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

    private async Task RestoreTargetWindowAsync(IntPtr targetWindowHandle, CancellationToken cancellationToken)
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var currentThreadId = NativeMethods.GetCurrentThreadId();
        var targetThreadId = NativeMethods.GetWindowThreadProcessId(targetWindowHandle, out _);
        var foregroundThreadId = foregroundWindow != IntPtr.Zero
            ? NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _)
            : 0;

        var attachedToForeground = false;
        var attachedToTarget = false;

        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attachedToForeground = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attachedToTarget = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            for (var attempt = 0; attempt < 6; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NativeMethods.BringWindowToTop(targetWindowHandle);
                NativeMethods.SetForegroundWindow(targetWindowHandle);

                await Task.Delay(40, cancellationToken);

                if (NativeMethods.GetForegroundWindow() == targetWindowHandle)
                {
                    _logger.LogInfo($"Foreground restored to target window 0x{targetWindowHandle.ToInt64():X} on attempt {attempt + 1}");
                    return;
                }
            }

            var actualForegroundWindow = NativeMethods.GetForegroundWindow();
            _logger.LogWarning(
                $"Failed to restore target window before paste. Target=0x{targetWindowHandle.ToInt64():X}, Foreground=0x{actualForegroundWindow.ToInt64():X}");
        }
        finally
        {
            if (attachedToTarget)
            {
                NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
            }

            if (attachedToForeground)
            {
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    private async Task SendPasteKeysAsync(CancellationToken cancellationToken)
    {
        ReleaseModifierKeys();
        await Task.Delay(30, cancellationToken);

        var inputs = new[]
        {
            CreateKeyInput(NativeMethods.VkControl, false),
            CreateKeyInput(NativeMethods.VkV, false),
            CreateKeyInput(NativeMethods.VkV, true),
            CreateKeyInput(NativeMethods.VkControl, true),
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogWarning($"SendInput sent {sent}/{inputs.Length} keyboard events; Win32Error={error}");
        }
        else
        {
            _logger.LogInfo("SendInput sent Ctrl+V successfully");
        }
    }

    private void ReleaseModifierKeys()
    {
        var pressedModifiers = new List<ushort>();
        var modifierKeys = new[]
        {
            NativeMethods.VkShift,
            NativeMethods.VkControl,
            NativeMethods.VkMenu,
            NativeMethods.VkLwin,
            NativeMethods.VkRwin,
        };

        foreach (var key in modifierKeys)
        {
            if ((NativeMethods.GetAsyncKeyState(key) & 0x8000) == 0)
            {
                continue;
            }

            pressedModifiers.Add(key);
        }

        if (pressedModifiers.Count == 0)
        {
            return;
        }

        var releaseInputs = pressedModifiers
            .Select(key => CreateKeyInput(key, true))
            .ToArray();

        var sent = NativeMethods.SendInput((uint)releaseInputs.Length, releaseInputs, Marshal.SizeOf<NativeMethods.INPUT>());
        var error = sent == releaseInputs.Length ? 0 : Marshal.GetLastWin32Error();
        _logger.LogInfo($"Released modifiers before paste: {string.Join(", ", pressedModifiers.Select(DescribeVirtualKey))}; sent {sent}/{releaseInputs.Length} events; Win32Error={error}");
    }

    private static string DescribeVirtualKey(ushort key)
    {
        return key switch
        {
            NativeMethods.VkShift => "Shift",
            NativeMethods.VkControl => "Ctrl",
            NativeMethods.VkMenu => "Alt",
            NativeMethods.VkLwin => "LWin",
            NativeMethods.VkRwin => "RWin",
            _ => $"0x{key:X2}"
        };
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
                    dwFlags = keyUp ? NativeMethods.KeyeventfKeyUp : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero,
                },
            },
        };
    }
}
