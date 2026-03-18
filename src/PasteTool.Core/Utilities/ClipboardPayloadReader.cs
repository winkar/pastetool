using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using PasteTool.Core.Models;

namespace PasteTool.Core.Utilities;

internal static class ClipboardPayloadReader
{
    private static readonly string[] PreferredImageFormats = ["PNG", "image/png"];

    public static CapturedClipboardPayload? TryRead()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var dataObject = Clipboard.GetDataObject();
                return dataObject is null ? null : ReadFromDataObject(dataObject);
            }
            catch (COMException)
            {
                if (attempt < 2)
                {
                    Thread.Sleep(50 * (attempt + 1)); // Exponential backoff: 50ms, 100ms
                }
            }
            catch (ExternalException)
            {
                if (attempt < 2)
                {
                    Thread.Sleep(50 * (attempt + 1));
                }
            }
        }

        return null;
    }

    internal static CapturedClipboardPayload? ReadFromDataObject(IDataObject dataObject)
    {
        string? unicodeText = null;
        if (dataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            unicodeText = dataObject.GetData(DataFormats.UnicodeText) as string;
        }
        else if (dataObject.GetDataPresent(DataFormats.Text))
        {
            unicodeText = dataObject.GetData(DataFormats.Text) as string;
        }

        var rtf = dataObject.GetDataPresent(DataFormats.Rtf)
            ? dataObject.GetData(DataFormats.Rtf) as string
            : null;
        var html = dataObject.GetDataPresent(DataFormats.Html)
            ? dataObject.GetData(DataFormats.Html) as string
            : null;

        var imageData = ReadImageData(dataObject);

        var payload = new CapturedClipboardPayload
        {
            UnicodeText = unicodeText,
            Rtf = rtf,
            Html = html,
            ImageBytes = imageData.Bytes,
            ImagePixelWidth = imageData.Width,
            ImagePixelHeight = imageData.Height,
            SourceFormats = dataObject.GetFormats(),
        };

        return payload.HasContent ? payload : null;
    }

    private static ImageReadResult ReadImageData(IDataObject dataObject)
    {
        foreach (var format in PreferredImageFormats)
        {
            if (TryReadPreferredImageFormat(dataObject, format, out var preferredImage))
            {
                return preferredImage;
            }
        }

        return TryReadBitmapFallback(dataObject, out var bitmapImage) ? bitmapImage : default;
    }

    private static bool TryReadPreferredImageFormat(IDataObject dataObject, string format, out ImageReadResult result)
    {
        result = default;

        if (!dataObject.GetDataPresent(format, autoConvert: false))
        {
            return false;
        }

        var data = dataObject.GetData(format, autoConvert: false);
        return TryCreateImageResult(data, out result);
    }

    private static bool TryReadBitmapFallback(IDataObject dataObject, out ImageReadResult result)
    {
        result = default;

        if (!dataObject.GetDataPresent(DataFormats.Bitmap))
        {
            return false;
        }

        var data = dataObject.GetData(DataFormats.Bitmap);
        return TryCreateImageResult(data, out result);
    }

    private static bool TryCreateImageResult(object? data, out ImageReadResult result)
    {
        result = default;

        return data switch
        {
            byte[] bytes => TryCreateImageResult(bytes, out result),
            MemoryStream memoryStream => TryCreateImageResult(ReadAllBytes(memoryStream), out result),
            Stream stream => TryCreateImageResult(ReadAllBytes(stream), out result),
            BitmapSource bitmapSource => TryCreateImageResult(bitmapSource, out result),
            _ => false,
        };
    }

    private static bool TryCreateImageResult(byte[] bytes, out ImageReadResult result)
    {
        result = default;

        if (bytes.Length == 0)
        {
            return false;
        }

        var image = ImageUtilities.DecodePng(bytes);
        if (image is null)
        {
            return false;
        }

        result = new ImageReadResult(bytes, image.PixelWidth, image.PixelHeight);
        return true;
    }

    private static bool TryCreateImageResult(BitmapSource bitmapSource, out ImageReadResult result)
    {
        result = default;

        if (bitmapSource.PixelWidth <= 0 || bitmapSource.PixelHeight <= 0)
        {
            return false;
        }

        result = new ImageReadResult(
            ImageUtilities.EncodePng(bitmapSource),
            bitmapSource.PixelWidth,
            bitmapSource.PixelHeight);
        return true;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private readonly record struct ImageReadResult(byte[]? Bytes, int? Width, int? Height);
}
