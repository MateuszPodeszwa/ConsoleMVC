namespace ConsoleMVC.Rendering;

/// <summary>
/// A cell of a table — the <c>&lt;cell&gt;</c> markup element. Behaves like <see cref="TextElement"/>.
/// </summary>
/// <example>
/// <code>
/// var cell = new TableCellElement { Align = Alignment.Right };
/// cell.AppendValue(user.Age);
/// </code>
/// </example>
public sealed class TableCellElement : InlineElement
{
}

/// <summary>
/// A row of a table — the <c>&lt;row&gt;</c> markup element.
/// </summary>
/// <example>
/// <code>
/// var header = new TableRowElement { IsHeader = true };
/// header.Add(nameCell);
/// header.Add(ageCell);
/// </code>
/// </example>
public sealed class TableRowElement
{
    private readonly List<TableCellElement> _cells = [];

    /// <summary>
    /// Gets or sets whether this is a header row. Header cells are bold and are followed by a separator line.
    /// </summary>
    public bool IsHeader { get; set; }

    /// <summary>Gets the cells in column order.</summary>
    public IReadOnlyList<TableCellElement> Cells => _cells;

    /// <summary>
    /// Appends a cell.
    /// </summary>
    /// <param name="cell">The cell to append.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cell"/> is <see langword="null"/>.</exception>
    public void Add(TableCellElement cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        _cells.Add(cell);
    }
}

/// <summary>
/// A grid of cells — the <c>&lt;table&gt;</c> markup element. Columns are sized to their content; when the
/// table is wider than the space available, the widest columns shrink and their cells wrap.
/// </summary>
/// <remarks>The column sizing algorithm is described in <c>docs/design/c-view-markup-0.2.md</c> §4.1.</remarks>
/// <example>
/// <code>
/// var table = new TableElement { Border = BorderStyle.Rounded };
/// table.Add(headerRow);
/// foreach (var user in users)
///     table.Add(CreateRow(user));
/// // ╭───────┬─────╮
/// // │ Name  │ Age │
/// // ├───────┼─────┤
/// // │ Ada   │ 36  │
/// // ╰───────┴─────╯
/// </code>
/// </example>
public sealed class TableElement : Element
{
    private readonly List<TableRowElement> _rows = [];

    /// <summary>Gets or sets the grid characters. Defaults to <see cref="BorderStyle.Single"/>.</summary>
    public BorderStyle Border { get; set; } = BorderStyle.Single;

    /// <summary>Gets or sets the colour of the grid lines.</summary>
    public Colour BorderColour { get; set; }

    /// <summary>Gets or sets the blank columns on each side of every cell. Defaults to 1.</summary>
    public int Padding { get; set; } = 1;

    /// <summary>
    /// Gets or sets the width of the whole table. <see cref="Length.Auto"/> (the default) fits the content;
    /// a larger fixed or percentage width widens the last column.
    /// </summary>
    public Length Width { get; set; } = Length.Auto;

    /// <summary>Gets the rows in order.</summary>
    public IReadOnlyList<TableRowElement> Rows => _rows;

    private int ColumnCount => _rows.Count == 0 ? 0 : _rows.Max(r => r.Cells.Count);

    private int SafePadding => Math.Max(Padding, 0);

    /// <summary>
    /// Appends a row.
    /// </summary>
    /// <param name="row">The row to append.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="row"/> is <see langword="null"/>.</exception>
    public void Add(TableRowElement row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _rows.Add(row);
    }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth)
    {
        var columns = ColumnCount;
        if (columns == 0)
            return 0;

        return Width.Resolve(maxWidth, NaturalWidths(columns).Sum() + Chrome(columns));
    }

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var columns = ColumnCount;
        if (columns == 0)
            return [];

        var widths = ColumnWidths(columns, MeasureWidth(width) - Chrome(columns));
        var borderStyle = TextStyle.Plain.WithForeground(BorderColour);
        var lines = new List<RenderedLine>();

        if (Border.IsVisible)
            lines.Add(HorizontalRule(widths, Border.TopLeft, Border.TeeDown, Border.TopRight, borderStyle));

        for (var r = 0; r < _rows.Count; r++)
        {
            lines.AddRange(LayoutRow(_rows[r], widths, borderStyle));

            if (_rows[r].IsHeader && r < _rows.Count - 1 && Border.IsVisible)
                lines.Add(HorizontalRule(widths, Border.TeeRight, Border.Cross, Border.TeeLeft, borderStyle));
        }

        if (Border.IsVisible)
            lines.Add(HorizontalRule(widths, Border.BottomLeft, Border.TeeUp, Border.BottomRight, borderStyle));

        return lines;
    }

    /// <summary>Columns used by vertical grid lines and cell padding.</summary>
    private int Chrome(int columns) => (Border.IsVisible ? columns + 1 : 0) + 2 * SafePadding * columns;

    private int[] NaturalWidths(int columns)
    {
        var widths = new int[columns];
        foreach (var row in _rows)
        {
            for (var c = 0; c < row.Cells.Count; c++)
                widths[c] = Math.Max(widths[c], row.Cells[c].MeasureWidth(int.MaxValue));
        }

        return widths;
    }

    /// <summary>
    /// Shrinks the widest columns until the total fits <paramref name="target"/>, or widens the last column
    /// when the target is larger than the content.
    /// </summary>
    private int[] ColumnWidths(int columns, int target)
    {
        var widths = NaturalWidths(columns).Select(w => Math.Max(w, 1)).ToArray();
        target = Math.Max(target, columns);

        while (widths.Sum() > target)
        {
            var widest = Array.IndexOf(widths, widths.Max());
            if (widths[widest] <= 1)
                break;

            widths[widest]--;
        }

        var spare = target - widths.Sum();
        if (spare > 0)
            widths[^1] += spare;

        return widths;
    }

    private IEnumerable<RenderedLine> LayoutRow(TableRowElement row, int[] widths, TextStyle borderStyle)
    {
        var decoration = row.IsHeader ? TextDecoration.Bold : TextDecoration.None;
        var cellLines = new IReadOnlyList<RenderedLine>[widths.Length];

        for (var c = 0; c < widths.Length; c++)
        {
            cellLines[c] = c < row.Cells.Count ? row.Cells[c].Layout(widths[c], decoration) : [RenderedLine.Empty];
        }

        var height = cellLines.Max(l => l.Count);
        var padding = Span.Spaces(SafePadding);
        var vertical = new Span(Border.Vertical.ToString(), borderStyle);

        for (var i = 0; i < height; i++)
        {
            var spans = new List<Span>();
            if (Border.IsVisible)
                spans.Add(vertical);

            for (var c = 0; c < widths.Length; c++)
            {
                var line = i < cellLines[c].Count ? cellLines[c][i] : RenderedLine.Empty;
                spans.Add(padding);
                spans.AddRange(line.PadRight(widths[c]).Spans);
                spans.Add(padding);

                if (Border.IsVisible)
                    spans.Add(vertical);
            }

            yield return new RenderedLine(spans);
        }
    }

    private RenderedLine HorizontalRule(int[] widths, char left, char junction, char right, TextStyle style)
    {
        var segments = widths.Select(w => new string(Border.Horizontal, w + 2 * SafePadding));
        return new RenderedLine([new Span(left + string.Join(junction, segments) + right, style)]);
    }
}
