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
/// Wraps the application's input when shortcuts are mapped: lines are read key by key (with <see cref="LineEditor"/>)
/// so a shortcut is noticed in menus and text fields too, and keys read by arrow-key fields are checked as well. When
/// the input cannot read keys (piped input), it is used as it is and shortcuts are not available.
/// </summary>
internal sealed class ShortcutInput(ITextInput inner, IReadOnlyList<KeyShortcut> shortcuts, TextWriter echo) : ITextInput
{
    /// <summary>Gets the wrapped input.</summary>
    public ITextInput Inner => inner;

    /// <inheritdoc />
    public bool CanReadKeys => inner.CanReadKeys;

    /// <inheritdoc />
    public bool WaitForInput(TimeSpan timeout) => inner.WaitForInput(timeout);

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey()
    {
        var key = inner.ReadKey();
        if (key is { } pressed)
            ThrowIfShortcut(pressed);
        return key;
    }

    /// <inheritdoc />
    public string? ReadLine() => inner.CanReadKeys ? ReadEdited(new LineEditor(EchoWriter())) : inner.ReadLine();

    /// <inheritdoc />
    public string? ReadMasked(char mask)
    {
        if (!inner.CanReadKeys)
            return inner.ReadMasked(mask);

        var editor = new MaskedLineEditor(mask, EchoWriter());
        while (true)
        {
            if (inner.ReadKey() is not { } key)
                return null;

            ThrowIfShortcut(key);
            if (editor.Process(key, out var value))
                return value;
        }
    }

    private string? ReadEdited(LineEditor editor)
    {
        while (true)
        {
            // A scripted input may follow keys with a typed line: hand over to it
            if (!inner.CanReadKeys)
                return editor.Text.Length == 0 ? inner.ReadLine() : editor.Text + inner.ReadLine();

            if (inner.ReadKey() is not { } key)
                return editor.Text.Length == 0 ? null : editor.Text;

            ThrowIfShortcut(key);
            if (editor.Process(key, out var line))
                return line;
        }
    }

    private void ThrowIfShortcut(ConsoleKeyInfo key)
    {
        foreach (var shortcut in shortcuts)
        {
            if (shortcut.Gesture.Matches(key))
                throw new ShortcutPressedException(shortcut, shortcut.Navigate());
        }
    }

    /// <summary>The echo, written as the user's input rather than as output from a view.</summary>
    private TextWriter EchoWriter() => new SuppressedWriter(echo);

    /// <summary>Writes through <see cref="ForeignOutputTracker.Suppress"/>, so echoed keys do not stop resize redraws.</summary>
    private sealed class SuppressedWriter(TextWriter inner) : TextWriter
    {
        public override System.Text.Encoding Encoding => inner.Encoding;

        public override void Write(char value)
        {
            using (ForeignOutputTracker.Suppress())
                inner.Write(value);
            inner.Flush();
        }

        public override void Write(string? value)
        {
            using (ForeignOutputTracker.Suppress())
                inner.Write(value);
            inner.Flush();
        }

        public override void WriteLine()
        {
            using (ForeignOutputTracker.Suppress())
                inner.WriteLine();
            inner.Flush();
        }
    }
}
