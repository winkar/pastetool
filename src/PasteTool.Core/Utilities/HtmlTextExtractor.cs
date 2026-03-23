using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PasteTool.Core.Utilities;

public static class HtmlTextExtractor
{
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LineBreakRegex = new("</(p|div|li|h1|h2|h3|h4|h5|h6)>|<br\\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TableRowRegex = new(@"<tr[\s\S]*?</tr>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TableCellRegex = new(@"<t[dh][^>]*>([\s\S]*?)</t[dh]>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    /// <summary>
    /// 检测 HTML 是否包含表格，若包含则以格式化文本方式提取，否则返回 null。
    /// </summary>
    public static string? TryExtractTableText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var fragment = ExtractClipboardFragment(html);
        if (!fragment.Contains("<tr", StringComparison.OrdinalIgnoreCase) &&
            !fragment.Contains("<TR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var sb = new StringBuilder();
        var rowMatches = TableRowRegex.Matches(fragment);
        foreach (Match rowMatch in rowMatches)
        {
            var cells = TableCellRegex.Matches(rowMatch.Value);
            var cellTexts = new List<string>();
            foreach (Match cellMatch in cells)
            {
                var cellContent = cellMatch.Groups[1].Value;
                // 去除嵌套 HTML 标签，提取文本
                cellContent = TagRegex.Replace(cellContent, "");
                cellContent = WebUtility.HtmlDecode(cellContent).Trim();
                cellTexts.Add(cellContent);
            }

            if (cellTexts.Count > 0)
            {
                sb.AppendLine(string.Join("\t", cellTexts));
            }
        }

        var result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
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

