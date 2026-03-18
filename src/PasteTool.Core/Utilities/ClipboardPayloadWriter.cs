using System.Runtime.InteropServices;
using System.Windows;
using PasteTool.Core.Models;

namespace PasteTool.Core.Utilities;

internal static class ClipboardPayloadWriter
{
    public static void Write(CapturedClipboardPayload payload)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                WriteCore(payload);
                return;
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
    }

    private static void WriteCore(CapturedClipboardPayload payload)
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

        Clipboard.SetDataObject(dataObject, true);
    }
}
