using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using PasteTool.Core.Models;
using PasteTool.Core.Utilities;

namespace PasteTool.Core.Services;

public sealed class SqliteClipRepository : IClipRepository
{
    private const int MaxTextChars = 1_000_000;
    private const int MaxImageBytes = 32 * 1024 * 1024;
    private const int PreviewLength = 180;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };
    private static readonly Regex SearchTokenRegex = new("[\\p{L}\\p{N}_]+", RegexOptions.Compiled);

    private readonly Func<AppSettings> _settingsAccessor;
    private readonly string _databasePath;
    private readonly string _blobDirectory;
    private readonly string _thumbnailDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteClipRepository(
        Func<AppSettings> settingsAccessor,
        string databasePath,
        string blobDirectory,
        string thumbnailDirectory)
    {
        _settingsAccessor = settingsAccessor;
        _databasePath = databasePath;
        _blobDirectory = blobDirectory;
        _thumbnailDirectory = thumbnailDirectory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            Directory.CreateDirectory(_blobDirectory);
            Directory.CreateDirectory(_thumbnailDirectory);
            await EnsureSchemaAsync(cancellationToken);
            await RebuildSearchIndexAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ClipEntry>> LoadEntriesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadEntriesCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SaveClipResult?> SaveAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken = default)
    {
        if (!payload.HasContent)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var prepared = await PrepareAsync(payload, cancellationToken);
            if (prepared is null)
            {
                return null;
            }

            await using var connection = await OpenConnectionAsync(cancellationToken);
            var existing = await GetEntryByHashAsync(connection, prepared.Entry.ContentHash, cancellationToken);

            long entryId;
            if (existing is null)
            {
                var insert = connection.CreateCommand();
                insert.CommandText =
                    """
                    INSERT INTO clip_entries (
                        kind,
                        captured_at_utc,
                        search_text,
                        preview_text,
                        content_hash,
                        formats,
                        blob_path,
                        thumbnail_path,
                        blob_size_bytes,
                        image_pixel_width,
                        image_pixel_height)
                    VALUES (
                        $kind,
                        $captured_at_utc,
                        $search_text,
                        $preview_text,
                        $content_hash,
                        $formats,
                        $blob_path,
                        $thumbnail_path,
                        $blob_size_bytes,
                        $image_pixel_width,
                        $image_pixel_height);
                    SELECT last_insert_rowid();
                    """;
                ApplyParameters(insert, prepared.Entry);
                entryId = Convert.ToInt64(await insert.ExecuteScalarAsync(cancellationToken));
            }
            else
            {
                var update = connection.CreateCommand();
                update.CommandText =
                    """
                    UPDATE clip_entries
                    SET
                        kind = $kind,
                        captured_at_utc = $captured_at_utc,
                        search_text = $search_text,
                        preview_text = $preview_text,
                        formats = $formats,
                        blob_path = $blob_path,
                        thumbnail_path = $thumbnail_path,
                        blob_size_bytes = $blob_size_bytes,
                        image_pixel_width = $image_pixel_width,
                        image_pixel_height = $image_pixel_height
                    WHERE id = $id;
                    """;
                update.Parameters.AddWithValue("$id", existing.Id);
                ApplyParameters(update, prepared.Entry);
                await update.ExecuteNonQueryAsync(cancellationToken);
                entryId = existing.Id;
            }

            await UpsertSearchIndexAsync(connection, prepared.Entry, cancellationToken);
            var removedEntryIds = await TrimAsync(connection, cancellationToken);
            var savedEntry = await GetEntryByIdAsync(connection, entryId, cancellationToken);
            if (savedEntry is null)
            {
                return null;
            }

            return new SaveClipResult
            {
                Entry = savedEntry,
                RemovedEntryIds = removedEntryIds,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ClipEntry>> SearchEntriesAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<ClipEntry>();
        }

        var normalizedQuery = SearchNormalizer.Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<ClipEntry>();
        }

        var tokens = ExtractSearchTokens(normalizedQuery);
        if (tokens.Length == 0)
        {
            return Array.Empty<ClipEntry>();
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT
                    clip_entries.id,
                    clip_entries.kind,
                    clip_entries.captured_at_utc,
                    clip_entries.search_text,
                    clip_entries.preview_text,
                    clip_entries.content_hash,
                    clip_entries.formats,
                    clip_entries.blob_path,
                    clip_entries.thumbnail_path,
                    clip_entries.blob_size_bytes,
                    clip_entries.image_pixel_width,
                    clip_entries.image_pixel_height
                FROM clip_entries
                INNER JOIN clip_entries_fts
                    ON clip_entries_fts.content_hash = clip_entries.content_hash
                WHERE clip_entries_fts MATCH $match_query
                ORDER BY bm25(clip_entries_fts), clip_entries.captured_at_utc DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$match_query", BuildMatchQuery(tokens));
            command.Parameters.AddWithValue("$limit", limit);

            var entries = new List<ClipEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(MapEntry(reader));
            }

            return entries;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CapturedClipboardPayload?> LoadPayloadAsync(ClipEntry entry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entry.BlobPath))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(entry.BlobPath))
            {
                return null;
            }

            if (entry.Kind == ClipKind.Image)
            {
                var bytes = await File.ReadAllBytesAsync(entry.BlobPath, cancellationToken);
                return new CapturedClipboardPayload
                {
                    ImageBytes = bytes,
                    ImagePixelWidth = entry.ImagePixelWidth,
                    ImagePixelHeight = entry.ImagePixelHeight,
                    SourceFormats = new[] { "Bitmap" },
                };
            }

            await using var stream = File.OpenRead(entry.BlobPath);
            var stored = await JsonSerializer.DeserializeAsync<StoredClipPayload>(stream, SerializerOptions, cancellationToken);
            if (stored is null)
            {
                return null;
            }

            return new CapturedClipboardPayload
            {
                UnicodeText = stored.UnicodeText,
                Rtf = stored.Rtf,
                Html = stored.Html,
                SourceFormats = stored.SourceFormats ?? Array.Empty<string>(),
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText =
                """
                DELETE FROM clip_entries;
                DELETE FROM clip_entries_fts;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            DeleteDirectoryContents(_blobDirectory);
            DeleteDirectoryContents(_thumbnailDirectory);
            Directory.CreateDirectory(_blobDirectory);
            Directory.CreateDirectory(_thumbnailDirectory);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS clip_entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                kind INTEGER NOT NULL,
                captured_at_utc TEXT NOT NULL,
                search_text TEXT NOT NULL,
                preview_text TEXT NOT NULL,
                content_hash TEXT NOT NULL UNIQUE,
                formats TEXT NOT NULL,
                blob_path TEXT,
                thumbnail_path TEXT,
                blob_size_bytes INTEGER NOT NULL DEFAULT 0,
                image_pixel_width INTEGER NULL,
                image_pixel_height INTEGER NULL
            );

            CREATE INDEX IF NOT EXISTS idx_clip_entries_captured_at ON clip_entries(captured_at_utc DESC);
            CREATE INDEX IF NOT EXISTS idx_clip_entries_content_hash ON clip_entries(content_hash);

            CREATE VIRTUAL TABLE IF NOT EXISTS clip_entries_fts USING fts5(
                content_hash UNINDEXED,
                search_text,
                tokenize = 'unicode61'
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<PreparedEntry?> PrepareAsync(CapturedClipboardPayload payload, CancellationToken cancellationToken)
    {
        if (payload.ImageBytes is { Length: > MaxImageBytes })
        {
            return null;
        }

        var estimatedTextSize =
            (payload.UnicodeText?.Length ?? 0) +
            (payload.Rtf?.Length ?? 0) +
            (payload.Html?.Length ?? 0);
        if (estimatedTextSize > MaxTextChars)
        {
            return null;
        }

        var hash = ContentHasher.Compute(payload);
        var kind = ResolveKind(payload);
        var plainText = ResolvePlainText(payload, kind);
        var searchText = kind == ClipKind.Image ? BuildImageSearchText(payload) : plainText;
        var previewText = BuildPreviewText(kind, plainText, payload);
        var formats = payload.SourceFormats.Count == 0
            ? BuildFallbackFormats(payload)
            : string.Join(", ", payload.SourceFormats);
        var now = DateTime.UtcNow;

        string? blobPath;
        string? thumbnailPath = null;
        long blobSizeBytes;

        if (kind == ClipKind.Image)
        {
            blobPath = Path.Combine(_blobDirectory, $"{hash}.png");
            thumbnailPath = Path.Combine(_thumbnailDirectory, $"{hash}.png");
            await EnsureImageFilesAsync(blobPath, thumbnailPath, payload.ImageBytes!, cancellationToken);
            blobSizeBytes = new FileInfo(blobPath).Length;
        }
        else
        {
            blobPath = Path.Combine(_blobDirectory, $"{hash}.json");
            if (!File.Exists(blobPath))
            {
                var storedPayload = new StoredClipPayload
                {
                    UnicodeText = payload.UnicodeText,
                    Rtf = payload.Rtf,
                    Html = payload.Html,
                    SourceFormats = payload.SourceFormats.ToArray(),
                };

                await using var stream = File.Create(blobPath);
                await JsonSerializer.SerializeAsync(stream, storedPayload, SerializerOptions, cancellationToken);
            }

            blobSizeBytes = new FileInfo(blobPath).Length;
        }

        return new PreparedEntry(
            new ClipEntry
            {
                Kind = kind,
                CapturedAtUtc = now,
                SearchText = searchText,
                PreviewText = previewText,
                ContentHash = hash,
                Formats = formats,
                BlobPath = blobPath,
                ThumbnailPath = thumbnailPath,
                BlobSizeBytes = blobSizeBytes,
                ImagePixelWidth = payload.ImagePixelWidth,
                ImagePixelHeight = payload.ImagePixelHeight,
            });
    }

    private async Task EnsureImageFilesAsync(string blobPath, string thumbnailPath, byte[] imageBytes, CancellationToken cancellationToken)
    {
        if (!File.Exists(blobPath))
        {
            await File.WriteAllBytesAsync(blobPath, imageBytes, cancellationToken);
        }

        if (!File.Exists(thumbnailPath))
        {
            var thumbnailBytes = ImageUtilities.CreateThumbnail(imageBytes, 240);
            await File.WriteAllBytesAsync(thumbnailPath, thumbnailBytes, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<long>> TrimAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var settings = _settingsAccessor();
        settings.Normalize();

        var oldestEntries = await GetEntriesOrderedOldestFirstAsync(connection, cancellationToken);
        var removedIds = new HashSet<long>();

        var overflowCount = Math.Max(0, oldestEntries.Count - settings.MaxEntries);
        foreach (var entry in oldestEntries.Take(overflowCount))
        {
            removedIds.Add(entry.Id);
        }

        long totalImageBytes = oldestEntries
            .Where(entry => entry.Kind == ClipKind.Image && !removedIds.Contains(entry.Id))
            .Sum(entry => entry.BlobSizeBytes);
        var maxImageBytes = settings.MaxImageCacheMb * 1024L * 1024L;

        foreach (var entry in oldestEntries.Where(entry => entry.Kind == ClipKind.Image))
        {
            if (totalImageBytes <= maxImageBytes)
            {
                break;
            }

            if (!removedIds.Add(entry.Id))
            {
                continue;
            }

            totalImageBytes -= entry.BlobSizeBytes;
        }

        if (removedIds.Count == 0)
        {
            return Array.Empty<long>();
        }

        var removedEntries = oldestEntries.Where(entry => removedIds.Contains(entry.Id)).ToArray();
        foreach (var entry in removedEntries)
        {
            DeleteIfExists(entry.BlobPath);
            DeleteIfExists(entry.ThumbnailPath);
        }

        await RemoveSearchIndexEntriesAsync(
            connection,
            removedEntries.Select(entry => entry.ContentHash).ToArray(),
            cancellationToken);

        var deleteCommand = connection.CreateCommand();
        var parameterNames = removedEntries.Select((entry, index) =>
        {
            var name = $"$id{index}";
            deleteCommand.Parameters.AddWithValue(name, entry.Id);
            return name;
        });
        deleteCommand.CommandText = $"DELETE FROM clip_entries WHERE id IN ({string.Join(", ", parameterNames)});";
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

        return removedEntries.Select(entry => entry.Id).ToArray();
    }

    private async Task<IReadOnlyList<ClipEntry>> GetEntriesOrderedOldestFirstAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                kind,
                captured_at_utc,
                search_text,
                preview_text,
                content_hash,
                formats,
                blob_path,
                thumbnail_path,
                blob_size_bytes,
                image_pixel_width,
                image_pixel_height
            FROM clip_entries
            ORDER BY captured_at_utc ASC;
            """;

        var entries = new List<ClipEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(MapEntry(reader));
        }

        return entries;
    }

    private async Task<IReadOnlyList<ClipEntry>> LoadEntriesCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                kind,
                captured_at_utc,
                search_text,
                preview_text,
                content_hash,
                formats,
                blob_path,
                thumbnail_path,
                blob_size_bytes,
                image_pixel_width,
                image_pixel_height
            FROM clip_entries
            ORDER BY captured_at_utc DESC;
            """;

        var entries = new List<ClipEntry>();
        var missingIds = new List<long>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var entry = MapEntry(reader);
            if (!string.IsNullOrWhiteSpace(entry.BlobPath) && !File.Exists(entry.BlobPath))
            {
                missingIds.Add(entry.Id);
                continue;
            }

            entries.Add(entry);
        }

        if (missingIds.Count > 0)
        {
            var delete = connection.CreateCommand();
            var parameterNames = missingIds.Select((id, index) =>
            {
                var name = $"$id{index}";
                delete.Parameters.AddWithValue(name, id);
                return name;
            });
            delete.CommandText = $"DELETE FROM clip_entries WHERE id IN ({string.Join(", ", parameterNames)});";
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        return entries;
    }

    private async Task<ClipEntry?> GetEntryByHashAsync(SqliteConnection connection, string hash, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                kind,
                captured_at_utc,
                search_text,
                preview_text,
                content_hash,
                formats,
                blob_path,
                thumbnail_path,
                blob_size_bytes,
                image_pixel_width,
                image_pixel_height
            FROM clip_entries
            WHERE content_hash = $content_hash
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$content_hash", hash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapEntry(reader);
    }

    private async Task<ClipEntry?> GetEntryByIdAsync(SqliteConnection connection, long entryId, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id,
                kind,
                captured_at_utc,
                search_text,
                preview_text,
                content_hash,
                formats,
                blob_path,
                thumbnail_path,
                blob_size_bytes,
                image_pixel_width,
                image_pixel_height
            FROM clip_entries
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", entryId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapEntry(reader);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void ApplyParameters(SqliteCommand command, ClipEntry entry)
    {
        command.Parameters.AddWithValue("$kind", (int)entry.Kind);
        command.Parameters.AddWithValue("$captured_at_utc", entry.CapturedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$search_text", entry.SearchText);
        command.Parameters.AddWithValue("$preview_text", entry.PreviewText);
        command.Parameters.AddWithValue("$content_hash", entry.ContentHash);
        command.Parameters.AddWithValue("$formats", entry.Formats);
        command.Parameters.AddWithValue("$blob_path", (object?)entry.BlobPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$thumbnail_path", (object?)entry.ThumbnailPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$blob_size_bytes", entry.BlobSizeBytes);
        command.Parameters.AddWithValue("$image_pixel_width", (object?)entry.ImagePixelWidth ?? DBNull.Value);
        command.Parameters.AddWithValue("$image_pixel_height", (object?)entry.ImagePixelHeight ?? DBNull.Value);
    }

    private static ClipEntry MapEntry(SqliteDataReader reader)
    {
        return new ClipEntry
        {
            Id = reader.GetInt64(0),
            Kind = (ClipKind)reader.GetInt32(1),
            CapturedAtUtc = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            SearchText = reader.GetString(3),
            PreviewText = reader.GetString(4),
            ContentHash = reader.GetString(5),
            Formats = reader.GetString(6),
            BlobPath = reader.IsDBNull(7) ? null : reader.GetString(7),
            ThumbnailPath = reader.IsDBNull(8) ? null : reader.GetString(8),
            BlobSizeBytes = reader.GetInt64(9),
            ImagePixelWidth = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            ImagePixelHeight = reader.IsDBNull(11) ? null : reader.GetInt32(11),
        };
    }

    private static ClipKind ResolveKind(CapturedClipboardPayload payload)
    {
        // 如果同时有文字内容，优先识别为文字类型（如 Excel 行数据会同时包含 HTML 和 Bitmap）
        var hasText = !string.IsNullOrWhiteSpace(payload.UnicodeText) ||
                      !string.IsNullOrWhiteSpace(payload.Rtf) ||
                      !string.IsNullOrWhiteSpace(payload.Html);

        if (!hasText && payload.ImageBytes is { Length: > 0 })
        {
            return ClipKind.Image;
        }

        return !string.IsNullOrWhiteSpace(payload.Rtf) || !string.IsNullOrWhiteSpace(payload.Html)
            ? ClipKind.RichText
            : ClipKind.Text;
    }

    private static string ResolvePlainText(CapturedClipboardPayload payload, ClipKind kind)
    {
        if (!string.IsNullOrWhiteSpace(payload.UnicodeText))
        {
            return payload.UnicodeText!;
        }

        if (kind == ClipKind.RichText && !string.IsNullOrWhiteSpace(payload.Rtf))
        {
            var rtfPlainText = RichTextUtilities.ExtractPlainText(payload.Rtf!);
            if (!string.IsNullOrWhiteSpace(rtfPlainText))
            {
                return rtfPlainText;
            }
        }

        if (!string.IsNullOrWhiteSpace(payload.Html))
        {
            return HtmlTextExtractor.ExtractPlainText(payload.Html!);
        }

        return string.Empty;
    }

    private static string BuildPreviewText(ClipKind kind, string plainText, CapturedClipboardPayload payload)
    {
        if (kind == ClipKind.Image)
        {
            var width = payload.ImagePixelWidth ?? 0;
            var height = payload.ImagePixelHeight ?? 0;
            return width > 0 && height > 0 ? $"图片 {width}x{height}" : "图片";
        }

        var collapsed = SearchNormalizer.CollapseWhitespace(plainText);
        return collapsed.Length <= PreviewLength ? collapsed : $"{collapsed[..PreviewLength]}...";
    }

    private static string BuildImageSearchText(CapturedClipboardPayload payload)
    {
        var width = payload.ImagePixelWidth?.ToString() ?? "0";
        var height = payload.ImagePixelHeight?.ToString() ?? "0";
        return $"image 图片 png {width}x{height}";
    }

    private static string BuildFallbackFormats(CapturedClipboardPayload payload)
    {
        var formats = new List<string>();

        if (!string.IsNullOrWhiteSpace(payload.UnicodeText))
        {
            formats.Add("UnicodeText");
        }

        if (!string.IsNullOrWhiteSpace(payload.Rtf))
        {
            formats.Add("Rtf");
        }

        if (!string.IsNullOrWhiteSpace(payload.Html))
        {
            formats.Add("Html");
        }

        if (payload.ImageBytes is { Length: > 0 })
        {
            formats.Add("Bitmap");
        }

        return string.Join(", ", formats);
    }

    private async Task RebuildSearchIndexAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var clearCommand = connection.CreateCommand();
        clearCommand.CommandText = "DELETE FROM clip_entries_fts;";
        await clearCommand.ExecuteNonQueryAsync(cancellationToken);

        var selectCommand = connection.CreateCommand();
        selectCommand.CommandText =
            """
            SELECT
                content_hash,
                search_text
            FROM clip_entries;
            """;

        var searchEntries = new List<(string ContentHash, string SearchText)>();
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            searchEntries.Add((reader.GetString(0), reader.GetString(1)));
        }

        foreach (var searchEntry in searchEntries)
        {
            await InsertSearchIndexEntryAsync(connection, searchEntry.ContentHash, searchEntry.SearchText, cancellationToken);
        }
    }

    private static async Task UpsertSearchIndexAsync(SqliteConnection connection, ClipEntry entry, CancellationToken cancellationToken)
    {
        await RemoveSearchIndexEntriesAsync(connection, new[] { entry.ContentHash }, cancellationToken);
        await InsertSearchIndexEntryAsync(connection, entry.ContentHash, entry.SearchText, cancellationToken);
    }

    private static async Task InsertSearchIndexEntryAsync(SqliteConnection connection, string contentHash, string searchText, CancellationToken cancellationToken)
    {
        var normalizedSearchText = NormalizeSearchText(searchText);
        if (string.IsNullOrWhiteSpace(normalizedSearchText))
        {
            return;
        }

        var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
            INSERT INTO clip_entries_fts (
                content_hash,
                search_text)
            VALUES (
                $content_hash,
                $search_text);
            """;
        insertCommand.Parameters.AddWithValue("$content_hash", contentHash);
        insertCommand.Parameters.AddWithValue("$search_text", normalizedSearchText);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RemoveSearchIndexEntriesAsync(SqliteConnection connection, IReadOnlyCollection<string> contentHashes, CancellationToken cancellationToken)
    {
        if (contentHashes.Count == 0)
        {
            return;
        }

        var deleteCommand = connection.CreateCommand();
        var parameterNames = contentHashes.Select((contentHash, index) =>
        {
            var name = $"$content_hash{index}";
            deleteCommand.Parameters.AddWithValue(name, contentHash);
            return name;
        });

        deleteCommand.CommandText = $"DELETE FROM clip_entries_fts WHERE content_hash IN ({string.Join(", ", parameterNames)});";
        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeSearchText(string? searchText)
    {
        return SearchNormalizer.Normalize(searchText);
    }

    private static string[] ExtractSearchTokens(string normalizedQuery)
    {
        return SearchTokenRegex.Matches(normalizedQuery)
            .Select(match => match.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildMatchQuery(IEnumerable<string> tokens)
    {
        return string.Join(" AND ", tokens.Select(token => $"{token}*"));
    }

    private static void DeleteIfExists(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Log but don't throw - file deletion is not critical
        }
    }

    private static void DeleteDirectoryContents(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Continue deleting other files even if one fails
                }
            }
        }
        catch
        {
            // Directory enumeration failed, but don't throw
        }
    }

    private sealed record PreparedEntry(ClipEntry Entry);

    private sealed class StoredClipPayload
    {
        public string? UnicodeText { get; init; }

        public string? Rtf { get; init; }

        public string? Html { get; init; }

        public string[]? SourceFormats { get; init; }
    }
}
