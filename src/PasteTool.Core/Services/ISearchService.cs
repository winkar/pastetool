using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

public interface ISearchService
{
    IReadOnlyList<ClipEntry> Search(IReadOnlyList<ClipEntry> entries, string query);
}
