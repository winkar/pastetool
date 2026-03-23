using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PasteTool.App.Models;
using PasteTool.Core.Models;
using PasteTool.Core.Native;
using PasteTool.Core.Services;
using PasteTool.Core.Utilities;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PasteTool.App.Windows;

public partial class HistoryWindow : Window
{
    private const int SearchDebounceMilliseconds = 50;
    private const int SearchResultLimit = 50;
    private readonly ClipboardHistoryManager _historyManager;
    private readonly ISearchService _searchService;
    private readonly Func<string, int, CancellationToken, Task<IReadOnlyList<ClipEntry>>> _searchEntriesAction;
    private readonly Func<ClipEntry, CancellationToken, Task<CapturedClipboardPayload?>> _payloadLoader;
    private readonly Func<ClipEntry, IntPtr, Task> _pasteAction;
    private readonly Action _showSettingsAction;
    private readonly Func<Task> _clearHistoryAction;
    private readonly ObservableCollection<HistoryListItem> _items = new();
    private readonly DispatcherTimer _searchDebounceTimer;
    private CancellationTokenSource? _previewCancellationTokenSource;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private bool _allowClose;
    private bool _isDismissingOverlay;
    private bool _isPasteInProgress;
    private IntPtr _targetWindowHandle;
    private int _searchRequestVersion;
    private static readonly string _logFilePath = Path.Combine(Path.GetTempPath(), "pastetool_debug.log");
    private static readonly object _logLock = new();

    public HistoryWindow(
        ClipboardHistoryManager historyManager,
        ISearchService searchService,
        Func<string, int, CancellationToken, Task<IReadOnlyList<ClipEntry>>> searchEntriesAction,
        Func<ClipEntry, CancellationToken, Task<CapturedClipboardPayload?>> payloadLoader,
        Func<ClipEntry, IntPtr, Task> pasteAction,
        Action showSettingsAction,
        Func<Task> clearHistoryAction)
    {
        InitializeComponent();

        // Initialize log file
        try
        {
            File.WriteAllText(_logFilePath, $"=== PasteTool Debug Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            LogToFile($"Log file location: {_logFilePath}");
        }
        catch
        {
            // Ignore
        }

        _historyManager = historyManager;
        _searchService = searchService;
        _searchEntriesAction = searchEntriesAction;
        _payloadLoader = payloadLoader;
        _pasteAction = pasteAction;
        _showSettingsAction = showSettingsAction;
        _clearHistoryAction = clearHistoryAction;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(SearchDebounceMilliseconds)
        };
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

        HistoryList.ItemsSource = _items;
        _historyManager.HistoryChanged += HistoryManager_HistoryChanged;
        ApplySearchResults(_historyManager.GetEntriesSnapshot(), null);
    }

    public void ShowOverlay()
    {
        _targetWindowHandle = NativeMethods.GetForegroundWindow();
        SearchBox.Text = string.Empty;
        ApplySearchResults(_historyManager.GetEntriesSnapshot(), null);
        PositionWindow();

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();

        // 滚动到列表顶部，显示最新内容
        if (_items.Count > 0)
        {
            Dispatcher.InvokeAsync(() => HistoryList.ScrollIntoView(_items[0]), DispatcherPriority.Loaded);
        }
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            DismissOverlay();
            return;
        }

        _historyManager.HistoryChanged -= HistoryManager_HistoryChanged;
        _previewCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchDebounceTimer.Stop();
        base.OnClosing(e);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        if (IsVisible)
        {
            DismissOverlay();
        }
    }

    private void HistoryManager_HistoryChanged(object? sender, ClipboardHistoryChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                ApplySearchResults(e.Entries, null);
                return;
            }

            _ = RefreshSearchResultsAsync(e.Entries);
        });
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + Math.Max(40, (workArea.Height - Height) / 5);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _searchDebounceTimer.Stop();
        _ = RefreshSearchResultsAsync();
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Tab:
                if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                {
                    HistoryList.Focus();
                    if (HistoryList.SelectedIndex >= 0)
                    {
                        var item = HistoryList.ItemContainerGenerator.ContainerFromIndex(HistoryList.SelectedIndex) as ListBoxItem;
                        item?.Focus();
                    }
                    e.Handled = true;
                }
                break;
        }
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DismissOverlay();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await PasteSelectedAsync();
        }
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = UpdatePreviewAsync((HistoryList.SelectedItem as HistoryListItem)?.Entry);
    }

    private async void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 确保点击的是列表项而非空白区域
        if (ItemsControl.ContainerFromElement(HistoryList, e.OriginalSource as DependencyObject) is not ListBoxItem item ||
            item.DataContext is not HistoryListItem historyItem)
        {
            return;
        }

        HistoryList.SelectedItem = historyItem;
        e.Handled = true;
        await PasteSelectedAsync(historyItem.Entry);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        DismissOverlay();
        _showSettingsAction();
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        DismissOverlay();
        await _clearHistoryAction();
    }

    private void MoveSelection(int delta)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var index = HistoryList.SelectedIndex;
        if (index < 0)
        {
            index = 0;
        }
        else
        {
            index = Math.Clamp(index + delta, 0, _items.Count - 1);
        }

        HistoryList.SelectedIndex = index;
        HistoryList.ScrollIntoView(HistoryList.SelectedItem);
    }

    private async Task RefreshSearchResultsAsync(IReadOnlyList<ClipEntry>? fallbackEntries = null)
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _searchCancellationTokenSource.Token;
        var requestVersion = Interlocked.Increment(ref _searchRequestVersion);
        var searchQuery = SearchBox.Text;
        var searchEntries = fallbackEntries ?? _historyManager.GetEntriesSnapshot();

        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            ApplySearchResults(searchEntries, null);
            return;
        }

        if (SearchNormalizer.ContainsCjk(searchQuery))
        {
            try
            {
                var inMemoryResults = await SearchInMemoryAsync(searchEntries, searchQuery, cancellationToken);
                ApplySearchResultsOnUiThread(inMemoryResults, searchQuery, requestVersion);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancelled searches.
            }
            return;
        }

        try
        {
            var results = await _searchEntriesAction(searchQuery, SearchResultLimit, cancellationToken);
            if (results.Count == 0)
            {
                results = await SearchInMemoryAsync(searchEntries, searchQuery, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || requestVersion != _searchRequestVersion)
            {
                return;
            }

            ApplySearchResultsOnUiThread(results, searchQuery, requestVersion);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancelled searches.
        }
        catch
        {
            if (cancellationToken.IsCancellationRequested || requestVersion != _searchRequestVersion)
            {
                return;
            }

            var fallbackResults = await SearchInMemoryAsync(searchEntries, searchQuery, cancellationToken);
            ApplySearchResultsOnUiThread(fallbackResults, searchQuery, requestVersion);
        }
    }

    private Task<IReadOnlyList<ClipEntry>> SearchInMemoryAsync(IReadOnlyList<ClipEntry> entries, string searchQuery, CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<ClipEntry>>(
            () => _searchService.Search(entries, searchQuery).Take(SearchResultLimit).ToArray(),
            cancellationToken);
    }

    private void ApplySearchResultsOnUiThread(IReadOnlyList<ClipEntry> results, string searchQuery, int requestVersion)
    {
        Dispatcher.Invoke(() =>
        {
            if (requestVersion != _searchRequestVersion || !string.Equals(SearchBox.Text, searchQuery, StringComparison.Ordinal))
            {
                return;
            }

            ApplySearchResults(results, searchQuery);
        });
    }

    private void ApplySearchResults(IReadOnlyList<ClipEntry> results, string? searchQuery)
    {
        var selectedHash = (HistoryList.SelectedItem as HistoryListItem)?.Entry.ContentHash;
        var desiredItems = BuildDesiredItems(results, searchQuery);

        SyncVisibleItems(desiredItems);

        EmptyStateText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_items.Count == 0)
        {
            HistoryList.SelectedItem = null;
            ResetPreview();
            return;
        }

        var preferred = _items.FirstOrDefault(item => item.Entry.ContentHash == selectedHash) ?? _items[0];
        if (!ReferenceEquals(HistoryList.SelectedItem, preferred))
        {
            HistoryList.SelectedItem = preferred;
        }
    }

    private List<HistoryListItem> BuildDesiredItems(IReadOnlyList<ClipEntry> results, string? searchQuery)
    {
        var existingItems = _items.ToDictionary(item => item.Entry.ContentHash, StringComparer.Ordinal);
        var desiredItems = new List<HistoryListItem>(results.Count);

        foreach (var entry in results)
        {
            if (existingItems.TryGetValue(entry.ContentHash, out var existingItem))
            {
                existingItem.UpdateEntry(entry);
                existingItem.UpdateSearchQuery(searchQuery);
                desiredItems.Add(existingItem);
                continue;
            }

            desiredItems.Add(new HistoryListItem(entry, searchQuery));
        }

        return desiredItems;
    }

    private void SyncVisibleItems(IReadOnlyList<HistoryListItem> desiredItems)
    {
        var desiredHashes = desiredItems
            .Select(item => item.Entry.ContentHash)
            .ToHashSet(StringComparer.Ordinal);

        for (var index = _items.Count - 1; index >= 0; index--)
        {
            if (!desiredHashes.Contains(_items[index].Entry.ContentHash))
            {
                _items.RemoveAt(index);
            }
        }

        for (var index = 0; index < desiredItems.Count; index++)
        {
            var desiredItem = desiredItems[index];
            if (index < _items.Count && ReferenceEquals(_items[index], desiredItem))
            {
                continue;
            }

            var existingIndex = _items.IndexOf(desiredItem);
            if (existingIndex >= 0)
            {
                _items.Move(existingIndex, index);
                continue;
            }

            if (index <= _items.Count)
            {
                _items.Insert(index, desiredItem);
            }
            else
            {
                _items.Add(desiredItem);
            }
        }

        while (_items.Count > desiredItems.Count)
        {
            _items.RemoveAt(_items.Count - 1);
        }
    }

    private void DismissOverlay()
    {
        if (_isDismissingOverlay || !IsVisible)
        {
            return;
        }

        _isDismissingOverlay = true;
        try
        {
            _searchDebounceTimer.Stop();
            _previewCancellationTokenSource?.Cancel();
            _previewCancellationTokenSource?.Dispose();
            _previewCancellationTokenSource = null;
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            _searchCancellationTokenSource = null;
            Hide();
        }
        finally
        {
            _isDismissingOverlay = false;
        }
    }

    private async Task PasteSelectedAsync(ClipEntry? entry = null)
    {
        if (_isPasteInProgress)
        {
            return;
        }

        var selected = entry ?? (HistoryList.SelectedItem as HistoryListItem)?.Entry;
        if (selected is null)
        {
            return;
        }

        _isPasteInProgress = true;

        // 在窗口隐藏之前（仍是前台时）允许其他进程切换前台
        // Windows 限制：Hide() 之后 PasteTool 不再是前台进程，SetForegroundWindow 会静默失败
        NativeMethods.AllowSetForegroundWindow(NativeMethods.AsfwAny);
        DismissOverlay();

        try
        {
            await _pasteAction(selected, _targetWindowHandle);
            ShowPasteFeedback("已粘贴");
        }
        catch (Exception ex)
        {
            ShowPasteFeedback($"粘贴失败: {ex.Message}");
        }
        finally
        {
            _isPasteInProgress = false;
        }
    }

    private void ShowPasteFeedback(string message)
    {
        Dispatcher.InvokeAsync(() =>
        {
            FeedbackText.Text = message;
            FeedbackPanel.Visibility = Visibility.Visible;

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                FeedbackPanel.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        });
    }

    private async Task UpdatePreviewAsync(ClipEntry? entry)
    {
        _previewCancellationTokenSource?.Cancel();
        _previewCancellationTokenSource?.Dispose();
        _previewCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _previewCancellationTokenSource.Token;

        ResetPreview();

        if (entry is null)
        {
            return;
        }

        PreviewMetaText.Text = $"{entry.Formats} · {entry.CapturedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

        if (entry.Kind == ClipKind.Image)
        {
            var msg = $"UpdatePreviewAsync: showing image for entry {entry.ContentHash}, path: {entry.BlobPath}";
            Debug.WriteLine(msg);
            LogToFile(msg);
            ShowImage(entry.BlobPath);

            if (cancellationToken.IsCancellationRequested)
            {
                var cancelMsg = $"UpdatePreviewAsync: cancelled after ShowImage for {entry.ContentHash}";
                Debug.WriteLine(cancelMsg);
                LogToFile(cancelMsg);
                ResetPreview();
            }
            return;
        }

        ShowLoadingIndicator(true);

        try
        {
            var payload = await _payloadLoader(entry, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var plainText = payload?.UnicodeText;
            if (string.IsNullOrWhiteSpace(plainText) && !string.IsNullOrWhiteSpace(payload?.Html))
            {
                plainText = HtmlTextExtractor.ExtractPlainText(payload.Html!);
            }

            if (!string.IsNullOrWhiteSpace(payload?.Rtf))
            {
                ShowRichText(payload.Rtf!);
                if (!string.IsNullOrWhiteSpace(plainText))
                {
                    PreviewMetaText.Text = $"{PreviewMetaText.Text} · {SearchNormalizer.CollapseWhitespace(plainText)[..Math.Min(80, SearchNormalizer.CollapseWhitespace(plainText).Length)]}";
                }

                ShowLoadingIndicator(false);
                return;
            }

            // 尝试以表格形式显示 HTML（如 Excel 复制的行数据）
            if (!string.IsNullOrWhiteSpace(payload?.Html))
            {
                var tableText = HtmlTextExtractor.TryExtractTableText(payload.Html!);
                if (!string.IsNullOrWhiteSpace(tableText))
                {
                    ShowText(tableText);
                    ShowLoadingIndicator(false);
                    return;
                }
            }

            ShowText(string.IsNullOrWhiteSpace(plainText) ? entry.PreviewText : plainText);
            ShowLoadingIndicator(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ShowLoadingIndicator(false);
        }
        catch
        {
            ShowLoadingIndicator(false);
            ShowText(entry.PreviewText);
        }
    }

    private void ShowLoadingIndicator(bool show)
    {
        LoadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ResetPreview()
    {
        PlaceholderBorder.Visibility = Visibility.Visible;
        PlaceholderText.Text = "暂无预览";
        ImagePreview.Visibility = Visibility.Collapsed;
        RichTextPreview.Visibility = Visibility.Collapsed;
        TextPreviewScroll.Visibility = Visibility.Collapsed;
        LoadingIndicator.Visibility = Visibility.Collapsed;
        TextPreview.Text = string.Empty;
        RichTextPreview.Document = new FlowDocument();
        ImagePreview.Source = null;
        PreviewMetaText.Text = "选择一条历史记录查看预览";
    }

    private void ShowImage(string? imagePath)
    {
        var msg1 = $"ShowImage: called with path: {imagePath}";
        Debug.WriteLine(msg1);
        LogToFile(msg1);

        var image = LoadBitmapImage(imagePath);
        if (image is null)
        {
            var msg2 = $"ShowImage: LoadBitmapImage returned null for {imagePath}";
            Debug.WriteLine(msg2);
            LogToFile(msg2);

            // Force immediate UI update using Dispatcher
            Dispatcher.Invoke(() =>
            {
                PlaceholderBorder.Visibility = Visibility.Visible;
                PlaceholderText.Text = "图片文件已丢失";
                ImagePreview.Visibility = Visibility.Collapsed;
                ImagePreview.Source = null;
            }, DispatcherPriority.Render);
            return;
        }

        var msg3 = "ShowImage: successfully loaded image, setting visibility";
        Debug.WriteLine(msg3);
        LogToFile(msg3);

        // Force immediate UI update using Dispatcher
        Dispatcher.Invoke(() =>
        {
            PlaceholderBorder.Visibility = Visibility.Collapsed;
            ImagePreview.Source = image;
            ImagePreview.Visibility = Visibility.Visible;
        }, DispatcherPriority.Render);

        var msg4 = "ShowImage: image display complete";
        Debug.WriteLine(msg4);
        LogToFile(msg4);
    }

    private void ShowRichText(string rtf)
    {
        PlaceholderBorder.Visibility = Visibility.Collapsed;
        RichTextPreview.Visibility = Visibility.Visible;

        var document = new FlowDocument();
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));

        try
        {
            range.Load(stream, System.Windows.DataFormats.Rtf);
            RichTextPreview.Document = document;
        }
        catch
        {
            ShowText(RichTextUtilities.ExtractPlainText(rtf));
        }
    }

    private void ShowText(string text)
    {
        PlaceholderBorder.Visibility = Visibility.Collapsed;
        TextPreviewScroll.Visibility = Visibility.Visible;
        TextPreview.Text = text;
    }

    private static BitmapImage? LoadBitmapImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var msg = "LoadBitmapImage: path is null or empty";
            Debug.WriteLine(msg);
            LogToFile(msg);
            return null;
        }

        if (!File.Exists(path))
        {
            var msg = $"LoadBitmapImage: file not found at path: {path}";
            Debug.WriteLine(msg);
            LogToFile(msg);
            return null;
        }

        try
        {
            var msg1 = $"LoadBitmapImage: attempting to load {path}";
            Debug.WriteLine(msg1);
            LogToFile(msg1);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var msg2 = $"LoadBitmapImage: successfully loaded {path}";
            Debug.WriteLine(msg2);
            LogToFile(msg2);
            return bitmap;
        }
        catch (Exception ex)
        {
            var msg = $"LoadBitmapImage: exception loading {path}: {ex.GetType().Name} - {ex.Message}";
            Debug.WriteLine(msg);
            LogToFile(msg);
            var stackMsg = $"LoadBitmapImage: stack trace: {ex.StackTrace}";
            Debug.WriteLine(stackMsg);
            LogToFile(stackMsg);
            return null;
        }
    }

    private static void LogToFile(string message)
    {
        try
        {
            lock (_logLock)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(_logFilePath, $"[{timestamp}] {message}\n");
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
