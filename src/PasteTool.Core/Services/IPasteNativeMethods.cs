using PasteTool.Core.Native;

namespace PasteTool.Core.Services;

internal interface IPasteNativeMethods
{
    IntPtr GetForegroundWindow();
    bool IsWindow(IntPtr hWnd);
    bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    bool GetWindowPlacement(IntPtr hWnd, ref NativeMethods.WINDOWPLACEMENT placement);
    uint GetCurrentThreadId();
    uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    bool BringWindowToTop(IntPtr hWnd);
    bool SetForegroundWindow(IntPtr hWnd);
    short GetAsyncKeyState(int vKey);
    uint SendInput(uint nInputs, NativeMethods.INPUT[] inputs, int size);
}
