using System.Windows.Input;
using System.Windows.Interop;
using PasteTool.Core.Models;
using PasteTool.Core.Native;

namespace PasteTool.App.Infrastructure;

public sealed class GlobalHotkeyManager : IDisposable
{
    private const int HotkeyId = 0x5041;
    private HwndSource? _source;
    private HotkeyGesture? _registeredGesture;

    public event EventHandler? HotkeyPressed;

    public bool Register(HotkeyGesture gesture)
    {
        EnsureWindow();
        Unregister();

        if (_source is null || gesture.Key == Key.None || gesture.Modifiers == ModifierKeys.None)
        {
            return false;
        }

        var success = NativeMethods.RegisterHotKey(
            _source.Handle,
            HotkeyId,
            ToNativeModifiers(gesture.Modifiers),
            (uint)KeyInterop.VirtualKeyFromKey(gesture.Key));

        if (success)
        {
            _registeredGesture = new HotkeyGesture
            {
                Key = gesture.Key,
                Modifiers = gesture.Modifiers,
            };
        }

        return success;
    }

    public void Dispose()
    {
        Unregister();

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private void EnsureWindow()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("PasteToolHotkeyWindow")
        {
            PositionX = -32000,
            PositionY = -32000,
            Width = 0,
            Height = 0,
            ParentWindow = NativeMethods.HwndMessage,
            WindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private void Unregister()
    {
        if (_source is not null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        }

        _registeredGesture = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotKey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        var value = 0u;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            value |= 0x0001;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            value |= 0x0002;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            value |= 0x0004;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            value |= 0x0008;
        }

        return value;
    }
}
