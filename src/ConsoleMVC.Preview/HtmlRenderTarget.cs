using System.Globalization;
using System.Net;
using System.Text;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Preview;

/// <summary>
/// Writes rendered lines as HTML for an editor's preview panel: one <c>&lt;span&gt;</c> per run of one style, with the
/// colours of the console palette, inside a <c>&lt;pre&gt;</c> the editor styles. Text is HTML-encoded, so a view cannot
/// inject markup into the panel.
/// </summary>
/// <param name="writer">Where the HTML is written.</param>
/// <example>
/// <code>
/// var html = new StringWriter();
/// var target = new HtmlRenderTarget(html);
/// new MarkupRenderer(target, () => 80).Render(element);
/// target.Finish(); // closes the &lt;pre&gt;
/// </code>
/// </example>
internal sealed class HtmlRenderTarget(TextWriter writer) : IRenderTarget
{
    private bool _started;
    private bool _finished;

    /// <inheritdoc />
    public void Write(RenderedLine line)
    {
        WriteInline(line);
        writer.Write('\n');
    }

    /// <inheritdoc />
    public void WriteInline(RenderedLine line)
    {
        Start();
        foreach (var span in line.Spans)
        {
            if (span.Text.Length == 0)
                continue;

            var style = Css(span.Style);
            var text = WebUtility.HtmlEncode(span.Text);
            if (style.Length == 0)
                writer.Write(text);
            else
                writer.Write($"<span style=\"{style}\">{text}</span>");
        }
    }

    /// <inheritdoc />
    /// <remarks>A preview shows one screen, so clearing does nothing.</remarks>
    public void Clear()
    {
    }

    /// <inheritdoc />
    public void Flush() => writer.Flush();

    /// <summary>Closes the <c>&lt;pre&gt;</c>; an empty screen is still written, so the editor shows something.</summary>
    /// <example><code>target.Finish();</code></example>
    public void Finish()
    {
        if (_finished)
            return;

        Start();
        writer.Write("</pre>\n");
        _finished = true;
    }

    private void Start()
    {
        if (_started)
            return;

        writer.Write("<pre class=\"cvw-screen\">");
        _started = true;
    }

    /// <summary>The inline CSS for a style; empty for plain text.</summary>
    /// <param name="style">The style.</param>
    /// <returns>CSS declarations, such as <c>color:#00ffff;font-weight:bold</c>.</returns>
    /// <example><code>HtmlRenderTarget.Css(TextStyle.Plain.WithForeground(Colour.Cyan)); // "color:#00ffff"</code></example>
    public static string Css(TextStyle style)
    {
        var css = new List<string>();
        if (Hex(style.Foreground) is { } foreground)
            css.Add("color:" + foreground);
        if (Hex(style.Background) is { } background)
            css.Add("background-color:" + background);

        var decoration = style.Decoration;
        if (decoration.HasFlag(TextDecoration.Bold))
            css.Add("font-weight:bold");
        if (decoration.HasFlag(TextDecoration.Dim))
            css.Add("opacity:0.6");
        if (decoration.HasFlag(TextDecoration.Italic))
            css.Add("font-style:italic");
        if (decoration.HasFlag(TextDecoration.Underline))
            css.Add("text-decoration:underline");

        return string.Join(";", css);
    }

    /// <summary>The colour as <c>#rrggbb</c>, or <see langword="null"/> for the editor's default colour.</summary>
    private static string? Hex(Colour colour)
    {
        if (colour.IsDefault)
            return null;

        var (r, g, b) = colour.Kind == ColourKind.Rgb ? (colour.R, colour.G, colour.B) : ConsolePalette.Rgb(colour.ConsoleColour);
        return new StringBuilder("#")
            .Append(r.ToString("x2", CultureInfo.InvariantCulture))
            .Append(g.ToString("x2", CultureInfo.InvariantCulture))
            .Append(b.ToString("x2", CultureInfo.InvariantCulture))
            .ToString();
    }
}
