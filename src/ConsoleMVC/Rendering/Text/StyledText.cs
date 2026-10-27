using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Text made of runs with different styles, word-wrapped like <see cref="TextWrapper"/> while keeping each character's
/// style. Used by elements whose content mixes styles in one paragraph, such as <see cref="MarkdownElement"/>.
/// </summary>
/// <example>
/// <code>
/// var text = new StyledText();
/// text.Append("Use ", TextStyle.Plain);
/// text.Append("bold", TextStyle.Plain.WithDecoration(TextDecoration.Bold));
/// var lines = text.Wrap(20);
/// </code>
/// </example>
internal sealed class StyledText
{
    private readonly StringBuilder _text = new();
    private readonly List<TextStyle> _styles = [];

    /// <summary>Gets whether nothing has been appended.</summary>
    public bool IsEmpty => _text.Length == 0;

    /// <summary>Appends sanitised <paramref name="text"/> in <paramref name="style"/>; <c>\n</c> breaks the line.</summary>
    public void Append(string text, TextStyle style)
    {
        var safe = TextSanitiser.Sanitise(text);
        _text.Append(safe);
        for (var i = 0; i < safe.Length; i++)
            _styles.Add(style);
    }

    /// <summary>Word-wraps the text to <paramref name="width"/> columns.</summary>
    /// <returns>The lines, each at most <paramref name="width"/> columns wide.</returns>
    public IReadOnlyList<RenderedLine> Wrap(int width)
    {
        var text = _text.ToString();
        var wrapped = TextWrapper.Wrap(text, width);
        var lines = new List<RenderedLine>(wrapped.Count);
        var position = 0;

        for (var index = 0; index < wrapped.Count; index++)
        {
            // The wrapper drops the spaces at a break and the line feeds between paragraphs; skip them in the source
            if (index > 0)
            {
                while (position < text.Length && (text[position] == ' ' || text[position] == '\n')
                       && !(wrapped[index].Length > 0 && text[position] == wrapped[index][0]))
                    position++;
            }

            lines.Add(Spans(wrapped[index], ref position));
        }

        return lines;
    }

    /// <summary>Styles <paramref name="line"/>, which matches the source text from <paramref name="position"/> on.</summary>
    private RenderedLine Spans(string line, ref int position)
    {
        var spans = new List<Span>();
        var run = new StringBuilder();
        var runStyle = TextStyle.Plain;

        foreach (var character in line)
        {
            // Indentation the wrapper added has no source character; it takes the style of what follows
            var matches = position < _text.Length && _text[position] == character;
            var style = matches ? _styles[position] : runStyle;
            if (matches)
                position++;

            if (run.Length > 0 && style != runStyle)
            {
                spans.Add(new Span(run.ToString(), runStyle));
                run.Clear();
            }

            runStyle = style;
            run.Append(character);
        }

        if (run.Length > 0)
            spans.Add(new Span(run.ToString(), runStyle));

        return new RenderedLine(spans);
    }
}
