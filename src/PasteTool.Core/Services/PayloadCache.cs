using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

internal sealed class PayloadCache
{
    private const int DefaultMaxEntries = 32;
    private const long DefaultMaxBytes = 16L * 1024L * 1024L;
    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, LinkedListNode<PayloadCacheEntry>> _entriesByHash = new(StringComparer.Ordinal);
    private readonly LinkedList<PayloadCacheEntry> _lru = new();
    private long _totalBytes;

    public PayloadCache(int maxEntries = DefaultMaxEntries, long maxBytes = DefaultMaxBytes)
    {
        _maxEntries = maxEntries;
        _maxBytes = maxBytes;
    }

    public bool TryGet(string contentHash, out CapturedClipboardPayload? payload)
    {
        lock (_syncRoot)
        {
            if (!_entriesByHash.TryGetValue(contentHash, out var node))
            {
                payload = null;
                return false;
            }

            _lru.Remove(node);
            _lru.AddFirst(node);
            payload = node.Value.Payload;
            return true;
        }
    }

    public void Store(string contentHash, CapturedClipboardPayload payload)
    {
        var sizeBytes = EstimateSizeBytes(payload);

        lock (_syncRoot)
        {
            if (_entriesByHash.TryGetValue(contentHash, out var existing))
            {
                _totalBytes -= existing.Value.SizeBytes;
                existing.Value = new PayloadCacheEntry(contentHash, payload, sizeBytes);
                _lru.Remove(existing);
                _lru.AddFirst(existing);
            }
            else
            {
                var node = new LinkedListNode<PayloadCacheEntry>(new PayloadCacheEntry(contentHash, payload, sizeBytes));
                _entriesByHash.Add(contentHash, node);
                _lru.AddFirst(node);
            }

            _totalBytes += sizeBytes;
            TrimIfNeeded();
        }
    }

    private void TrimIfNeeded()
    {
        while (_entriesByHash.Count > _maxEntries || _totalBytes > _maxBytes)
        {
            var node = _lru.Last;
            if (node is null)
            {
                return;
            }

            _lru.RemoveLast();
            _entriesByHash.Remove(node.Value.ContentHash);
            _totalBytes -= node.Value.SizeBytes;
        }
    }

    private static long EstimateSizeBytes(CapturedClipboardPayload payload)
    {
        long sizeBytes = 0;
        sizeBytes += (payload.UnicodeText?.Length ?? 0) * sizeof(char);
        sizeBytes += (payload.Rtf?.Length ?? 0) * sizeof(char);
        sizeBytes += (payload.Html?.Length ?? 0) * sizeof(char);
        sizeBytes += payload.ImageBytes?.Length ?? 0;

        foreach (var format in payload.SourceFormats)
        {
            sizeBytes += format.Length * sizeof(char);
        }

        return Math.Max(sizeBytes, 1);
    }

    private sealed record PayloadCacheEntry(string ContentHash, CapturedClipboardPayload Payload, long SizeBytes);
}
