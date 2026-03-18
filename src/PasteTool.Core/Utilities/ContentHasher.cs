using System.Security.Cryptography;
using System.Text;
using PasteTool.Core.Models;

namespace PasteTool.Core.Utilities;

public static class ContentHasher
{
    public static string Compute(CapturedClipboardPayload payload)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "text", payload.UnicodeText);
        Append(hash, "rtf", payload.Rtf);
        Append(hash, "html", payload.Html);

        if (payload.ImageBytes is { Length: > 0 })
        {
            hash.AppendData(payload.ImageBytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string label, string? value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(label));
        hash.AppendData(new byte[] { 0 });

        if (!string.IsNullOrEmpty(value))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
        }

        hash.AppendData(new byte[] { 0 });
    }
}
