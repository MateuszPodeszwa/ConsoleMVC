using ConsoleMVC.Rendering;

namespace ConsoleMVC.Tests.Rendering;

/// <summary>
/// An <see cref="ITextInput"/> that reports key support and returns pre-scripted keys, then <see langword="null"/>
/// (end of input). Lines are not supported: an element that reads a line from it fails the test.
/// </summary>
/// <example><code>var keys = new ScriptedKeys(ConsoleKey.DownArrow, ConsoleKey.Enter);</code></example>
public sealed class ScriptedKeys : ITextInput
{
    private readonly Queue<ConsoleKeyInfo> _keys;

    /// <summary>Creates the input from special keys such as arrows, Space and Enter.</summary>
    /// <param name="keys">The keys, in order.</param>
    public ScriptedKeys(params ConsoleKey[] keys) =>
        _keys = new Queue<ConsoleKeyInfo>(keys.Select(Key));

    /// <summary>Creates the input from key information, for typed characters such as digits.</summary>
    /// <param name="keys">The keys, in order.</param>
    public ScriptedKeys(params ConsoleKeyInfo[] keys) => _keys = new Queue<ConsoleKeyInfo>(keys);

    /// <summary>Gets how many keys are still to be read.</summary>
    public int Remaining => _keys.Count;

    /// <summary>Returns the key information for a special key.</summary>
    /// <param name="key">The key.</param>
    /// <example><code>var enter = ScriptedKeys.Key(ConsoleKey.Enter);</code></example>
    public static ConsoleKeyInfo Key(ConsoleKey key)
    {
        var character = key switch
        {
            ConsoleKey.Enter => '\r',
            ConsoleKey.Spacebar => ' ',
            ConsoleKey.Escape => '\u001b',
            _ => '\0'
        };

        return new ConsoleKeyInfo(character, key, shift: false, alt: false, control: false);
    }

    /// <summary>Returns the key information for a typed character.</summary>
    /// <param name="character">The character, e.g. <c>'2'</c> or <c>'y'</c>.</param>
    /// <example><code>var two = ScriptedKeys.Char('2');</code></example>
    public static ConsoleKeyInfo Char(char character)
    {
        var key = char.IsDigit(character)
            ? ConsoleKey.D0 + (character - '0')
            : char.IsLetter(character) ? ConsoleKey.A + (char.ToUpperInvariant(character) - 'A') : ConsoleKey.NoName;

        return new ConsoleKeyInfo(character, key, shift: false, alt: false, control: false);
    }

    /// <inheritdoc />
    public bool CanReadKeys => true;

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey() => _keys.Count > 0 ? _keys.Dequeue() : null;

    /// <inheritdoc />
    public string? ReadLine() => throw new InvalidOperationException("Keys only: the element should not read a line.");

    /// <inheritdoc />
    public string? ReadMasked(char mask) => ReadLine();
}

/// <summary>
/// A render target that keeps the screen as plain text and can erase its last lines, like an ANSI terminal.
/// </summary>
/// <example><code>var renderer = new MarkupRenderer(new RewritableScreen(), () => 60);</code></example>
public sealed class RewritableScreen : IRenderTarget
{
    private readonly List<string> _lines = [""];

    /// <summary>Gets how many times lines were erased.</summary>
    public int Erases { get; private set; }

    /// <summary>Gets the screen, one line per terminal row.</summary>
    public string Screen => string.Join('\n', _lines);

    /// <inheritdoc />
    public void Write(RenderedLine line)
    {
        _lines[^1] += line.ToPlainText();
        _lines.Add("");
    }

    /// <inheritdoc />
    public void WriteInline(RenderedLine line) => _lines[^1] += line.ToPlainText();

    /// <inheritdoc />
    public void Clear()
    {
        _lines.Clear();
        _lines.Add("");
    }

    /// <inheritdoc />
    public void Flush()
    {
    }

    /// <inheritdoc />
    public bool TryEraseLines(int count)
    {
        Erases++;
        // The cursor is at the start of the empty line after the last one written
        _lines.RemoveRange(_lines.Count - 1 - count, count);
        return true;
    }
}
