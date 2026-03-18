using PasteTool.Core.Models;
using System.Collections.Generic;

namespace PasteTool.App.Models;

public sealed class HistoryListItem
{
    public HistoryListItem(ClipEntry entry, string? searchQuery = null)
    {
        Entry = entry;
        SearchQuery = searchQuery;
    }

    public ClipEntry Entry { get; }

    public string? SearchQuery { get; }

    public string KindLabel => Entry.Kind switch
    {
        ClipKind.Image => "IMG",
        ClipKind.RichText => "RTF",
        _ => "TXT",
    };

    public string PreviewText => string.IsNullOrWhiteSpace(Entry.PreviewText) ? "(空内容)" : Entry.PreviewText;

    public string FormatsText => Entry.Formats;

    public string CapturedAtText => Entry.CapturedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss");

    public IEnumerable<TextSegment> HighlightedPreviewSegments
    {
        get
        {
            var text = PreviewText;
            if (string.IsNullOrWhiteSpace(SearchQuery) || string.IsNullOrWhiteSpace(text))
            {
                yield return new TextSegment(text, false);
                yield break;
            }

            var normalizedText = text.ToLowerInvariant();
            var normalizedQuery = SearchQuery.ToLowerInvariant();
            var lastIndex = 0;

            while (lastIndex < text.Length)
            {
                var index = normalizedText.IndexOf(normalizedQuery, lastIndex, StringComparison.Ordinal);
                if (index < 0)
                {
                    yield return new TextSegment(text.Substring(lastIndex), false);
                    break;
                }

                if (index > lastIndex)
                {
                    yield return new TextSegment(text.Substring(lastIndex, index - lastIndex), false);
                }

                yield return new TextSegment(text.Substring(index, SearchQuery.Length), true);
                lastIndex = index + SearchQuery.Length;
            }
        }
    }
}

public record TextSegment(string Text, bool IsHighlighted);
