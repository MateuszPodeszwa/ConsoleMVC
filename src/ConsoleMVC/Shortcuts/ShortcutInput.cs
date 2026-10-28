using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Thrown by <see cref="ShortcutInput"/> when a shortcut is pressed, to leave the screen from wherever it is waiting;
/// the application loop catches it and follows <see cref="Navigation"/>.
/// </summary>
internal sealed class ShortcutPressedException(KeyShortcut shortcut, NavigationResult navigation)
    : Exception($"The shortcut {shortcut.Gesture} was pressed.")
{
    /// <summary>Gets the shortcut.</summary>
    public KeyShortcut Shortcut { get; } = shortcut;

    /// <summary>Gets where to go.</summary>
    public NavigationResult Navigation { get; } = navigation;
}

/// <summary>
/// Wraps the application's input when shortcuts are mapped: lines are read key by key (see <see cref="LineEditingInput"/>)
/// so a shortcut is noticed in menus and text fields too, and keys read by arrow-key fields are checked as well. When
/// the input cannot read keys (piped input), it is used as it is and shortcuts are not available.
/// </summary>
internal sealed class ShortcutInput(ITextInput inner, IReadOnlyList<KeyShortcut> shortcuts, TextWriter echo)
    : LineEditingInput(inner, echo)
{
    /// <inheritdoc />
    protected override void OnKey(ConsoleKeyInfo key)
    {
        // The field being answered uses this key now (Esc restoring a changed <number>, for example)
        if (FieldKeyClaim.IsClaimed(key))
            return;

        foreach (var shortcut in shortcuts)
        {
            if (shortcut.Gesture.Matches(key))
                throw new ShortcutPressedException(shortcut, shortcut.Navigate());
        }
    }
}
