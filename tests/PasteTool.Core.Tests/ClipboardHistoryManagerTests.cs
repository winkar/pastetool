using PasteTool.Core.Models;
using PasteTool.Core.Services;

namespace PasteTool.Core.Tests;

public sealed class ClipboardHistoryManagerTests
{
    [Fact]
    public async Task LoadPayloadAsync_UsesCacheAfterFirstRepositoryRead()
    {
        var repository = new FakeClipRepository();
        var monitor = new FakeClipboardMonitor();
        var pasteService = new FakePasteService();
        var logger = new TestLogger();
        var entry = CreateEntry("hash-1");
        var payload = new CapturedClipboardPayload { UnicodeText = "cached value" };
        repository.LoadPayloadResult = payload;

        using var manager = new ClipboardHistoryManager(monitor, repository, pasteService, logger);

        var first = await manager.LoadPayloadAsync(entry);
        var second = await manager.LoadPayloadAsync(entry);

        Assert.Same(payload, first);
        Assert.Same(payload, second);
        Assert.Equal(1, repository.LoadPayloadCalls);
    }

    [Fact]
    public async Task PasteAsync_LoadsFromCache_WhenPayloadWasPreviouslyLoaded()
    {
        var repository = new FakeClipRepository();
        var monitor = new FakeClipboardMonitor();
        var pasteService = new FakePasteService();
        var logger = new TestLogger();
        var entry = CreateEntry("hash-2");
        var payload = new CapturedClipboardPayload { UnicodeText = "paste value" };
        repository.LoadPayloadResult = payload;

        using var manager = new ClipboardHistoryManager(monitor, repository, pasteService, logger);
        await manager.LoadPayloadAsync(entry);

        repository.LoadPayloadResult = new CapturedClipboardPayload { UnicodeText = "unexpected reload" };
        await manager.PasteAsync(entry, IntPtr.Zero);

        Assert.Equal(1, repository.LoadPayloadCalls);
        Assert.Same(payload, pasteService.LastPayload);
    }

    private static ClipEntry CreateEntry(string hash)
    {
        return new ClipEntry
        {
            Id = 1,
            Kind = ClipKind.Text,
            CapturedAtUtc = DateTime.UtcNow,
            SearchText = "text",
            PreviewText = "text",
            ContentHash = hash,
            Formats = "UnicodeText",
            BlobPath = "ignored",
        };
    }

    private sealed class FakeClipboardMonitor : IClipboardMonitor
    {
#pragma warning disable CS0067
        public event EventHandler<ClipboardPayloadCapturedEventArgs>? ClipboardCaptured;
#pragma warning restore CS0067

        public void Dispose()
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class FakeClipRepository : IClipRepository
    {
        public int LoadPayloadCalls { get; private set; }
        public CapturedClipboardPayload? LoadPayloadResult { get; set; }

        public void Dispose()
        {
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ClipEntry>> LoadEntriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ClipEntry>>(Array.Empty<ClipEntry>());

        public Task<SaveClipResult?> SaveAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken = default) => Task.FromResult<SaveClipResult?>(null);

        public Task<CapturedClipboardPayload?> LoadPayloadAsync(ClipEntry entry, CancellationToken cancellationToken = default)
        {
            LoadPayloadCalls++;
            return Task.FromResult(LoadPayloadResult);
        }

        public Task<IReadOnlyList<ClipEntry>> SearchEntriesAsync(string query, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ClipEntry>>(Array.Empty<ClipEntry>());

        public Task ClearAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePasteService : IPasteService
    {
        public CapturedClipboardPayload? LastPayload { get; private set; }

        public Task PasteAsync(CapturedClipboardPayload payload, IntPtr targetWindowHandle, CancellationToken cancellationToken = default)
        {
            LastPayload = payload;
            return Task.CompletedTask;
        }
    }
}
