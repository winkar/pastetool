using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace PasteTool.Core.Utilities;

public static class RichTextUtilities
{
    public static string ExtractPlainText(string rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
        {
            return string.Empty;
        }

        var document = new FlowDocument();
        var range = new TextRange(document.ContentStart, document.ContentEnd);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rtf));
        try
        {
            range.Load(stream, DataFormats.Rtf);
            return SearchNormalizer.CollapseWhitespace(range.Text);
        }
        catch
        {
            return string.Empty;
        }
    }
}
