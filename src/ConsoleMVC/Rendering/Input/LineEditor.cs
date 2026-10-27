using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Key-by-key line editing with echo, used when the framework reads keys itself instead of
/// <see cref="Console.ReadLine"/> (for example to notice keyboard shortcuts while a line is typed). Supports typing,
/// Backspace, Delete, Left, Right, Home and End; Enter finishes the line, and Ctrl+D or Ctrl+Z on an empty line ends
/// the input. Characters are assumed to be one column wide.
/// </summary>
/// <example>
/// <code>
/// var editor = new LineEditor(Console.Out);
/// string? line;
/// while (!editor.Process(Console.ReadKey(intercept: true), out line)) { }
/// </code>
/// </example>
internal sealed class LineEditor
{
    private readonly TextWriter _echo;
    private readonly StringBuilder _text = new();
    private int _cursor;

    /// <summary>
    /// Creates an editor.
    /// </summary>
    /// <param name="echo">Where typed characters, cursor movements (backspace characters) and the final line break are written.</param>
    public LineEditor(TextWriter echo) => _echo = echo;

    /// <summary>Gets the text typed so far.</summary>
    public string Text => _text.ToString();

    /// <summary>
    /// Applies one key press.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="result">When the line is finished: the line, or <see langword="null"/> at the end of input.</param>
    /// <returns><see langword="true"/> when the line is finished.</returns>
    public bool Process(ConsoleKeyInfo key, out string? result)
    {
        result = null;

        if (key.Key == ConsoleKey.Enter || key.KeyChar is '\r' or '\n')
        {
            _echo.WriteLine();
            result = _text.ToString();
            return true;
        }

        if (key.KeyChar is '\u0004' or '\u001a' && _text.Length == 0)
        {
            _echo.WriteLine();
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.Backspace:
                if (_cursor > 0)
                {
                    _cursor--;
                    _text.Remove(_cursor, 1);
                    _echo.Write('\b');
                    RewriteTail(1);
                }
                return false;
            case ConsoleKey.Delete:
                if (_cursor < _text.Length)
                {
                    _text.Remove(_cursor, 1);
                    RewriteTail(1);
                }
                return false;
            case ConsoleKey.LeftArrow:
                if (_cursor > 0)
                {
                    _cursor--;
                    _echo.Write('\b');
                }
                return false;
            case ConsoleKey.RightArrow:
                if (_cursor < _text.Length)
                {
                    _echo.Write(_text[_cursor]);
                    _cursor++;
                }
                return false;
            case ConsoleKey.Home:
                _echo.Write(new string('\b', _cursor));
                _cursor = 0;
                return false;
            case ConsoleKey.End:
                _echo.Write(_text.ToString(_cursor, _text.Length - _cursor));
                _cursor = _text.Length;
                return false;
        }

        var c = key.KeyChar;
        if (char.IsControl(c))
            return false;

        _text.Insert(_cursor, c);
        _cursor++;
        _echo.Write(c);
        RewriteTail(0);
        return false;
    }

    /// <summary>Writes the text after the cursor again, blanks <paramref name="erased"/> columns, and moves the cursor back.</summary>
    private void RewriteTail(int erased)
    {
        var tail = _text.ToString(_cursor, _text.Length - _cursor);
        if (tail.Length == 0 && erased == 0)
            return;

        _echo.Write(tail + new string(' ', erased) + new string('\b', tail.Length + erased));
    }
}
