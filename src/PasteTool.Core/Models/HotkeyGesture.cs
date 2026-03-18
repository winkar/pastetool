using System.Windows.Input;

namespace PasteTool.Core.Models;

public sealed class HotkeyGesture
{
    public static HotkeyGesture Default { get; } = new()
    {
        Key = Key.V,
        Modifiers = ModifierKeys.Control | ModifierKeys.Alt,
    };

    public Key Key { get; set; } = Key.V;

    public ModifierKeys Modifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Alt;

    public HotkeyGesture Clone()
    {
        return new HotkeyGesture
        {
            Key = Key,
            Modifiers = Modifiers,
        };
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        if (Key != Key.None)
        {
            parts.Add(Key.ToString());
        }

        return parts.Count == 0 ? "未设置" : string.Join("+", parts);
    }
}
