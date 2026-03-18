using PasteTool.Core.Models;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Services;

public sealed class SearchService : ISearchService
{
    public IReadOnlyList<ClipEntry> Search(IReadOnlyList<ClipEntry> entries, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            var results = new ClipEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                results[i] = entries[i];
            }
            Array.Sort(results, (a, b) => b.CapturedAtUtc.CompareTo(a.CapturedAtUtc));
            return results;
        }

        var normalizedQuery = SearchNormalizer.Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var results = new ClipEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                results[i] = entries[i];
            }
            Array.Sort(results, (a, b) => b.CapturedAtUtc.CompareTo(a.CapturedAtUtc));
            return results;
        }

        var tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matchedResults = new List<SearchResult>(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            var result = ScoreEntry(entries[i], normalizedQuery, tokens);
            if (result.IsMatch)
            {
                matchedResults.Add(result);
            }
        }

        matchedResults.Sort((a, b) =>
        {
            var prefixCompare = a.PrefixRank.CompareTo(b.PrefixRank);
            if (prefixCompare != 0) return prefixCompare;

            var indexCompare = a.MatchIndex.CompareTo(b.MatchIndex);
            if (indexCompare != 0) return indexCompare;

            return b.Entry.CapturedAtUtc.CompareTo(a.Entry.CapturedAtUtc);
        });

        var finalResults = new ClipEntry[matchedResults.Count];
        for (int i = 0; i < matchedResults.Count; i++)
        {
            finalResults[i] = matchedResults[i].Entry;
        }

        return finalResults;
    }

    private static SearchResult ScoreEntry(ClipEntry entry, string normalizedQuery, string[] tokens)
    {
        var haystack = SearchNormalizer.Normalize(entry.SearchText);
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return SearchResult.NotMatched(entry);
        }

        var fullIndex = haystack.IndexOf(normalizedQuery, StringComparison.Ordinal);
        var prefixRank = fullIndex == 0 ? 0 : 1;
        var earliestTokenIndex = int.MaxValue;

        foreach (var token in tokens)
        {
            var tokenIndex = haystack.IndexOf(token, StringComparison.Ordinal);
            if (tokenIndex < 0)
            {
                return SearchResult.NotMatched(entry);
            }

            earliestTokenIndex = Math.Min(earliestTokenIndex, tokenIndex);
        }

        return new SearchResult(entry, true, prefixRank, fullIndex >= 0 ? fullIndex : earliestTokenIndex);
    }

    private readonly record struct SearchResult(ClipEntry Entry, bool IsMatch, int PrefixRank, int MatchIndex)
    {
        public static SearchResult NotMatched(ClipEntry entry) => new(entry, false, int.MaxValue, int.MaxValue);
    }
}
