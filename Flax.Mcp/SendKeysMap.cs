using System;
using System.Collections.Generic;
using Flax.Windows;

namespace Flax.Mcp;

/// <summary>Maps a special-key name to an action on FlaxKeyboard. Use type_text for arbitrary text.</summary>
public static class SendKeysMap
{
    private static readonly Dictionary<string, Action<FlaxKeyboard>> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTER"] = k => k.Enter(),
            ["ESC"] = k => k.Esc(),
            ["TAB"] = k => k.Tab(),
            ["SPACE"] = k => k.Space(),
            ["BACKSPACE"] = k => k.BackSpace(),
            ["DELETE"] = k => k.Delete(),
            ["UP"] = k => k.Up(),
            ["DOWN"] = k => k.Down(),
            ["LEFT"] = k => k.Left(),
            ["RIGHT"] = k => k.Right(),
            ["CTRL+A"] = k => k.CtrlA(),
            ["CTRL+C"] = k => k.CtrlC(),
            ["CTRL+V"] = k => k.CtrlV(),
        };

    public static bool TryGet(string keys, out Action<FlaxKeyboard> action)
        => Map.TryGetValue((keys ?? string.Empty).Trim(), out action!);
}
