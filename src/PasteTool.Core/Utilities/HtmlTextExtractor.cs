using System.Net;
using System.Text.RegularExpressions;

namespace PasteTool.Core.Utilities;

public static class HtmlTextExtractor
{
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LineBreakRegex = new("</(p|div|li|tr|h1|h2|h3|h4|h5|h6)>|<br\\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ExtractPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var fragment = ExtractClipboardFragment(html);
        fragment = LineBreakRegex.Replace(fragment, "\n");
        fragment = TagRegex.Replace(fragment, " ");
        fragment = WebUtility.HtmlDecode(fragment);
        return SearchNormalizer.CollapseWhitespace(fragment);
    }

    private static string ExtractClipboardFragment(string html)
    {
        const string startMarker = "<!--StartFragment-->";
        const string endMarker = "<!--EndFragment-->";

        var startIndex = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        var endIndex = html.IndexOf(endMarker, StringComparison.OrdinalIgnoreCase);

        if (startIndex >= 0 && endIndex > startIndex)
        {
            startIndex += startMarker.Length;
            return html[startIndex..endIndex];
        }

        return html;
    }
}
