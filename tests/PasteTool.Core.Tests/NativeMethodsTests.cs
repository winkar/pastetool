using System.Runtime.InteropServices;
using PasteTool.Core.Native;

namespace PasteTool.Core.Tests;

public sealed class NativeMethodsTests
{
    [Fact]
    public void InputStruct_SizeMatchesWin32Expectation()
    {
        var expectedSize = Environment.Is64BitProcess ? 40 : 28;

        Assert.Equal(expectedSize, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
