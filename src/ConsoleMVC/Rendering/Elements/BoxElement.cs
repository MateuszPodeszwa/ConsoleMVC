namespace ConsoleMVC.Rendering;

/// <summary>
/// A bordered panel with an optional title — the <c>&lt;box&gt;</c> markup element. Children are
/// stacked vertically inside the frame.
/// </summary>
/// <remarks>
/// With the default <see cref="Length.Auto"/> width the box shrinks to fit its content and title;
/// a fixed or percentage width is honoured but never exceeds the available width. Content that does
/// not fit is wrapped by the child elements.
/// </remarks>
/// <example>
/// <code>
/// var box = new BoxElement { Title = "Dashboard", Border = BorderStyle.Double, Width = 60 };
/// box.Add(welcomeText);
/// box.Add(new SeparatorElement());
/// // ╔═ Dashboard ══════╗
/// // ║ Welcome, Ada!    ║
/// // ║ ──────────────── ║
/// // ╚══════════════════╝
/// </code>
/// </example>
public sealed class BoxElement : ContainerElement
{
    /// <summary>Columns taken by the title decoration "─ " + " " on the left of the title.</summary>
    private const int TitleLeadIn = 3;

    /// <summary>Gets or sets the title drawn in the top border. <see langword="null"/> or empty for none.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the width of the whole box, including its border. Defaults to <see cref="Length.Auto"/>.</summary>
    public Length Width { get; set; } = Length.Auto;

    /// <summary>
    /// Gets or sets the smallest width in columns, or <see langword="null"/> for none. Never exceeds the available width.
    /// </summary>
    /// <example><code>element.MinWidth = 30; // at least 30 columns when the terminal allows</code></example>
    public int? MinWidth { get; set; }

    /// <summary>Gets or sets the largest width in columns, or <see langword="null"/> for none.</summary>
    /// <example><code>element.MaxWidth = 80; // a full-width element stops growing at 80 columns</code></example>
    public int? MaxWidth { get; set; }

    /// <summary>Gets or sets the border characters. Defaults to <see cref="BorderStyle.Single"/>.</summary>
    public BorderStyle Border { get; set; } = BorderStyle.Single;

    /// <summary>Gets or sets the number of blank columns between the border and the content on each side. Defaults to 1.</summary>
    public int Padding { get; set; } = 1;

    /// <summary>Gets or sets the colour of the border.</summary>
    public Colour BorderColour { get; set; }

    /// <summary>Gets or sets the colour of the title. Defaults to the border colour.</summary>
    public Colour TitleColour { get; set; }

    /// <summary>Gets the total columns used by the border and padding on both sides.</summary>
    private int Chrome => (Border.IsVisible ? 2 : 0) + 2 * Math.Max(Padding, 0);

    private string SafeTitle => TextSanitiser.Sanitise(Title).Replace('\n', ' ');

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        var natural = MeasureChildren(Math.Max(maxWidth - Chrome, 0)) + Chrome;

        if (SafeTitle.Length > 0)
        {
            // Corners + lead-in + title + trailing space + at least one horizontal character
            var titleNeed = Border.IsVisible
                ? TextWidth.Of(SafeTitle) + TitleLeadIn + 3
                : TextWidth.Of(SafeTitle) + Chrome;
            natural = Math.Max(natural, titleNeed);
        }

        return WidthLimits.Apply(Width.Resolve(maxWidth, natural), MinWidth, MaxWidth, maxWidth);
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var borderColumns = Border.IsVisible ? 2 : 0;
        var available = Math.Max(width, 0);
        var boxWidth = Math.Max(MeasureWidth(width), Math.Min(borderColumns + 1, available));

        // Keep the requested padding unless it leaves no room for content; then reduce it only as far as
        // needed for the content to fit (or to one column, if even that is not possible)
        var effectivePadding = Math.Max(Padding, 0);
        if (boxWidth - borderColumns - 2 * effectivePadding < 1)
        {
            var contentWidth = Math.Max(MeasureChildren(Math.Max(boxWidth - borderColumns, 0)), 1);
            effectivePadding = Math.Clamp((boxWidth - borderColumns - contentWidth) / 2, 0, effectivePadding);
        }
        var innerWidth = Math.Max(boxWidth - borderColumns - 2 * effectivePadding, 1);
        var padding = Span.Spaces(effectivePadding);
        var borderStyle = TextStyle.Plain.WithForeground(BorderColour);
        var lines = new List<RenderedLine>();

        if (Border.IsVisible)
            lines.Add(TopBorder(boxWidth, borderStyle));
        else if (SafeTitle.Length > 0)
            lines.Add(new RenderedLine([padding, new Span(TextWidth.Truncate(SafeTitle, innerWidth), TitleStyle(borderStyle))]));

        var left = Border.IsVisible ? new[] { new Span(Border.Vertical.ToString(), borderStyle), padding } : [padding];
        var right = Border.IsVisible ? new[] { padding, new Span(Border.Vertical.ToString(), borderStyle) } : [padding];

        foreach (var line in LayoutChildren(innerWidth))
            lines.Add(line.PadRight(innerWidth).Surround(left, right));

        if (Border.IsVisible)
        {
            lines.Add(new RenderedLine(
            [
                new Span(Border.BottomLeft + new string(Border.Horizontal, Math.Max(boxWidth - 2, 0)) + Border.BottomRight, borderStyle)
            ]));
        }

        // Safety net for widths smaller than the border itself: never draw past the width given
        return lines.Select(line => line.Clip(available)).ToList();
    }

    /// <inheritdoc />
    protected internal override bool HasRelativeWidth => Width.IsRelative;

    /// <summary>Builds the top border, embedding the (possibly truncated) title.</summary>
    private RenderedLine TopBorder(int boxWidth, TextStyle borderStyle)
    {
        var horizontalCount = Math.Max(boxWidth - 2, 0);

        if (SafeTitle.Length == 0 || horizontalCount < TitleLeadIn + 2)
        {
            return new RenderedLine(
            [
                new Span(Border.TopLeft + new string(Border.Horizontal, horizontalCount) + Border.TopRight, borderStyle)
            ]);
        }

        // ┌─ Title ──┐ : corner, one horizontal, space, title, space, fill, corner
        var title = TextWidth.Truncate(SafeTitle, horizontalCount - TitleLeadIn);
        var fill = horizontalCount - TitleLeadIn - TextWidth.Of(title);

        return new RenderedLine(
        [
            new Span($"{Border.TopLeft}{Border.Horizontal} ", borderStyle),
            new Span(title, TitleStyle(borderStyle)),
            new Span(" " + new string(Border.Horizontal, Math.Max(fill, 0)) + Border.TopRight, borderStyle)
        ]);
    }

    private TextStyle TitleStyle(TextStyle borderStyle) =>
        TitleColour.IsDefault ? borderStyle.WithDecoration(TextDecoration.Bold) : borderStyle.WithForeground(TitleColour).WithDecoration(TextDecoration.Bold);
}
