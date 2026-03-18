using System.IO;
using System.Text.Json;
using PasteTool.Core.Models;

namespace PasteTool.Core.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly ILogger _logger;

    public SettingsStore(string settingsPath, ILogger logger)
    {
        _settingsPath = settingsPath;
        _logger = logger;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return CreateDefaultSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
            settings ??= CreateDefaultSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning($"Settings file is corrupted, using defaults: {_settingsPath}", ex);
            return CreateDefaultSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load settings from {_settingsPath}, using defaults", ex);
            return CreateDefaultSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            settings.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);

            await using var stream = File.Create(_settingsPath);
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to save settings to {_settingsPath}", ex);
            throw;
        }
    }

    private static AppSettings CreateDefaultSettings()
    {
        var settings = new AppSettings();
        settings.Normalize();
        return settings;
    }
}
