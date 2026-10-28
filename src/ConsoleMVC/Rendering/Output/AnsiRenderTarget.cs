using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Writes lines using ANSI/VT100 escape sequences (SGR) for colour and decoration. This is the
/// default target on Linux, macOS and Windows 10+ terminals.
/// </summary>
/// <remarks>
/// Every styled span is followed by a reset (<c>ESC[0m</c>) so that styles never leak into text written
/// later with <see cref="Console.Write(string)"/>. Plain spans are written without any escape codes.
/// </remarks>
/// <example>
/// <code>
/// var target = new AnsiRenderTarget(Console.Out, ColourSupport.TrueColour);
/// </code>
/// </example>
public sealed class AnsiRenderTarget : IRenderTarget
{
    private const string Escape = "\u001b[";
    private const string Reset = "\u001b[0m";

    private readonly TextWriter _writer;
    private readonly ColourSupport _colourSupport;
    private readonly StringBuilder _buffer = new();

    /// <summary>
    /// Creates an ANSI render target.
    /// </summary>
    /// <param name="writer">The writer to send output to, typically <see cref="Console.Out"/>.</param>
    /// <param name="colourSupport">How many colours the terminal supports.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is <see langword="null"/>.</exception>
    public AnsiRenderTarget(TextWriter writer, ColourSupport colourSupport)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _colourSupport = colourSupport;
    }

    /// <inheritdoc />
    public void Write(RenderedLine line) => _writer.WriteLine(Format(line));

    /// <inheritdoc />
    public void WriteInline(RenderedLine line) => _writer.Write(Format(line));

    /// <summary>Builds the escape-coded text of a line; one write per line keeps output fast and unbroken.</summary>
    private string Format(RenderedLine line)
    {
        _buffer.Clear();

        foreach (var span in line.Spans)
        {
            var codes = BuildCodes(span.Style);

            if (codes.Length == 0)
            {
                _buffer.Append(span.Text);
                continue;
            }

            _buffer.Append(Escape).Append(codes).Append('m').Append(span.Text).Append(Reset);
        }

        return _buffer.ToString();
    }

    /// <inheritdoc />
    /// <remarks>Moves the cursor home, clears the screen and the scroll-back buffer.</remarks>
    public void Clear() => _writer.Write("\u001b[H\u001b[2J\u001b[3J");

    /// <inheritdoc />
    /// <remarks>Moves the cursor up to the first line (<c>ESC[nF</c>) and erases to the end of the screen (<c>ESC[J</c>).</remarks>
    public bool TryEraseLines(int count)
    {
        if (count > 0)
            _writer.Write($"{Escape}{count}F{Escape}J");

        return true;
    }

    /// <inheritdoc />
    public void Flush() => _writer.Flush();

    /// <inheritdoc />
    public bool TrySetMouseReporting(bool on)
    {
        // Button presses (1000) in SGR form (1006), so columns and rows past 223 are reported correctly
        _writer.Write(on ? "\u001b[?1000h\u001b[?1006h" : "\u001b[?1006l\u001b[?1000l");
        _writer.Flush();
        return true;
    }

    /// <inheritdoc />
    /// <remarks>Writes <c>ESC[?1049h</c> (and moves the cursor home) or <c>ESC[?1049l</c>, which terminals that know no
    /// alternate screen ignore.</remarks>
    public bool TrySetAlternateScreen(bool on)
    {
        _writer.Write(on ? "\u001b[?1049h\u001b[H" : "\u001b[?1049l");
        _writer.Flush();
        return true;
    }

    /// <summary>
    /// Builds the semicolon-separated SGR parameters for a style, e.g. <c>"1;91"</c>. Returns an
    /// empty string for styles that need no codes.
    /// </summary>
    private string BuildCodes(TextStyle style)
    {
        var codes = new List<string>(4);

        if (style.Decoration.HasFlag(TextDecoration.Bold)) codes.Add("1");
        if (style.Decoration.HasFlag(TextDecoration.Dim)) codes.Add("2");
        if (style.Decoration.HasFlag(TextDecoration.Italic)) codes.Add("3");
        if (style.Decoration.HasFlag(TextDecoration.Underline)) codes.Add("4");

        AddColour(codes, style.Foreground, background: false);
        AddColour(codes, style.Background, background: true);

        return string.Join(';', codes);
    }

    private void AddColour(List<string> codes, Colour colour, bool background)
    {
        if (colour.IsDefault || _colourSupport == ColourSupport.None)
            return;

        if (colour.Kind == ColourKind.Rgb && _colourSupport == ColourSupport.TrueColour)
        {
            codes.Add($"{(background ? 48 : 38)};2;{colour.R};{colour.G};{colour.B}");
            return;
        }

        var code = ConsolePalette.ForegroundCode(ConsolePalette.ToConsoleColour(colour));
        codes.Add((background ? code + 10 : code).ToString());
    }
}
