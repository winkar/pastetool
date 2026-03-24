using System.Windows;
using PasteTool.App.Infrastructure;
using PasteTool.App.Windows;
using PasteTool.Core.Models;
using PasteTool.Core.Services;
using Application = System.Windows.Application;

namespace PasteTool.App;

public sealed class AppController : IDisposable
{
    private readonly Application _application;
    private readonly AppPaths _paths;
    private readonly SettingsStore _settingsStore;
    private readonly AutoStartService _autoStartService = new();
    private readonly ILogger _logger;
    private AppSettings _settings = new();
    private ClipboardHistoryManager? _historyManager;
    private SqliteClipRepository? _clipRepository;
    private PasteService? _pasteService;
    private ClipboardMonitor? _clipboardMonitor;
    private GlobalHotkeyManager? _hotkeyManager;
    private TrayIconHost? _trayIconHost;
    private HistoryWindow? _historyWindow;
    private bool _isDisposed;

    public AppController(Application application)
    {
        _application = application;
        _paths = AppPaths.CreateDefault();
        _logger = new FileLogger(_paths.LogDirectory);
        _settingsStore = new SettingsStore(_paths.SettingsPath, _logger);
    }

    public async Task StartAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        _settings.Normalize();

        _clipRepository = new SqliteClipRepository(
            () => _settings,
            _paths.DatabasePath,
            _paths.BlobDirectory,
            _paths.ThumbnailDirectory);
        _pasteService = new PasteService(_logger);
        _clipboardMonitor = new ClipboardMonitor(_logger);
        _historyManager = new ClipboardHistoryManager(_clipboardMonitor, _clipRepository, _pasteService, _logger);

        _historyWindow = new HistoryWindow(
            _historyManager,
            new SearchService(),
            (query, limit, cancellationToken) => _clipRepository!.SearchEntriesAsync(query, limit, cancellationToken),
            (entry, cancellationToken) => _historyManager!.LoadPayloadAsync(entry, cancellationToken),
            PasteAsync,
            ShowSettings,
            ClearHistoryAsync);

        _hotkeyManager = new GlobalHotkeyManager();
        _hotkeyManager.HotkeyPressed += (_, _) => ShowHistory();

        if (!_hotkeyManager.Register(_settings.Hotkey))
        {
            _settings.Hotkey = HotkeyGesture.Default;
            _hotkeyManager.Register(_settings.Hotkey);
            await _settingsStore.SaveAsync(_settings);
        }

        _trayIconHost = new TrayIconHost(ShowHistory, ShowSettings, ClearHistoryAsync, Exit);

        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前程序路径。");
        _autoStartService.Apply(_settings.StartWithWindows, processPath, error =>
        {
            _logger.LogWarning($"Auto-start configuration failed: {error}");
        });

        await _historyManager.InitializeAsync();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _trayIconHost?.Dispose();
        _hotkeyManager?.Dispose();

        if (_historyWindow is not null)
        {
            _historyWindow.CloseForExit();
        }

        _historyManager?.Dispose();
        _pasteService?.Dispose();
    }

    private void ShowHistory()
    {
        _historyWindow?.ShowOverlay();
    }

    private void ShowSettings()
    {
        var dialog = new SettingsWindow(_settings.Clone());
        if (_historyWindow is not null && _historyWindow.IsLoaded)
        {
            dialog.Owner = _historyWindow;
        }

        if (dialog.ShowDialog() != true || dialog.ResultSettings is null)
        {
            return;
        }

        _ = ApplySettingsAsync(dialog.ResultSettings);
    }

    private async Task ApplySettingsAsync(AppSettings newSettings)
    {
        if (_hotkeyManager is null)
        {
            return;
        }

        try
        {
            newSettings.Normalize();
            var previous = _settings.Clone();

            if (!_hotkeyManager.Register(newSettings.Hotkey))
            {
                _hotkeyManager.Register(previous.Hotkey);
                System.Windows.MessageBox.Show(
                    "无法注册这个全局快捷键，请换一个组合键。",
                    "PasteTool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _settings = newSettings;
            await _settingsStore.SaveAsync(_settings);

            var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前程序路径。");
            _autoStartService.Apply(_settings.StartWithWindows, processPath, error =>
            {
                _logger.LogWarning($"Auto-start configuration failed: {error}");
                System.Windows.MessageBox.Show(
                    error,
                    "PasteTool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"保存设置失败:\n{ex.Message}",
                "PasteTool",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ClearHistoryAsync()
    {
        if (_historyManager is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            "确定要清空所有剪贴板历史吗？",
            "PasteTool",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        await _historyManager.ClearAsync();
    }

    private async Task PasteAsync(ClipEntry entry, IntPtr targetWindowHandle)
    {
        if (_historyManager is null)
        {
            return;
        }

        await _historyManager.PasteAsync(entry, targetWindowHandle);
    }

    private void Exit()
    {
        Dispose();
        _application.Shutdown();
    }
}
