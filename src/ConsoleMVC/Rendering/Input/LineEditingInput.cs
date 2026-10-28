namespace ConsoleMVC.Rendering;

/// <summary>
/// Reads lines key by key with <see cref="LineEditor"/> (typing, Backspace, Delete, Left, Right, Home, End), so every key
/// passes through <see cref="OnKey"/> first: keyboard shortcuts and mouse clicks are noticed in text fields and menus too.
/// When the input cannot read keys (piped input), lines are read from it as they are.
/// </summary>
/// <param name="inner">The input to read keys from.</param>
/// <param name="echo">Where typed characters are echoed, usually the console; <see langword="null"/> for nowhere.</param>
internal class LineEditingInput(ITextInput inner, TextWriter? echo = null) : ITextInput
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
            OnKey(pressed);
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

            OnKey(key);
            if (editor.Process(key, out var value))
                return value;
        }
    }

    /// <summary>Called for every key read, before it is used; may throw to leave the screen (a shortcut).</summary>
    /// <param name="key">The key.</param>
    protected virtual void OnKey(ConsoleKeyInfo key)
    {
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

            OnKey(key);
            if (editor.Process(key, out var line))
                return line;
        }
    }

    /// <summary>The echo, written as the user's input rather than as output from a view.</summary>
    private TextWriter EchoWriter() => echo is null ? TextWriter.Null : new SuppressedWriter(echo);

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
