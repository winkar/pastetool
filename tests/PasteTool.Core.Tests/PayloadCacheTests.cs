using PasteTool.Core.Models;
using PasteTool.Core.Services;

namespace PasteTool.Core.Tests;

public sealed class PayloadCacheTests
{
    [Fact]
    public void Store_ThenTryGet_ReturnsPayload()
    {
        var cache = new PayloadCache(maxEntries: 4, maxBytes: 1024);
        var payload = new CapturedClipboardPayload { UnicodeText = "hello" };

        cache.Store("hash-1", payload);

        var found = cache.TryGet("hash-1", out var cachedPayload);

        Assert.True(found);
        Assert.Same(payload, cachedPayload);
    }

    [Fact]
    public void Store_EvictsLeastRecentlyUsedEntry_WhenEntryLimitExceeded()
    {
        var cache = new PayloadCache(maxEntries: 2, maxBytes: 1024);
        cache.Store("hash-1", new CapturedClipboardPayload { UnicodeText = "one" });
        cache.Store("hash-2", new CapturedClipboardPayload { UnicodeText = "two" });

        Assert.True(cache.TryGet("hash-1", out _));

        cache.Store("hash-3", new CapturedClipboardPayload { UnicodeText = "three" });

        Assert.True(cache.TryGet("hash-1", out _));
        Assert.False(cache.TryGet("hash-2", out _));
        Assert.True(cache.TryGet("hash-3", out _));
    }
}
