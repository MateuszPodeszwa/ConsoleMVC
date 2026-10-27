using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// One fully laid-out terminal line: an immutable sequence of styled <see cref="Span"/>s.
/// Elements produce these from <see cref="Element.Layout"/>, and render targets write them.
/// </summary>
/// <example>
/// <code>
/// var line = new RenderedLine([new Span("│", TextStyle.Plain), new Span(" Hello", TextStyle.Plain)]);
/// Console.WriteLine(line.ToPlainText()); // "│ Hello"
/// </code>
/// </example>
public sealed class RenderedLine
{
    /// <summary>Gets an empty line.</summary>
    public static RenderedLine Empty { get; } = new([]);

    /// <summary>
    /// Creates a line from the given spans. Empty spans are discarded.
    /// </summary>
    /// <param name="spans">The styled runs making up the line, left to right.</param>
    public RenderedLine(IEnumerable<Span> spans)
    {
        Spans = spans.Where(s => s.Text.Length > 0).ToArray();
        Width = Spans.Sum(s => s.Width);
    }

    /// <summary>Gets the styled runs making up the line, left to right.</summary>
    public IReadOnlyList<Span> Spans { get; }

    /// <summary>Gets the display width of the line in terminal columns.</summary>
    public int Width { get; }

    /// <summary>
    /// Returns a new line with <paramref name="before"/> prepended and <paramref name="after"/> appended.
    /// </summary>
    /// <param name="before">Spans placed before this line's content.</param>
    /// <param name="after">Spans placed after this line's content.</param>
    /// <returns>The combined line.</returns>
    public RenderedLine Surround(IEnumerable<Span> before, IEnumerable<Span> after) =>
        new(before.Concat(Spans).Concat(after));

    /// <summary>
    /// Returns a new line padded with unstyled spaces up to <paramref name="width"/> columns.
    /// Lines that are already at least that wide are returned unchanged.
    /// </summary>
    /// <param name="width">The target width.</param>
    /// <returns>The padded line.</returns>
    public RenderedLine PadRight(int width) =>
        Width >= width ? this : new RenderedLine(Spans.Append(Span.Spaces(width - Width)));

    /// <summary>
    /// Returns the text of the line without any styling information.
    /// </summary>
    /// <returns>The concatenated text of every span.</returns>
    public string ToPlainText()
    {
        var builder = new StringBuilder();
        foreach (var span in Spans)
            builder.Append(span.Text);

        return builder.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => ToPlainText();
}
