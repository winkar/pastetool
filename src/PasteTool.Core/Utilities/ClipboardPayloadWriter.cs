using System.Runtime.InteropServices;
using System.Windows;
using PasteTool.Core.Models;

namespace PasteTool.Core.Utilities;

internal static class ClipboardPayloadWriter
{
    public static DataObject CreateDataObject(CapturedClipboardPayload payload)
    {
        var dataObject = new DataObject();

        if (!string.IsNullOrWhiteSpace(payload.UnicodeText))
        {
            dataObject.SetData(DataFormats.UnicodeText, payload.UnicodeText);
            dataObject.SetData(DataFormats.Text, payload.UnicodeText);
        }

        if (!string.IsNullOrWhiteSpace(payload.Rtf))
        {
            dataObject.SetData(DataFormats.Rtf, payload.Rtf);
        }

        if (!string.IsNullOrWhiteSpace(payload.Html))
        {
            dataObject.SetData(DataFormats.Html, payload.Html);
        }

        if (payload.ImageBytes is { Length: > 0 })
        {
            var image = ImageUtilities.DecodePng(payload.ImageBytes);
            if (image is not null)
            {
                dataObject.SetImage(image);
            }
        }

        return dataObject;
    }

    public static void Write(CapturedClipboardPayload payload, bool copy = true)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                WriteCore(payload, copy);
                return;
            }
            catch (COMException ex)
            {
                lastException = ex;
                if (attempt < 2)
                {
                    Thread.Sleep(50 * (attempt + 1)); // Exponential backoff: 50ms, 100ms
                }
            }
            catch (ExternalException ex)
            {
                lastException = ex;
                if (attempt < 2)
                {
                    Thread.Sleep(50 * (attempt + 1));
                }
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }
    }

    private static void WriteCore(CapturedClipboardPayload payload, bool copy)
    {
        var dataObject = CreateDataObject(payload);
        Clipboard.SetDataObject(dataObject, copy);
    }
}
