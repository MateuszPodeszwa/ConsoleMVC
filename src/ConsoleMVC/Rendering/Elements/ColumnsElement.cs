namespace ConsoleMVC.Rendering;

/// <summary>
/// Lays out <see cref="ColumnElement"/>s side by side: the <c>&lt;columns&gt;</c> markup element.
/// </summary>
/// <remarks>
/// <para>Column widths are resolved in three steps (see <c>docs/design/c-view-markup-1.0.md</c> §3):</para>
/// <list type="number">
///   <item><description>Fixed columns take their width and percentage columns their share of the width left after the gaps.</description></item>
///   <item><description>Auto columns take their natural width when everything fits; otherwise the remaining width is shared
///   out so that narrow columns keep their natural width and wide ones split the rest equally.</description></item>
///   <item><description>When the available width is below <see cref="StackBelow"/>, the columns are stacked vertically
///   instead, so a layout stays readable in a narrow terminal.</description></item>
/// </list>
/// <para>Every line is clipped to the width given, so columns never draw past their container.</para>
/// </remarks>
/// <example>
/// <code>
/// var columns = new ColumnsElement { Gap = 2, StackBelow = 60 };
/// columns.Add(new ColumnElement { Width = Length.Fixed(20) });   // sidebar
/// columns.Add(new ColumnElement());                              // main content
/// </code>
/// </example>
public sealed class ColumnsElement : Element
{
    private readonly List<ColumnElement> _columns = [];

    /// <summary>Gets or sets the number of spaces between columns. Defaults to 2.</summary>
    public int Gap { get; set; } = 2;

    /// <summary>
    /// Gets or sets the width (in terminal columns) below which the columns are stacked vertically.
    /// <see langword="null"/> (the default) never stacks.
    /// </summary>
    /// <example><code>columns.StackBelow = 60; // side by side from 60 columns, stacked below</code></example>
    public int? StackBelow { get; set; }

    /// <summary>Gets the columns in order.</summary>
    public IReadOnlyList<ColumnElement> Columns => _columns;

    /// <summary>
    /// Adds a column.
    /// </summary>
    /// <param name="child">The column to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="child"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="child"/> is not a <see cref="ColumnElement"/>.</exception>
    /// <example><code>columns.Add(new ColumnElement { Width = "30%" });</code></example>
    public void Add(Element child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child is not ColumnElement column)
            throw new ArgumentException("<columns> may only contain <column> elements.", nameof(child));

        _columns.Add(column);
    }

    private int SafeGap => Math.Max(Gap, 0);

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        var available = Math.Max(maxWidth, 0);
        if (_columns.Count == 0)
            return 0;

        if (IsStacked(available))
            return Math.Min(_columns.Max(c => c.MeasureWidth(available)), available);

        var widths = ResolveWidths(available);
        return Math.Min(widths.Sum() + SafeGap * (_columns.Count - 1), available);
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var available = Math.Max(width, 0);
        if (_columns.Count == 0)
            return [];

        if (IsStacked(available))
            return _columns.SelectMany(c => c.Layout(available)).Select(l => l.Clip(available)).ToList();

        var widths = ResolveWidths(available);
        var blocks = _columns.Select((column, i) => widths[i] > 0 ? column.Layout(widths[i]) : []).ToArray();
        var height = blocks.Max(b => b.Count);
        var gap = Span.Spaces(SafeGap);
        var last = Array.FindLastIndex(widths, w => w > 0);
        var lines = new List<RenderedLine>(height);

        for (var row = 0; row < height; row++)
        {
            var spans = new List<Span>();
            for (var i = 0; i < _columns.Count; i++)
            {
                if (widths[i] == 0)
                    continue;

                if (spans.Count > 0)
                    spans.Add(gap);

                var line = row < blocks[i].Count ? blocks[i][row] : RenderedLine.Empty;
                // The last column is not padded, so lines carry no trailing spaces
                var clipped = line.Clip(widths[i]);
                spans.AddRange((i == last ? clipped : clipped.PadRight(widths[i])).Spans);
            }

            lines.Add(new RenderedLine(spans).Clip(available));
        }

        return lines;
    }

    /// <inheritdoc />
    protected internal override bool HasRelativeWidth => _columns.Any(c => c.Width.Kind == LengthKind.Percent);

    private bool IsStacked(int available) => StackBelow is { } threshold && available < threshold;

    /// <summary>Resolves the width of every column for the given available width.</summary>
    private int[] ResolveWidths(int available)
    {
        var count = _columns.Count;
        var content = Math.Max(available - SafeGap * (count - 1), 0);
        var widths = new int[count];
        var used = 0;

        for (var i = 0; i < count; i++)
        {
            var length = _columns[i].Width;
            if (length.Kind == LengthKind.Auto)
                continue;

            widths[i] = Math.Min(length.Resolve(content, 0), Math.Max(content - used, 0));
            used += widths[i];
        }

        var remaining = Math.Max(content - used, 0);
        var auto = Enumerable.Range(0, count).Where(i => _columns[i].Width.Kind == LengthKind.Auto).ToList();
        if (auto.Count == 0)
            return widths;

        var natural = auto.ToDictionary(i => i, i => _columns[i].MeasureWidth(remaining));
        if (natural.Values.Sum() <= remaining)
        {
            foreach (var i in auto)
                widths[i] = natural[i];
            return widths;
        }

        // Water filling: columns narrower than an equal share keep their natural width; the others share the rest
        var pending = auto.OrderBy(i => natural[i]).ToList();
        while (pending.Count > 0)
        {
            var share = remaining / pending.Count;
            var first = pending[0];
            if (natural[first] <= share)
            {
                widths[first] = natural[first];
                remaining -= natural[first];
                pending.RemoveAt(0);
                continue;
            }

            var extra = remaining % pending.Count;
            foreach (var i in pending.OrderBy(i => i))
                widths[i] = share + (extra-- > 0 ? 1 : 0);
            break;
        }

        return widths;
    }
}

/// <summary>
/// One column of a <see cref="ColumnsElement"/>: the <c>&lt;column&gt;</c> markup element. It holds block content, like
/// a box without a border.
/// </summary>
/// <example>
/// <code>
/// var column = new ColumnElement { Width = Length.Percent(30) };
/// column.Add(menuText);
/// </code>
/// </example>
public sealed class ColumnElement : ContainerElement
{
    /// <summary>Gets or sets the column's width: <c>auto</c> (the default), a column count, or a percentage.</summary>
    public Length Width { get; set; } = Length.Auto;

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) => Width.Resolve(maxWidth, MeasureChildren(maxWidth));

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width) =>
        LayoutChildren(Math.Max(width, 0)).Select(l => l.Clip(Math.Max(width, 0))).ToList();
}
