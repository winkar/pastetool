using System.Windows;
using System.Windows.Input;
using PasteTool.Core.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace PasteTool.App.Windows;

public partial class SettingsWindow : Window
{
    private HotkeyGesture _hotkey;

    public SettingsWindow(AppSettings currentSettings)
    {
        InitializeComponent();
        _hotkey = currentSettings.Hotkey.Clone();
        HotkeyTextBox.Text = _hotkey.ToString();
        MaxEntriesTextBox.Text = currentSettings.MaxEntries.ToString();
        MaxImageCacheTextBox.Text = currentSettings.MaxImageCacheMb.ToString();
        StartWithWindowsCheckBox.IsChecked = currentSettings.StartWithWindows;
    }

    public AppSettings? ResultSettings { get; private set; }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            return;
        }

        _hotkey = new HotkeyGesture
        {
            Key = key,
            Modifiers = modifiers,
        };

        HotkeyTextBox.Text = _hotkey.ToString();
        e.Handled = true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxEntriesTextBox.Text, out var maxEntries))
        {
            System.Windows.MessageBox.Show("历史条数上限必须是数字。", "PasteTool", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(MaxImageCacheTextBox.Text, out var maxImageCacheMb))
        {
            System.Windows.MessageBox.Show("图片缓存上限必须是数字。", "PasteTool", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var settings = new AppSettings
        {
            Hotkey = _hotkey,
            StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
            MaxEntries = maxEntries,
            MaxImageCacheMb = maxImageCacheMb,
        };
        settings.Normalize();

        ResultSettings = settings;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
