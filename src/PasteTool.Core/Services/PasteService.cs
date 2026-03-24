using System.Runtime.InteropServices;
using PasteTool.Core.Models;
using PasteTool.Core.Native;

namespace PasteTool.Core.Services;

public sealed class PasteService : IPasteService, IDisposable
{
    private static readonly TimeSpan ForegroundWaitTimeout = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromMilliseconds(5);
    private readonly ILogger _logger;
    private readonly IClipboardTransport _clipboardTransport;
    private readonly IPasteNativeMethods _nativeMethods;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public PasteService(ILogger logger)
        : this(logger, new DelayedClipboardPasteTransport(logger), new PasteNativeMethodsAdapter(), Task.Delay)
    {
    }

    internal PasteService(
        ILogger logger,
        IClipboardTransport clipboardTransport,
        IPasteNativeMethods nativeMethods,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _logger = logger;
        _clipboardTransport = clipboardTransport;
        _nativeMethods = nativeMethods;
        _delayAsync = delayAsync;
    }

    public async Task PasteAsync(CapturedClipboardPayload payload, IntPtr targetWindowHandle, CancellationToken cancellationToken = default)
    {
        try
        {
            await _clipboardTransport.SetClipboardPayloadAsync(payload, cancellationToken);

            if (targetWindowHandle != IntPtr.Zero)
            {
                if (!_nativeMethods.IsWindow(targetWindowHandle))
                {
                    _logger.LogWarning($"Target window handle is invalid: 0x{targetWindowHandle.ToInt64():X}");
                }
                else
                {
                    _logger.LogInfo($"Attempting paste to target window 0x{targetWindowHandle.ToInt64():X}");
                    await TryRestoreWindowAsync(targetWindowHandle, cancellationToken);
                }
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
        if (_clipboardTransport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }

    private async Task<bool> TryRestoreWindowAsync(IntPtr targetWindowHandle, CancellationToken cancellationToken)
    {
        var placement = new NativeMethods.WINDOWPLACEMENT
        {
            length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>()
        };
        _nativeMethods.GetWindowPlacement(targetWindowHandle, ref placement);
        if (placement.showCmd == NativeMethods.SwShowMinimized)
        {
            _nativeMethods.ShowWindowAsync(targetWindowHandle, NativeMethods.SwRestore);
        }

        var foregroundWindow = _nativeMethods.GetForegroundWindow();
        if (foregroundWindow == targetWindowHandle)
        {
            return true;
        }

        var currentThreadId = _nativeMethods.GetCurrentThreadId();
        var targetThreadId = _nativeMethods.GetWindowThreadProcessId(targetWindowHandle, out _);
        var foregroundThreadId = foregroundWindow != IntPtr.Zero
            ? _nativeMethods.GetWindowThreadProcessId(foregroundWindow, out _)
            : 0;

        var attachedToForeground = false;
        var attachedToTarget = false;

        try
        {
            if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
            {
                attachedToForeground = _nativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attachedToTarget = _nativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            _nativeMethods.BringWindowToTop(targetWindowHandle);
            _nativeMethods.SetForegroundWindow(targetWindowHandle);

            if (_nativeMethods.GetForegroundWindow() == targetWindowHandle)
            {
                _logger.LogInfo($"Foreground restored to target window 0x{targetWindowHandle.ToInt64():X} immediately");
                return true;
            }

            var deadline = DateTime.UtcNow + ForegroundWaitTimeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _delayAsync(ForegroundPollInterval, cancellationToken);

                if (_nativeMethods.GetForegroundWindow() == targetWindowHandle)
                {
                    _logger.LogInfo($"Foreground restored to target window 0x{targetWindowHandle.ToInt64():X} after short wait");
                    return true;
                }
            }

            var actualForegroundWindow = _nativeMethods.GetForegroundWindow();
            _logger.LogWarning(
                $"Failed to restore target window before paste. Target=0x{targetWindowHandle.ToInt64():X}, Foreground=0x{actualForegroundWindow.ToInt64():X}");
            return false;
        }
        finally
        {
            if (attachedToTarget)
            {
                _nativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
            }

            if (attachedToForeground)
            {
                _nativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    private void SendPasteKeys()
    {
        var pressedModifiers = GetPressedModifierKeys();
        var inputs = new NativeMethods.INPUT[pressedModifiers.Count + 4];
        var index = 0;

        foreach (var modifierKey in pressedModifiers)
        {
            inputs[index++] = CreateKeyInput(modifierKey, keyUp: true);
        }

        inputs[index++] = CreateKeyInput(NativeMethods.VkControl, false);
        inputs[index++] = CreateKeyInput(NativeMethods.VkV, false);
        inputs[index++] = CreateKeyInput(NativeMethods.VkV, true);
        inputs[index] = CreateKeyInput(NativeMethods.VkControl, true);

        var sent = _nativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogWarning($"SendInput sent {sent}/{inputs.Length} keyboard events; Win32Error={error}");
            return;
        }

        if (pressedModifiers.Count > 0)
        {
            _logger.LogInfo($"Released modifiers before paste: {string.Join(", ", pressedModifiers.Select(DescribeVirtualKey))}");
        }

        _logger.LogInfo("SendInput sent Ctrl+V successfully");
    }

    private List<ushort> GetPressedModifierKeys()
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
            if ((_nativeMethods.GetAsyncKeyState(key) & 0x8000) == 0)
            {
                continue;
            }

            pressedModifiers.Add(key);
        }

        return pressedModifiers;
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
