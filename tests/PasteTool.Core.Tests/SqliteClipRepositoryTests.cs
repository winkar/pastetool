using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PasteTool.Core.Models;
using PasteTool.Core.Services;

namespace PasteTool.Core.Tests;

public sealed class SqliteClipRepositoryTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), "PasteToolTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_DeduplicatesMatchingPayloads()
    {
        var settings = CreateSettings(maxEntries: 10, maxImageCacheMb: 512);
        using var repository = CreateRepository(settings);
        await repository.InitializeAsync();

        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "same value" });
        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "same value" });

        var entries = await repository.LoadEntriesAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("same value", entry.SearchText);
    }

    [Fact]
    public async Task SaveAsync_TrimRemovesOldEntriesBeyondLimit()
    {
        var settings = CreateSettings(maxEntries: 2, maxImageCacheMb: 512);
        using var repository = CreateRepository(settings);
        await repository.InitializeAsync();

        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "first" });
        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "second" });
        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "third" });

        var entries = await repository.LoadEntriesAsync();

        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { "third", "second" }, entries.Select(entry => entry.SearchText));
    }

    [Fact]
    public async Task SaveAsync_TrimRemovesOldImagesWhenImageCacheLimitIsExceeded()
    {
        var settings = CreateSettings(maxEntries: 10, maxImageCacheMb: 64);
        using var repository = CreateRepository(settings);
        await repository.InitializeAsync();

        for (var index = 0; index < 6; index++)
        {
            var width = 1800 + index;
            var imageBytes = CreateNoisePng(width, 1800, index + 1);
            await repository.SaveAsync(new CapturedClipboardPayload
            {
                ImageBytes = imageBytes,
                ImagePixelWidth = width,
                ImagePixelHeight = 1800,
            });
        }

        var entries = await repository.LoadEntriesAsync();

        Assert.True(entries.Count < 6);
        Assert.All(entries, entry => Assert.Equal(ClipKind.Image, entry.Kind));
        Assert.Equal(1805, entries[0].ImagePixelWidth);
    }

    [Fact]
    public async Task ClearAsync_RemovesRowsAndBlobFiles()
    {
        var settings = CreateSettings(maxEntries: 10, maxImageCacheMb: 512);
        using var repository = CreateRepository(settings);
        await repository.InitializeAsync();

        await repository.SaveAsync(new CapturedClipboardPayload
        {
            UnicodeText = "hello",
            Rtf = @"{\rtf1\ansi hello}",
        });

        await repository.ClearAsync();

        var entries = await repository.LoadEntriesAsync();
        Assert.Empty(entries);
        Assert.Empty(Directory.GetFiles(Path.Combine(_rootDirectory, "blobs")));
    }

    [Fact]
    public async Task SearchEntriesAsync_ReturnsOnlyEntriesMatchingAllTokens()
    {
        var settings = CreateSettings(maxEntries: 10, maxImageCacheMb: 512);
        using var repository = CreateRepository(settings);
        await repository.InitializeAsync();

        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "alpha beta" });
        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "alpha only" });
        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "beta only" });

        var results = await repository.SearchEntriesAsync("alpha beta", 10);

        var matched = Assert.Single(results);
        Assert.Equal("alpha beta", matched.SearchText);
    }

    [Fact]
    public async Task SearchEntriesAsync_UsesPrefixMatching()
    {
        var settings = CreateSettings(maxEntries: 10, maxImageCacheMb: 512);
        using var repository = CreateRepository(settings);
        await repository.InitializeAsync();

        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "project roadmap" });
        await repository.SaveAsync(new CapturedClipboardPayload { UnicodeText = "release checklist" });

        var results = await repository.SearchEntriesAsync("proj road", 10);

        var matched = Assert.Single(results);
        Assert.Equal("project roadmap", matched.SearchText);
    }

    [Fact]
    public async Task SearchEntriesAsync_FindsImageEntriesByIndexedKeywords()
    {
        var settings = CreateSettings(maxEntries: 10, maxImageCacheMb: 512);
        using var repository = CreateRepository(settings);
        await repository.InitializeAsync();

        await repository.SaveAsync(new CapturedClipboardPayload
        {
            ImageBytes = CreateNoisePng(640, 480, 42),
            ImagePixelWidth = 640,
            ImagePixelHeight = 480,
        });

        var results = await repository.SearchEntriesAsync("png 640x480", 10);

        var matched = Assert.Single(results);
        Assert.Equal(ClipKind.Image, matched.Kind);
        Assert.Equal(640, matched.ImagePixelWidth);
        Assert.Equal(480, matched.ImagePixelHeight);
    }

    public void Dispose()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (!Directory.Exists(_rootDirectory))
            {
                return;
            }

            try
            {
                Directory.Delete(_rootDirectory, true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
        }
    }

    private SqliteClipRepository CreateRepository(AppSettings settings)
    {
        return new SqliteClipRepository(
            () => settings,
            Path.Combine(_rootDirectory, "history.db"),
            Path.Combine(_rootDirectory, "blobs"),
            Path.Combine(_rootDirectory, "thumbs"));
    }

    private static AppSettings CreateSettings(int maxEntries, int maxImageCacheMb)
    {
        var settings = new AppSettings
        {
            MaxEntries = maxEntries,
            MaxImageCacheMb = maxImageCacheMb,
        };
        settings.Normalize();
        return settings;
    }

    private static byte[] CreateNoisePng(int width, int height, int seed)
    {
        var bytesPerPixel = 4;
        var stride = width * bytesPerPixel;
        var pixels = new byte[stride * height];
        var random = new Random(seed);
        random.NextBytes(pixels);

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
