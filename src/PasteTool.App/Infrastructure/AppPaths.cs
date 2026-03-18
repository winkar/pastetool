using System.IO;

namespace PasteTool.App.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
        DatabasePath = Path.Combine(rootDirectory, "history.db");
        BlobDirectory = Path.Combine(rootDirectory, "blobs");
        ThumbnailDirectory = Path.Combine(rootDirectory, "thumbs");
        SettingsPath = Path.Combine(rootDirectory, "config.json");
        LogDirectory = Path.Combine(rootDirectory, "logs");
    }

    public string RootDirectory { get; }

    public string DatabasePath { get; }

    public string BlobDirectory { get; }

    public string ThumbnailDirectory { get; }

    public string SettingsPath { get; }

    public string LogDirectory { get; }

    public static AppPaths CreateDefault()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasteTool");
        Directory.CreateDirectory(root);
        return new AppPaths(root);
    }
}
