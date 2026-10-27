using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// The key-by-key editing behind <see cref="ConsoleTextInput.ReadMasked"/>: collects typed characters, echoes one
/// mask character per character (not per UTF-16 unit, so emoji show a single mask), handles Backspace and
/// treats Ctrl+D or Ctrl+Z on an empty line as the end of input.
/// </summary>
/// <example>
/// <code>
/// var editor = new MaskedLineEditor('*', Console.Out);
/// string? value;
/// while (!editor.Process(Console.ReadKey(intercept: true), out value)) { }
/// </code>
/// </example>
internal sealed class MaskedLineEditor
{
    private readonly char _mask;
    private readonly TextWriter _echo;
    private readonly StringBuilder _value = new();

    /// <summary>
    /// Creates an editor.
    /// </summary>
    /// <param name="mask">The character echoed for each typed character.</param>
    /// <param name="echo">Where the mask characters and the final line break are written.</param>
    public MaskedLineEditor(char mask, TextWriter echo)
    {
        _mask = mask;
        _echo = echo;
    }

    /// <summary>
    /// Applies one key press.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="result">When the line is finished: the value, or <see langword="null"/> at the end of input.</param>
    /// <returns><see langword="true"/> when the line is finished (Enter, or end of input on an empty line).</returns>
    public bool Process(ConsoleKeyInfo key, out string? result)
    {
        result = null;

        if (key.Key == ConsoleKey.Enter || key.KeyChar is '\r' or '\n')
        {
            _echo.WriteLine();
            result = _value.ToString();
            return true;
        }

        // Ctrl+D (Unix) or Ctrl+Z (Windows) on an empty line ends the input, like ReadLine returning null
        if (key.KeyChar is '\u0004' or '\u001a' && _value.Length == 0)
        {
            _echo.WriteLine();
            return true;
        }

        if (key.Key == ConsoleKey.Backspace || key.KeyChar == '\b')
        {
            RemoveLastCharacter();
            return false;
        }

        var c = key.KeyChar;
        if (char.IsControl(c))
            return false;

        _value.Append(c);

        // A high surrogate is only half a character: echo the mask when its low surrogate arrives
        if (!char.IsHighSurrogate(c))
            _echo.Write(_mask);

        return false;
    }

    private void RemoveLastCharacter()
    {
        if (_value.Length == 0)
            return;

        var remove = _value.Length >= 2 && char.IsLowSurrogate(_value[^1]) && char.IsHighSurrogate(_value[^2]) ? 2 : 1;
        _value.Length -= remove;
        _echo.Write("\b \b");
    }
}
