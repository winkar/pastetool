using System.Runtime.InteropServices;
using PasteTool.Core.Models;
using PasteTool.Core.Services;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Tests;

public sealed class DelayedClipboardPasteTransportTests
{
    [Fact]
    public async Task SetClipboardPayloadAsync_UsesOleClipboard_WhenPrimaryPathSucceeds()
    {
        var logger = new TestLogger();
        using var dispatcher = new StaDispatcher("PasteTool.TransportTests");
        var clipboardWriter = new FakeClipboardPayloadWriter();
        using var transport = new DelayedClipboardPasteTransport(logger, dispatcher, clipboardWriter);
        var payload = CreatePayload();

        await transport.SetClipboardPayloadAsync(payload);

        Assert.Equal(1, clipboardWriter.WriteCalls);
        Assert.False(clipboardWriter.CopyFlags.Single());
    }

    [Fact]
    public async Task SetClipboardPayloadAsync_FallsBackToEagerClipboard_WhenOleClipboardFails()
    {
        var logger = new TestLogger();
        using var dispatcher = new StaDispatcher("PasteTool.TransportFallbackTests");
        var clipboardWriter = new FakeClipboardPayloadWriter { FailOnFirstWrite = new COMException("boom") };
        using var transport = new DelayedClipboardPasteTransport(logger, dispatcher, clipboardWriter);
        var payload = CreatePayload();

        await transport.SetClipboardPayloadAsync(payload);

        Assert.Equal(2, clipboardWriter.WriteCalls);
        Assert.Equal(new[] { false, true }, clipboardWriter.CopyFlags);
        Assert.Same(payload, clipboardWriter.LastPayload);
    }

    [Fact]
    public async Task SetClipboardPayloadAsync_FallsBackToEagerClipboard_ForNonComExceptions()
    {
        var logger = new TestLogger();
        using var dispatcher = new StaDispatcher("PasteTool.TransportFallbackTests2");
        var clipboardWriter = new FakeClipboardPayloadWriter { FailOnFirstWrite = new InvalidCastException("boom") };
        using var transport = new DelayedClipboardPasteTransport(logger, dispatcher, clipboardWriter);
        var payload = CreatePayload();

        await transport.SetClipboardPayloadAsync(payload);

        Assert.Equal(2, clipboardWriter.WriteCalls);
        Assert.Equal(new[] { false, true }, clipboardWriter.CopyFlags);
        Assert.Same(payload, clipboardWriter.LastPayload);
    }

    [Fact]
    public void CreateDataObject_ExposesExpectedFormats()
    {
        var payload = CreatePayload();

        var dataObject = ClipboardPayloadWriter.CreateDataObject(payload);

        Assert.True(dataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText));
        Assert.True(dataObject.GetDataPresent(System.Windows.DataFormats.Text));
        Assert.True(dataObject.GetDataPresent(System.Windows.DataFormats.Rtf));
        Assert.True(dataObject.GetDataPresent(System.Windows.DataFormats.Html));
        Assert.True(dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap));
    }

    private static CapturedClipboardPayload CreatePayload()
    {
        return new CapturedClipboardPayload
        {
            UnicodeText = "hello",
            Rtf = @"{\rtf1\ansi hello}",
            Html = "<html><body><b>hello</b></body></html>",
            ImageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9p8fBzEAAAAASUVORK5CYII="),
            ImagePixelWidth = 1,
            ImagePixelHeight = 1,
        };
    }

    private sealed class FakeClipboardPayloadWriter : IClipboardPayloadWriter
    {
        public int WriteCalls { get; private set; }
        public CapturedClipboardPayload? LastPayload { get; private set; }
        public List<bool> CopyFlags { get; } = new();
        public Exception? FailOnFirstWrite { get; set; }

        public void Write(CapturedClipboardPayload payload, bool copy)
        {
            WriteCalls++;
            LastPayload = payload;
            CopyFlags.Add(copy);

            if (WriteCalls == 1 && FailOnFirstWrite is not null)
            {
                throw FailOnFirstWrite;
            }
        }
    }
}
