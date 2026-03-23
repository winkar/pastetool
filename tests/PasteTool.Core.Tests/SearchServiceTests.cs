using PasteTool.Core.Models;
using PasteTool.Core.Services;

namespace PasteTool.Core.Tests;

public class SearchServiceTests
{
    private readonly SearchService _service = new();

    [Fact]
    public void Search_PrioritizesPrefixMatchesBeforeRecency()
    {
        var now = DateTime.UtcNow;
        var entries = new[]
        {
            CreateEntry(1, "foo project notes", now.AddMinutes(-10)),
            CreateEntry(2, "notes about foo project", now),
        };

        var results = _service.Search(entries, "foo");

        Assert.Equal(new long[] { 1, 2 }, results.Select(entry => entry.Id));
    }

    [Fact]
    public void Search_RequiresAllTokensToMatch()
    {
        var now = DateTime.UtcNow;
        var entries = new[]
        {
            CreateEntry(1, "alpha beta", now),
            CreateEntry(2, "alpha only", now.AddMinutes(-1)),
            CreateEntry(3, "beta only", now.AddMinutes(-2)),
        };

        var results = _service.Search(entries, "alpha beta");

        var matched = Assert.Single(results);
        Assert.Equal(1, matched.Id);
    }

    [Fact]
    public void Search_UsesSameTokenizationAsSqliteFallback()
    {
        var now = DateTime.UtcNow;
        var entries = new[]
        {
            CreateEntry(1, "foo bar", now),
        };

        var results = _service.Search(entries, "foo-bar");

        var matched = Assert.Single(results);
        Assert.Equal(1, matched.Id);
    }

    [Fact]
    public void Search_MatchesChineseSubstringInsideContinuousText()
    {
        var now = DateTime.UtcNow;
        var entries = new[]
        {
            CreateEntry(1, "这里面的逻辑链挺长的，", now),
        };

        var results = _service.Search(entries, "逻辑");

        var matched = Assert.Single(results);
        Assert.Equal(1, matched.Id);
    }

    private static ClipEntry CreateEntry(long id, string searchText, DateTime capturedAtUtc)
    {
        return new ClipEntry
        {
            Id = id,
            SearchText = searchText,
            PreviewText = searchText,
            CapturedAtUtc = capturedAtUtc,
            ContentHash = id.ToString(),
            Formats = "UnicodeText",
            Kind = ClipKind.Text,
        };
    }
}
