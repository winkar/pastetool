using System.Text.RegularExpressions;

namespace PasteTool.Core.Utilities;

public static class SearchNormalizer
{
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex SearchTokenRegex = new("[\\p{L}\\p{N}_]+", RegexOptions.Compiled);

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

    public static string[] ExtractTokens(string? text)
    {
        var normalized = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        return SearchTokenRegex.Matches(normalized)
            .Select(match => match.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static bool ContainsCjk(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (ch is >= '\u3400' and <= '\u4DBF' or
                >= '\u4E00' and <= '\u9FFF' or
                >= '\uF900' and <= '\uFAFF' or
                >= '\u3040' and <= '\u30FF' or
                >= '\uAC00' and <= '\uD7AF')
            {
                return true;
            }
        }

        return false;
    }
}
