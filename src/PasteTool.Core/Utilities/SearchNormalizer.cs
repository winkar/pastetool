using System.Text.RegularExpressions;

namespace PasteTool.Core.Utilities;

public static class SearchNormalizer
{
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return CollapseWhitespace(text).ToLowerInvariant();
    }

    public static string CollapseWhitespace(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(text.Trim(), " ");
    }
}
