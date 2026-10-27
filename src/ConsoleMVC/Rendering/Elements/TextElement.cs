using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// A block of styled text — the <c>&lt;text&gt;</c> markup element. Text is word-wrapped (or truncated)
/// to the available width and aligned left, centre or right.
/// </summary>
/// <remarks>
/// All content is sanitised on the way in (see <see cref="TextSanitiser"/>), so values that come from
/// models or users cannot inject terminal escape sequences.
/// </remarks>
/// <example>
/// <code>
/// var text = new TextElement { Foreground = Colour.Cyan, Bold = true, Align = Alignment.Centre };
/// text.AppendText("Welcome, ");
/// text.AppendValue(model.UserName);
/// text.AppendText("!");
/// </code>
/// </example>
public sealed class TextElement : Element
{
    private readonly StringBuilder _content = new();

    /// <summary>Gets or sets the text colour. Defaults to the terminal colour.</summary>
    public Colour Foreground { get; set; }

    /// <summary>Gets or sets the background colour. Defaults to the terminal colour.</summary>
    public Colour Background { get; set; }

    /// <summary>Gets or sets whether the text is bold.</summary>
    public bool Bold { get; set; }

    /// <summary>Gets or sets whether the text is dimmed.</summary>
    public bool Dim { get; set; }

    /// <summary>Gets or sets whether the text is italic (terminal support varies).</summary>
    public bool Italic { get; set; }

    /// <summary>Gets or sets whether the text is underlined.</summary>
    public bool Underline { get; set; }

    /// <summary>Gets or sets the horizontal alignment. Defaults to <see cref="Alignment.Left"/>.</summary>
    public Alignment Align { get; set; } = Alignment.Left;

    /// <summary>
    /// Gets or sets whether long lines are word-wrapped (<see langword="true"/>, the default) or
    /// truncated with an ellipsis (<see langword="false"/>).
    /// </summary>
    public bool Wrap { get; set; } = true;

    /// <summary>Gets the sanitised text content, with <c>\n</c> marking line breaks.</summary>
    public string Content => _content.ToString();

    /// <summary>Gets the combined style built from the colour and decoration properties.</summary>
    public TextStyle Style
    {
        get
        {
            var decoration = TextDecoration.None;
            if (Bold) decoration |= TextDecoration.Bold;
            if (Dim) decoration |= TextDecoration.Dim;
            if (Italic) decoration |= TextDecoration.Italic;
            if (Underline) decoration |= TextDecoration.Underline;

            return new TextStyle(Foreground, Background, decoration);
        }
    }

    /// <summary>
    /// Appends literal text. Used by generated code for text written in the <c>.cvw</c> markup.
    /// </summary>
    /// <param name="text">The text to append; sanitised before storing.</param>
    public void AppendText(string? text) => _content.Append(TextSanitiser.Sanitise(text));

    /// <summary>
    /// Appends the formatted value of an expression, such as <c>@Model.Name</c>.
    /// </summary>
    /// <param name="value">The value to append. <see langword="null"/> appends nothing.</param>
    /// <seealso cref="MarkupValue.Format"/>
    public void AppendValue(object? value) => _content.Append(TextSanitiser.Sanitise(MarkupValue.Format(value)));

    /// <summary>
    /// Appends a hard line break (the <c>&lt;br /&gt;</c> element inside text).
    /// </summary>
    public void AppendLineBreak() => _content.Append('\n');

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        // Trailing spaces are dropped by wrapping and truncation, so they must not count here either
        var widest = Content.Split('\n').Max(paragraph => TextWidth.Of(paragraph.TrimEnd(' ')));
        return Math.Min(widest, Math.Max(maxWidth, 0));
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var rows = Wrap ? TextWrapper.Wrap(Content, width) : TextWrapper.Truncate(Content, width);
        var style = Style;
        var lines = new RenderedLine[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            var rowWidth = TextWidth.Of(rows[i]);
            var indent = Align switch
            {
                Alignment.Centre => (width - rowWidth) / 2,
                Alignment.Right => width - rowWidth,
                _ => 0
            };

            // Clip: a double-width character cannot be shown in a single column
            lines[i] = new RenderedLine([Span.Spaces(indent), new Span(rows[i], style)]).Clip(Math.Max(width, 0));
        }

        return lines;
    }
}
