using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

public sealed class ClipboardHistoryChangedEventArgs : EventArgs
{
    public ClipboardHistoryChangedEventArgs(IReadOnlyList<ClipEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<ClipEntry> Entries { get; }
}
