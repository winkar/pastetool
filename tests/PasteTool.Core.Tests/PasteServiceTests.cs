using PasteTool.Core.Models;
using PasteTool.Core.Native;
using PasteTool.Core.Services;

namespace PasteTool.Core.Tests;

public sealed class PasteServiceTests
{
    [Fact]
    public async Task PasteAsync_DoesNotWait_WhenTargetWindowIsAlreadyForeground()
    {
        var logger = new TestLogger();
        var transport = new FakeClipboardTransport();
        var nativeMethods = new FakePasteNativeMethods
        {
            IsWindowResult = true,
            ForegroundWindow = new IntPtr(0x1234),
        };
        var delays = new List<TimeSpan>();
        using var service = new PasteService(logger, transport, nativeMethods, RecordDelay);

        await service.PasteAsync(new CapturedClipboardPayload { UnicodeText = "hello" }, new IntPtr(0x1234));

        Assert.Empty(delays);
        Assert.Equal(1, transport.SetClipboardCalls);
        Assert.Equal(1u, nativeMethods.SendInputCalls);
        return;

        Task RecordDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PasteAsync_SendsPaste_WhenTargetWindowHandleIsInvalid()
    {
        var logger = new TestLogger();
        var transport = new FakeClipboardTransport();
        var nativeMethods = new FakePasteNativeMethods
        {
            IsWindowResult = false,
        };
        using var service = new PasteService(logger, transport, nativeMethods, Task.Delay);

        await service.PasteAsync(new CapturedClipboardPayload { UnicodeText = "hello" }, new IntPtr(0x1234));

        Assert.Equal(1u, nativeMethods.SendInputCalls);
        Assert.Equal(0, nativeMethods.SetForegroundWindowCalls);
    }

    [Fact]
    public async Task PasteAsync_PollsBriefly_WhenForegroundWindowNeedsTimeToSwitch()
    {
        var logger = new TestLogger();
        var transport = new FakeClipboardTransport();
        var targetWindow = new IntPtr(0x2222);
        var nativeMethods = new FakePasteNativeMethods
        {
            IsWindowResult = true,
            ForegroundWindow = IntPtr.Zero,
            ForegroundWindowSequence = new Queue<IntPtr>(new[]
            {
                IntPtr.Zero,
                IntPtr.Zero,
                targetWindow,
            }),
        };
        var delays = new List<TimeSpan>();
        using var service = new PasteService(logger, transport, nativeMethods, RecordDelay);

        await service.PasteAsync(new CapturedClipboardPayload { UnicodeText = "hello" }, targetWindow);

        Assert.NotEmpty(delays);
        Assert.All(delays, delay => Assert.Equal(TimeSpan.FromMilliseconds(5), delay));
        Assert.Equal(1u, nativeMethods.SendInputCalls);
        return;

        Task RecordDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClipboardTransport : IClipboardTransport
    {
        public int SetClipboardCalls { get; private set; }

        public Task SetClipboardPayloadAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken = default)
        {
            SetClipboardCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasteNativeMethods : IPasteNativeMethods
    {
        public bool IsWindowResult { get; set; }
        public IntPtr ForegroundWindow { get; set; }
        public Queue<IntPtr>? ForegroundWindowSequence { get; set; }
        public int SetForegroundWindowCalls { get; private set; }
        public uint SendInputCalls { get; private set; }

        public IntPtr GetForegroundWindow()
        {
            if (ForegroundWindowSequence is { Count: > 0 })
            {
                ForegroundWindow = ForegroundWindowSequence.Dequeue();
            }

            return ForegroundWindow;
        }

        public bool IsWindow(IntPtr hWnd) => IsWindowResult;

        public bool ShowWindowAsync(IntPtr hWnd, int nCmdShow) => true;

        public bool GetWindowPlacement(IntPtr hWnd, ref NativeMethods.WINDOWPLACEMENT placement)
        {
            placement.showCmd = NativeMethods.SwShowNormal;
            return true;
        }

        public uint GetCurrentThreadId() => 1;

        public uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId)
        {
            processId = 1;
            return 1;
        }

        public bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach) => true;

        public bool BringWindowToTop(IntPtr hWnd) => true;

        public bool SetForegroundWindow(IntPtr hWnd)
        {
            SetForegroundWindowCalls++;
            return true;
        }

        public short GetAsyncKeyState(int vKey) => 0;

        public uint SendInput(uint nInputs, NativeMethods.INPUT[] inputs, int size)
        {
            SendInputCalls++;
            return nInputs;
        }
    }
}
