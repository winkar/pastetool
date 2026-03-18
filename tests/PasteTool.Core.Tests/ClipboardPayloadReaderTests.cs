using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Tests;

public sealed class ClipboardPayloadReaderTests
{
    [Fact]
    public void ReadFromDataObject_PrefersRawPngWhenBitmapFallbackIsBlank()
    {
        using var dispatcher = new StaDispatcher("clipboard-reader-test");

        var payload = dispatcher.Invoke(() =>
        {
            var dataObject = new DataObject();
            var expected = CreateBitmap(2, 1, [255, 0, 0, 255, 0, 255, 0, 255]);
            var fallback = CreateBitmap(2, 1, [255, 255, 255, 255, 255, 255, 255, 255]);
            var pngBytes = ImageUtilities.EncodePng(expected);

            dataObject.SetData("image/png", new MemoryStream(pngBytes), false);
            dataObject.SetData(DataFormats.Bitmap, fallback);

            return ClipboardPayloadReader.ReadFromDataObject(dataObject);
        });

        Assert.NotNull(payload);
        Assert.NotNull(payload!.ImageBytes);

        var actualPixels = dispatcher.Invoke(() => GetPixels(ImageUtilities.DecodePng(payload.ImageBytes!)));
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }, actualPixels);
        Assert.Equal(2, payload.ImagePixelWidth);
        Assert.Equal(1, payload.ImagePixelHeight);
    }

    [Fact]
    public void ReadFromDataObject_FallsBackToBitmapWhenRawPngIsMissing()
    {
        using var dispatcher = new StaDispatcher("clipboard-reader-test");

        var payload = dispatcher.Invoke(() =>
        {
            var dataObject = new DataObject();
            var expected = CreateBitmap(2, 1, [10, 20, 30, 255, 40, 50, 60, 255]);

            dataObject.SetData(DataFormats.Bitmap, expected);

            return ClipboardPayloadReader.ReadFromDataObject(dataObject);
        });

        Assert.NotNull(payload);
        Assert.NotNull(payload!.ImageBytes);

        var actualPixels = dispatcher.Invoke(() => GetPixels(ImageUtilities.DecodePng(payload.ImageBytes!)));
        Assert.Equal(new byte[] { 10, 20, 30, 255, 40, 50, 60, 255 }, actualPixels);
        Assert.Equal(2, payload.ImagePixelWidth);
        Assert.Equal(1, payload.ImagePixelHeight);
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] GetPixels(BitmapSource? bitmapSource)
    {
        Assert.NotNull(bitmapSource);

        if (bitmapSource!.Format != PixelFormats.Bgra32)
        {
            bitmapSource = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, null, 0);
            bitmapSource.Freeze();
        }

        var pixels = new byte[bitmapSource.PixelWidth * bitmapSource.PixelHeight * 4];
        bitmapSource.CopyPixels(pixels, bitmapSource.PixelWidth * 4, 0);
        return pixels;
    }
}
