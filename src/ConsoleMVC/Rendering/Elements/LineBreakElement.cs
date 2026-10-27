namespace ConsoleMVC.Rendering;

/// <summary>
/// One or more empty lines — the <c>&lt;br /&gt;</c> markup element when used between blocks.
/// </summary>
/// <example><code>var gap = new LineBreakElement { Count = 2 };</code></example>
public sealed class LineBreakElement : Element
{
    /// <summary>Gets or sets the number of empty lines. Defaults to 1; negative values render nothing.</summary>
    public int Count { get; set; } = 1;

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) => 0;

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width) =>
        Enumerable.Repeat(RenderedLine.Empty, Math.Max(Count, 0)).ToArray();
}
