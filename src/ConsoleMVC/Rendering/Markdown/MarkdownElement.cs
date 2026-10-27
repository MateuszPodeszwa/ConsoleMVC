namespace ConsoleMVC.Rendering;

/// <summary>
/// Markdown drawn for the terminal: the <c>&lt;markdown text="…" /&gt;</c> markup element. Supports headings, paragraphs,
/// bulleted and numbered lists (nested), block quotes, fenced code, rules, <c>**bold**</c>, <c>*italic*</c>,
/// <c>`code`</c> and links. Text wraps to the width; code blocks keep their lines and are clipped instead.
/// </summary>
/// <remarks>
/// Everything is sanitised before it is drawn, so Markdown from users or files cannot send terminal escape sequences.
/// Links are shown as their text followed by the address; nothing is opened or fetched. HTML, tables and images are not
/// interpreted: they are shown as text.
/// </remarks>
/// <example>
/// <code>
/// renderer.Render(new MarkdownElement { Text = "# Help\n\nPress **F1** at any time.\n\n- Esc goes back\n- Ctrl+X quits" });
/// </code>
/// </example>
public sealed class MarkdownElement : Element
{
    /// <summary>The colour of code unless <see cref="CodeColour"/> is set: yellow.</summary>
    public static readonly Colour DefaultCodeColour = Colour.Yellow;

    /// <summary>The colour of block quotes unless <see cref="QuoteColour"/> is set: dark grey.</summary>
    public static readonly Colour DefaultQuoteColour = Colour.DarkGrey;

    /// <summary>Gets or sets the Markdown to draw.</summary>
    /// <example><code>element.Text = File.ReadAllText("CHANGELOG.md");</code></example>
    public string? Text { get; set; }

    /// <summary>Gets or sets the colour of headings.</summary>
    public Colour HeadingColour { get; set; }

    /// <summary>Gets or sets the colour of code spans and code blocks. Defaults to <see cref="DefaultCodeColour"/>.</summary>
    public Colour CodeColour { get; set; } = DefaultCodeColour;

    /// <summary>Gets or sets the colour of links (which are also underlined).</summary>
    public Colour LinkColour { get; set; }

    /// <summary>Gets or sets the colour of block quotes. Defaults to <see cref="DefaultQuoteColour"/>.</summary>
    public Colour QuoteColour { get; set; } = DefaultQuoteColour;

    /// <summary>Gets or sets whether a link's address is shown after its text. Defaults to <see langword="true"/>.</summary>
    public bool ShowLinkUrls { get; set; } = true;

    /// <inheritdoc />
    /// <remarks>Markdown fills the width it is given, like a paragraph of <c>&lt;text&gt;</c>.</remarks>
    public override int MeasureWidth(int maxWidth) => 0;

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        width = Math.Max(width, 1);
        var lines = new List<RenderedLine>();
        MarkdownBlock? previous = null;

        foreach (var block in MarkdownParser.Parse(Text ?? ""))
        {
            // One blank line between blocks; none after a heading or between the items of one list
            var continues = block.Kind == MarkdownBlockKind.ListItem && block.ContinuesList;
            if (previous is not null && previous.Kind != MarkdownBlockKind.Heading && !continues)
                lines.Add(RenderedLine.Empty);

            switch (block.Kind)
            {
                case MarkdownBlockKind.Heading:
                    var heading = TextStyle.Plain.WithForeground(HeadingColour).WithDecoration(
                        block.Level == 1 ? TextDecoration.Bold | TextDecoration.Underline : TextDecoration.Bold);
                    lines.AddRange(Inline(block.Text, heading, width));
                    break;

                case MarkdownBlockKind.Paragraph:
                    lines.AddRange(Inline(block.Text, TextStyle.Plain, width));
                    break;

                case MarkdownBlockKind.ListItem:
                    lines.AddRange(ListItem(block, width));
                    break;

                case MarkdownBlockKind.Quote:
                    var quote = TextStyle.Plain.WithForeground(QuoteColour);
                    foreach (var line in Inline(block.Text, quote, Math.Max(width - 2, 1)))
                        lines.Add(line.Surround([new Span("│ ", quote)], []));
                    break;

                case MarkdownBlockKind.Code:
                    var code = TextStyle.Plain.WithForeground(CodeColour);
                    foreach (var line in block.Text.Split('\n'))
                        lines.Add(new RenderedLine([new Span("  " + TextSanitiser.Sanitise(line).Replace('\n', ' '), code)]));
                    break;

                case MarkdownBlockKind.Rule:
                    lines.Add(new RenderedLine([new Span(new string('─', width), TextStyle.Plain.WithForeground(QuoteColour))]));
                    break;
            }

            previous = block;
        }

        return lines.Select(l => l.Clip(width)).ToArray();
    }

    private IReadOnlyList<RenderedLine> Inline(string text, TextStyle style, int width)
    {
        var styled = new StyledText();
        MarkdownInline.Append(text, style, this, styled);
        return styled.IsEmpty ? [RenderedLine.Empty] : styled.Wrap(width);
    }

    private IEnumerable<RenderedLine> ListItem(MarkdownBlock block, int width)
    {
        var indent = new string(' ', block.Level * 2);
        var marker = block.Marker + " ";
        var hanging = new string(' ', indent.Length + TextWidth.Of(marker));
        var body = Inline(block.Text, TextStyle.Plain, Math.Max(width - hanging.Length, 1));

        for (var i = 0; i < body.Count; i++)
            yield return body[i].Surround([new Span(i == 0 ? indent + marker : hanging, TextStyle.Plain)], []);
    }
}
