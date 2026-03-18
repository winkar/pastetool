using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PasteTool.Core.Utilities;

public static class ImageUtilities
{
    public static byte[] EncodePng(BitmapSource source)
    {
        if (source.CanFreeze)
        {
            source.Freeze();
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static BitmapSource? DecodePng(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.FirstOrDefault();
        if (frame is null)
        {
            return null;
        }

        if (frame.CanFreeze)
        {
            frame.Freeze();
        }

        return frame;
    }

    public static byte[] CreateThumbnail(byte[] originalBytes, int maxSize)
    {
        var source = DecodePng(originalBytes);
        if (source is null || source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            return originalBytes;
        }

        var scale = Math.Min(1d, Math.Min((double)maxSize / source.PixelWidth, (double)maxSize / source.PixelHeight));
        if (scale >= 1d)
        {
            return originalBytes;
        }

        var transform = new ScaleTransform(scale, scale);
        var scaled = new TransformedBitmap(source, transform);
        if (scaled.CanFreeze)
        {
            scaled.Freeze();
        }

        return EncodePng(scaled);
    }
}
