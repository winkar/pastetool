using System.Windows.Input;

namespace PasteTool.Core.Models;

public sealed class AppSettings
{
    public HotkeyGesture Hotkey { get; set; } = HotkeyGesture.Default;

    public bool StartWithWindows { get; set; }

    public int MaxEntries { get; set; } = 2000;

    public int MaxImageCacheMb { get; set; } = 512;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Hotkey = new HotkeyGesture
            {
                Key = Hotkey.Key,
                Modifiers = Hotkey.Modifiers,
            },
            StartWithWindows = StartWithWindows,
            MaxEntries = MaxEntries,
            MaxImageCacheMb = MaxImageCacheMb,
        };
    }

    public void Normalize()
    {
        if (Hotkey.Key == Key.None)
        {
            Hotkey = HotkeyGesture.Default;
        }

        MaxEntries = Math.Clamp(MaxEntries, 1, 10000);
        MaxImageCacheMb = Math.Clamp(MaxImageCacheMb, 1, 4096);
    }
}
