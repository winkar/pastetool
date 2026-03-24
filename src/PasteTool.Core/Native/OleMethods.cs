using System.Runtime.InteropServices;

namespace PasteTool.Core.Native;

internal static class OleMethods
{
    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(IntPtr pvReserved);

    [DllImport("ole32.dll")]
    internal static extern void OleUninitialize();
}
