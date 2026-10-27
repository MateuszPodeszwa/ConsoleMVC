using System.Globalization;

namespace ConsoleMVC.Mvc;

/// <summary>
/// A key with its modifiers, such as <c>F1</c>, <c>Esc</c> or <c>Ctrl+X</c>, for <see cref="MvcApplication.MapKey(KeyGesture, NavigationResult, string?)"/>.
/// </summary>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">Control, Alt and Shift.</param>
/// <example>
/// <code>
/// var help = new KeyGesture(ConsoleKey.F1);
/// var quit = KeyGesture.Parse("Ctrl+X");
/// </code>
/// </example>
public readonly record struct KeyGesture(ConsoleKey Key, ConsoleModifiers Modifiers = 0)
{
    /// <summary>
    /// Reads a gesture such as <c>F1</c>, <c>Esc</c>, <c>Ctrl+X</c> or <c>Alt+Shift+S</c> (case-insensitive).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The gesture.</returns>
    /// <exception cref="FormatException">Thrown when the text is not a gesture.</exception>
    /// <example><code>var gesture = KeyGesture.Parse("ctrl+s");</code></example>
    public static KeyGesture Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        ConsoleModifiers modifiers = 0;

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ConsoleModifiers.Control,
                "alt" => ConsoleModifiers.Alt,
                "shift" => ConsoleModifiers.Shift,
                _ => throw new FormatException($"'{part}' in '{text}' is not Ctrl, Alt or Shift.")
            };
        }

        var name = parts[^1];
        ConsoleKey key;
        if (string.Equals(name, "Esc", StringComparison.OrdinalIgnoreCase))
            key = ConsoleKey.Escape;
        else if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]))
            key = char.IsAsciiDigit(name[0]) ? ConsoleKey.D0 + (name[0] - '0') : ConsoleKey.A + (char.ToUpperInvariant(name[0]) - 'A');
        else if (!Enum.TryParse(name, ignoreCase: true, out key) || int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            throw new FormatException($"'{name}' in '{text}' is not a key; use names such as F1, Esc, Q or PageUp.");

        return new KeyGesture(key, modifiers);
    }

    /// <summary>Gets whether <paramref name="pressed"/> is this gesture.</summary>
    /// <param name="pressed">The key that was pressed.</param>
    /// <example><code>if (gesture.Matches(Console.ReadKey(true))) { }</code></example>
    public bool Matches(ConsoleKeyInfo pressed) => pressed.Key == Key && pressed.Modifiers == Modifiers;

    /// <summary>The gesture as people write it: <c>Ctrl+X</c>, <c>F1</c>, <c>Esc</c>.</summary>
    public override string ToString()
    {
        var name = Key switch
        {
            ConsoleKey.Escape => "Esc",
            >= ConsoleKey.D0 and <= ConsoleKey.D9 => ((char)('0' + (Key - ConsoleKey.D0))).ToString(),
            _ => Key.ToString()
        };

        return (Modifiers.HasFlag(ConsoleModifiers.Control) ? "Ctrl+" : "")
               + (Modifiers.HasFlag(ConsoleModifiers.Alt) ? "Alt+" : "")
               + (Modifiers.HasFlag(ConsoleModifiers.Shift) ? "Shift+" : "")
               + name;
    }
}
