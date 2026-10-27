namespace ConsoleMVC.Rendering;

/// <summary>
/// A horizontal rule — the <c>&lt;separator /&gt;</c> markup element. By default it fills the width
/// of its container (or the terminal, at top level).
/// </summary>
/// <example>
/// <code>
/// var rule = new SeparatorElement { Style = LineStyle.Double, Colour = Colour.DarkGrey };
/// </code>
/// </example>
public sealed class SeparatorElement : Element
{
    /// <summary>Gets or sets the line character. Defaults to <see cref="LineStyle.Single"/>.</summary>
    public LineStyle Style { get; set; } = LineStyle.Single;

    /// <summary>Gets or sets the colour of the line.</summary>
    public Colour Colour { get; set; }

    /// <summary>
    /// Gets or sets the width of the line. <see cref="Length.Auto"/> (the default) fills the available width.
    /// </summary>
    public Length Width { get; set; } = Length.Auto;

    /// <inheritdoc />
    /// <remarks>
    /// Only a fixed width contributes to the parent's size; auto and percentage separators adapt
    /// to their container rather than widening it.
    /// </remarks>
    public override int MeasureWidth(int maxWidth) =>
        Width.Kind == LengthKind.Fixed ? Width.Resolve(maxWidth, 0) : 0;

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var columns = Width.Resolve(width, natural: width);
        var style = TextStyle.Plain.WithForeground(Colour);

        return [new RenderedLine([new Span(new string(Style.Character, columns), style)])];
    }
}
