using System.Globalization;
using System.Text;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Turns the characters a remote terminal sends into key presses, as <see cref="Console.ReadKey(bool)"/> does for the
/// local console: Enter, Backspace, Tab, control keys, and the escape sequences of the arrows, Home, End, Insert, Delete,
/// Page Up, Page Down and F1 to F12, with Shift, Alt and Ctrl. Mouse reports (<c>ESC [ &lt;</c>) are passed on as typed
/// for <see cref="Rendering.MouseDecoder"/>, and window size reports (<c>ESC [ 8 ; rows ; columns t</c>) are handed to
/// a callback instead of becoming keys. See docs/design/remote-sessions.md, section 5.
/// </summary>
/// <param name="source">Reads the next character: waits at most the timeout, or for ever when it is <see langword="null"/>.</param>
/// <param name="windowSize">Called with the columns and rows of a window size report; <see langword="null"/> to ignore them.</param>
/// <example>
/// <code>
/// var decoder = new TerminalKeyDecoder(source, (columns, rows) => terminal.Resize(columns, rows));
/// ConsoleKeyInfo? key = decoder.Next(); // null at the end of the input
/// </code>
/// </example>
internal sealed class TerminalKeyDecoder(TerminalKeyDecoder.CharSource source, Action<int, int>? windowSize = null)
{
    /// <summary>Reads one character.</summary>
    /// <param name="timeout">How long to wait, or <see langword="null"/> to wait until one arrives or the input ends.</param>
    /// <param name="character">The character read.</param>
    /// <returns><see langword="false"/> when the time passed or the input ended.</returns>
    internal delegate bool CharSource(TimeSpan? timeout, out char character);

    /// <summary>
    /// How long to wait for the rest of an escape sequence before taking Escape as the key itself: 50 ms, longer than
    /// on the local console because the sequence crosses a network.
    /// </summary>
    public static readonly TimeSpan SequenceGap = TimeSpan.FromMilliseconds(50);

    /// <summary>The longest control sequence read; anything longer is passed on as typed.</summary>
    private const int MaxSequenceLength = 16;

    private const char Escape = '\u001b';

    /// <summary>Keys decoded but not returned yet (the characters of a sequence passed on as typed).</summary>
    private readonly Queue<ConsoleKeyInfo> _pending = new();

    /// <summary>A character read ahead that belongs to the next key (the one after a carriage return).</summary>
    private char? _readAhead;

    /// <summary>Gets whether a key can be returned without reading anything.</summary>
    /// <example><code>if (decoder.HasPending) { var key = decoder.Next(); }</code></example>
    public bool HasPending => _pending.Count > 0 || _readAhead is not null;

    /// <summary>Returns the next key press.</summary>
    /// <returns>The key, or <see langword="null"/> when the input ended.</returns>
    /// <example><code>while (decoder.Next() is { } key) Handle(key);</code></example>
    public ConsoleKeyInfo? Next()
    {
        while (true)
        {
            if (_pending.TryDequeue(out var queued))
                return queued;

            if (!Read(null, out var c))
                return null;

            if (Decode(c) is { } key)
                return key;
        }
    }

    /// <summary>Decodes the key that starts with <paramref name="c"/>; <see langword="null"/> when it is not a key.</summary>
    private ConsoleKeyInfo? Decode(char c)
    {
        switch (c)
        {
            case '\r':
                // Telnet sends CR LF or CR NUL for Enter; a terminal in raw mode sends CR alone
                if (Read(SequenceGap, out var next) && next is not ('\n' or '\0'))
                    _readAhead = next;
                return Key('\r', ConsoleKey.Enter);

            case '\n':
                return Key('\r', ConsoleKey.Enter);

            case '\0':
                return null;

            case '\u007f' or '\b':
                return Key('\b', ConsoleKey.Backspace);

            case '\t':
                return Key('\t', ConsoleKey.Tab);

            case Escape:
                return DecodeEscape();

            case < ' ':
                // Ctrl+A is 1, ..., Ctrl+Z is 26; the rest (Ctrl+\ and so on) have no key of their own
                return c <= '\u001a'
                    ? new ConsoleKeyInfo(c, ConsoleKey.A + (c - 1), shift: false, alt: false, control: true)
                    : Key(c, ConsoleKey.NoName);

            default:
                return CharKey(c, alt: false);
        }
    }

    /// <summary>Decodes what follows an Escape.</summary>
    private ConsoleKeyInfo? DecodeEscape()
    {
        if (!Read(SequenceGap, out var c))
            return Key(Escape, ConsoleKey.Escape);

        switch (c)
        {
            case '[':
                return DecodeControlSequence();

            case 'O':
                if (!Read(SequenceGap, out var final))
                    return CharKey('O', alt: true);

                var key = final switch
                {
                    'P' => ConsoleKey.F1,
                    'Q' => ConsoleKey.F2,
                    'R' => ConsoleKey.F3,
                    'S' => ConsoleKey.F4,
                    'H' => ConsoleKey.Home,
                    'F' => ConsoleKey.End,
                    'A' => ConsoleKey.UpArrow,
                    'B' => ConsoleKey.DownArrow,
                    'C' => ConsoleKey.RightArrow,
                    'D' => ConsoleKey.LeftArrow,
                    _ => (ConsoleKey?)null
                };

                if (key is { } known)
                    return Key('\0', known);

                return PassOn(Escape, 'O', final);

            case Escape:
                // Escape pressed twice: the first is a key, the second starts again
                _readAhead = Escape;
                return Key(Escape, ConsoleKey.Escape);

            case >= ' ' and not '\u007f':
                return CharKey(c, alt: true);

            default:
                _readAhead = c;
                return Key(Escape, ConsoleKey.Escape);
        }
    }

    /// <summary>Decodes <c>ESC [ parameters final</c>.</summary>
    private ConsoleKeyInfo? DecodeControlSequence()
    {
        var text = new StringBuilder();
        while (true)
        {
            if (text.Length >= MaxSequenceLength || !Read(SequenceGap, out var c))
                return PassOn([Escape, '[', .. text.ToString()]);

            // A mouse report is read by MouseDecoder, one character at a time
            if (text.Length == 0 && c == '<')
                return PassOn(Escape, '[', '<');

            if (c is >= '@' and <= '~')
                return TryControlSequence(text.ToString(), c, out var key) ? key : PassOn([Escape, '[', .. text.ToString(), c]);

            text.Append(c);
        }
    }

    /// <summary>
    /// Understands a complete control sequence: <paramref name="key"/> is its key, or <see langword="null"/> for a
    /// report that is not a key. Returns <see langword="false"/> for a sequence it does not know.
    /// </summary>
    private bool TryControlSequence(string parameters, char final, out ConsoleKeyInfo? key)
    {
        key = null;
        var numbers = parameters.Split(';');
        if (numbers.Any(n => n.Length > 0 && !n.All(char.IsAsciiDigit)))
            return false;

        int Number(int index, int fallback) =>
            index < numbers.Length && int.TryParse(numbers[index], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        if (final == 't')
        {
            // ESC [ 8 ; rows ; columns t answers ESC [ 18 t
            if (numbers.Length != 3 || Number(0, 0) != 8)
                return false;

            windowSize?.Invoke(Number(2, 0), Number(1, 0));
            return true;
        }

        var modifiers = Number(1, 1) - 1;
        ConsoleKey? named = final switch
        {
            'A' => ConsoleKey.UpArrow,
            'B' => ConsoleKey.DownArrow,
            'C' => ConsoleKey.RightArrow,
            'D' => ConsoleKey.LeftArrow,
            'H' => ConsoleKey.Home,
            'F' => ConsoleKey.End,
            'P' => ConsoleKey.F1,
            'Q' => ConsoleKey.F2,
            'R' => ConsoleKey.F3,
            'S' => ConsoleKey.F4,
            'Z' => ConsoleKey.Tab,
            '~' => TildeKey(Number(0, 0)),
            _ => null
        };

        if (named is not { } found)
            return false;

        key = final == 'Z'
            ? new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift: true, alt: false, control: false)
            : new ConsoleKeyInfo('\0', found, shift: (modifiers & 1) != 0, alt: (modifiers & 2) != 0, control: (modifiers & 4) != 0);
        return true;
    }

    /// <summary>The key of <c>ESC [ n ~</c>.</summary>
    private static ConsoleKey? TildeKey(int code) => code switch
    {
        1 or 7 => ConsoleKey.Home,
        2 => ConsoleKey.Insert,
        3 => ConsoleKey.Delete,
        4 or 8 => ConsoleKey.End,
        5 => ConsoleKey.PageUp,
        6 => ConsoleKey.PageDown,
        11 => ConsoleKey.F1,
        12 => ConsoleKey.F2,
        13 => ConsoleKey.F3,
        14 => ConsoleKey.F4,
        15 => ConsoleKey.F5,
        17 => ConsoleKey.F6,
        18 => ConsoleKey.F7,
        19 => ConsoleKey.F8,
        20 => ConsoleKey.F9,
        21 => ConsoleKey.F10,
        23 => ConsoleKey.F11,
        24 => ConsoleKey.F12,
        _ => null
    };

    /// <summary>Returns the first character as a key and queues the others, as if each had been typed.</summary>
    private ConsoleKeyInfo PassOn(params char[] typed)
    {
        foreach (var c in typed.Skip(1))
            _pending.Enqueue(c == Escape ? Key(Escape, ConsoleKey.Escape) : CharKey(c, alt: false));

        return typed[0] == Escape ? Key(Escape, ConsoleKey.Escape) : CharKey(typed[0], alt: false);
    }

    /// <summary>The key of a printable character, as <c>ScriptedInput.Char</c> reports it.</summary>
    private static ConsoleKeyInfo CharKey(char c, bool alt)
    {
        var key = c switch
        {
            >= '0' and <= '9' => ConsoleKey.D0 + (c - '0'),
            >= 'a' and <= 'z' => ConsoleKey.A + (c - 'a'),
            >= 'A' and <= 'Z' => ConsoleKey.A + (c - 'A'),
            ' ' => ConsoleKey.Spacebar,
            _ => ConsoleKey.NoName
        };

        return new ConsoleKeyInfo(c, key, shift: c is >= 'A' and <= 'Z', alt: alt, control: false);
    }

    private static ConsoleKeyInfo Key(char c, ConsoleKey key) => new(c, key, shift: false, alt: false, control: false);

    /// <summary>Reads the character read ahead first, then from the source.</summary>
    private bool Read(TimeSpan? timeout, out char c)
    {
        if (_readAhead is { } ahead)
        {
            _readAhead = null;
            c = ahead;
            return true;
        }

        return source(timeout, out c);
    }
}
