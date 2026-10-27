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
    public void Write(RenderedLine line)
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

        // One write per line keeps output fast and avoids interleaving with other writers
        _writer.WriteLine(_buffer.ToString());
    }

    /// <inheritdoc />
    /// <remarks>Moves the cursor home, clears the screen and the scroll-back buffer.</remarks>
    public void Clear() => _writer.Write("\u001b[H\u001b[2J\u001b[3J");

    /// <inheritdoc />
    public void Flush() => _writer.Flush();

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
