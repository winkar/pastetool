using System.ComponentModel;
using System.Runtime.CompilerServices;
using PasteTool.Core.Models;
using System.Collections.Generic;

namespace PasteTool.App.Models;

public sealed class HistoryListItem : INotifyPropertyChanged
{
    private ClipEntry _entry;
    private string? _searchQuery;
    private IReadOnlyList<TextSegment> _highlightedPreviewSegments;

    public HistoryListItem(ClipEntry entry, string? searchQuery = null)
    {
        _entry = entry;
        _searchQuery = NormalizeSearchQuery(searchQuery);
        _highlightedPreviewSegments = BuildHighlightedPreviewSegments();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ClipEntry Entry => _entry;

    public string? SearchQuery => _searchQuery;

    public string KindLabel => Entry.Kind switch
    {
        ClipKind.Image => "IMG",
        ClipKind.RichText => "RTF",
        _ => "TXT",
    };

    public string PreviewText => string.IsNullOrWhiteSpace(Entry.PreviewText) ? "(空内容)" : Entry.PreviewText;

    public string FormatsText => Entry.Formats;

    public string CapturedAtText => Entry.CapturedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss");

    public IReadOnlyList<TextSegment> HighlightedPreviewSegments => _highlightedPreviewSegments;

    public void UpdateEntry(ClipEntry entry)
    {
        _entry = entry;
        RefreshDisplayState();
    }

    public void UpdateSearchQuery(string? searchQuery)
    {
        var normalizedSearchQuery = NormalizeSearchQuery(searchQuery);
        if (string.Equals(_searchQuery, normalizedSearchQuery, StringComparison.Ordinal))
        {
            return;
        }

        _searchQuery = normalizedSearchQuery;
        RefreshHighlightSegments();
        OnPropertyChanged(nameof(SearchQuery));
    }

    private void RefreshDisplayState()
    {
        RefreshHighlightSegments();
        OnPropertyChanged(nameof(Entry));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(FormatsText));
        OnPropertyChanged(nameof(CapturedAtText));
    }

    private void RefreshHighlightSegments()
    {
        _highlightedPreviewSegments = BuildHighlightedPreviewSegments();
        OnPropertyChanged(nameof(HighlightedPreviewSegments));
    }

    private IReadOnlyList<TextSegment> BuildHighlightedPreviewSegments()
    {
        var text = PreviewText;
        if (string.IsNullOrWhiteSpace(_searchQuery) || string.IsNullOrWhiteSpace(text))
        {
            return new[] { new TextSegment(text, false) };
        }

        var segments = new List<TextSegment>();
        var normalizedText = text.ToLowerInvariant();
        var normalizedQuery = _searchQuery.ToLowerInvariant();
        var lastIndex = 0;

        while (lastIndex < text.Length)
        {
            var index = normalizedText.IndexOf(normalizedQuery, lastIndex, StringComparison.Ordinal);
            if (index < 0)
            {
                segments.Add(new TextSegment(text.Substring(lastIndex), false));
                break;
            }

            if (index > lastIndex)
            {
                segments.Add(new TextSegment(text.Substring(lastIndex, index - lastIndex), false));
            }

            segments.Add(new TextSegment(text.Substring(index, _searchQuery!.Length), true));
            lastIndex = index + _searchQuery.Length;
        }

        if (segments.Count == 0)
        {
            segments.Add(new TextSegment(text, false));
        }

        return segments;
    }

    private static string? NormalizeSearchQuery(string? searchQuery)
    {
        return string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record TextSegment(string Text, bool IsHighlighted);
