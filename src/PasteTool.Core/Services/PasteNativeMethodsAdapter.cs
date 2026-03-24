using PasteTool.Core.Native;

namespace PasteTool.Core.Services;

internal sealed class PasteNativeMethodsAdapter : IPasteNativeMethods
{
    public IntPtr GetForegroundWindow() => NativeMethods.GetForegroundWindow();

    public bool IsWindow(IntPtr hWnd) => NativeMethods.IsWindow(hWnd);

    public bool ShowWindowAsync(IntPtr hWnd, int nCmdShow) => NativeMethods.ShowWindowAsync(hWnd, nCmdShow);

    public bool GetWindowPlacement(IntPtr hWnd, ref NativeMethods.WINDOWPLACEMENT placement) => NativeMethods.GetWindowPlacement(hWnd, ref placement);

    public uint GetCurrentThreadId() => NativeMethods.GetCurrentThreadId();

    public uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId) => NativeMethods.GetWindowThreadProcessId(hWnd, out processId);

    public bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach) => NativeMethods.AttachThreadInput(idAttach, idAttachTo, attach);

    public bool BringWindowToTop(IntPtr hWnd) => NativeMethods.BringWindowToTop(hWnd);

    public bool SetForegroundWindow(IntPtr hWnd) => NativeMethods.SetForegroundWindow(hWnd);

    public short GetAsyncKeyState(int vKey) => NativeMethods.GetAsyncKeyState(vKey);

    public uint SendInput(uint nInputs, NativeMethods.INPUT[] inputs, int size) => NativeMethods.SendInput(nInputs, inputs, size);
}
