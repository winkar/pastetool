using PasteTool.Core.Models;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Services;

public sealed class ClipboardHistoryManager : IDisposable
{
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly IClipRepository _clipRepository;
    private readonly IPasteService _pasteService;
    private readonly ILogger _logger;
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private IReadOnlyList<ClipEntry> _entries = Array.Empty<ClipEntry>();
    private string? _suppressedHash;
    private DateTime _suppressedHashExpiresUtc;
    private CancellationTokenSource? _cancellationSource;

    public ClipboardHistoryManager(
        IClipboardMonitor clipboardMonitor,
        IClipRepository clipRepository,
        IPasteService pasteService,
        ILogger logger)
    {
        _clipboardMonitor = clipboardMonitor;
        _clipRepository = clipRepository;
        _pasteService = pasteService;
        _logger = logger;
    }

    public event EventHandler<ClipboardHistoryChangedEventArgs>? HistoryChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _cancellationSource = new CancellationTokenSource();

        try
        {
            await _clipRepository.InitializeAsync(cancellationToken);
            var entries = await _clipRepository.LoadEntriesAsync(cancellationToken);
            UpdateEntries(entries);
            _clipboardMonitor.ClipboardCaptured += OnClipboardCaptured;
            _clipboardMonitor.Start();
            _logger.LogInfo("ClipboardHistoryManager initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to initialize ClipboardHistoryManager", ex);
            throw;
        }
    }

    public IReadOnlyList<ClipEntry> GetEntriesSnapshot()
    {
        lock (_syncRoot)
        {
            return _entries.ToArray();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _clipRepository.ClearAsync(cancellationToken);
        UpdateEntries(Array.Empty<ClipEntry>());
    }

    public async Task PasteAsync(ClipEntry entry, IntPtr targetWindowHandle, CancellationToken cancellationToken = default)
    {
        var payload = await _clipRepository.LoadPayloadAsync(entry, cancellationToken);
        if (payload is null)
        {
            return;
        }

        MarkSuppressed(entry.ContentHash);
        await _pasteService.PasteAsync(payload, targetWindowHandle, cancellationToken);
    }

    private void OnClipboardCaptured(object? sender, ClipboardPayloadCapturedEventArgs e)
    {
        if (_cancellationSource?.IsCancellationRequested == true)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessCaptureAsync(e.Payload, _cancellationSource?.Token ?? default);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unhandled error in clipboard capture processing", ex);
            }
        });
    }

    private async Task ProcessCaptureAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken)
    {
        var hash = ContentHasher.Compute(payload);
        if (TryConsumeSuppression(hash))
        {
            return;
        }

        await _processingGate.WaitAsync(cancellationToken);
        try
        {
            SaveClipResult? saveResult;
            try
            {
                saveResult = await _clipRepository.SaveAsync(payload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save clipboard entry to repository", ex);
                return;
            }

            if (saveResult is null)
            {
                return;
            }

            lock (_syncRoot)
            {
                var removed = saveResult.RemovedEntryIds.Count == 0
                    ? null
                    : saveResult.RemovedEntryIds.ToHashSet();

                var updatedEntries = _entries
                    .Where(entry => entry.Id != saveResult.Entry.Id && (removed is null || !removed.Contains(entry.Id)))
                    .ToList();

                updatedEntries.Insert(0, saveResult.Entry);
                _entries = updatedEntries;
            }

            RaiseHistoryChanged();
        }
        finally
        {
            _processingGate.Release();
        }
    }

    private void MarkSuppressed(string hash)
    {
        lock (_syncRoot)
        {
            _suppressedHash = hash;
            _suppressedHashExpiresUtc = DateTime.UtcNow.AddSeconds(3);
        }
    }

    private bool TryConsumeSuppression(string hash)
    {
        lock (_syncRoot)
        {
            if (_suppressedHash is null)
            {
                return false;
            }

            if (_suppressedHashExpiresUtc < DateTime.UtcNow)
            {
                _suppressedHash = null;
                return false;
            }

            if (!string.Equals(_suppressedHash, hash, StringComparison.Ordinal))
            {
                return false;
            }

            _suppressedHash = null;
            return true;
        }
    }

    private void UpdateEntries(IReadOnlyList<ClipEntry> entries)
    {
        lock (_syncRoot)
        {
            _entries = entries.ToArray();
        }

        RaiseHistoryChanged();
    }

    private void RaiseHistoryChanged()
    {
        var handler = HistoryChanged;
        if (handler is null)
        {
            return;
        }

        handler(this, new ClipboardHistoryChangedEventArgs(GetEntriesSnapshot()));
    }

    public void Dispose()
    {
        _cancellationSource?.Cancel();
        _clipboardMonitor.ClipboardCaptured -= OnClipboardCaptured;
        _clipboardMonitor.Dispose();
        _clipRepository.Dispose();
        _processingGate.Dispose();
        _cancellationSource?.Dispose();
    }
}
