namespace ConsoleMVC.Rendering;

/// <summary>
/// How the bars of a <see cref="BarChartElement"/> are drawn: the character of a full cell, and whether the end of a
/// bar may be drawn in eighths of a cell for a smoother result.
/// </summary>
/// <example><code>var chart = new BarChartElement { Style = BarChartStyle.Ascii }; // ######</code></example>
public sealed class BarChartStyle
{
    /// <summary>Solid blocks, ending in eighths of a cell (<c>████▌</c>). The default.</summary>
    public static BarChartStyle Block { get; } = new("block", '█', smooth: true);

    /// <summary>Shaded blocks in whole cells: <c>▓▓▓▓</c>.</summary>
    public static BarChartStyle Shade { get; } = new("shade", '▓', smooth: false);

    /// <summary>Plain ASCII in whole cells, for terminals without Unicode: <c>####</c>.</summary>
    public static BarChartStyle Ascii { get; } = new("ascii", '#', smooth: false);

    /// <summary>The partial blocks for one to seven eighths of a cell.</summary>
    internal const string Eighths = " ▏▎▍▌▋▊▉";

    /// <summary>
    /// Creates a custom bar style.
    /// </summary>
    /// <param name="name">A descriptive name, used by <see cref="ToString"/>.</param>
    /// <param name="full">The character of a full cell. Must occupy a single terminal cell.</param>
    /// <param name="smooth">Whether a bar may end in eighths of a cell (the block characters <c>▏</c> to <c>▉</c>).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="full"/> does not occupy exactly one terminal cell.</exception>
    /// <example><code>var dots = new BarChartStyle("dots", '●', smooth: false);</code></example>
    public BarChartStyle(string name, char full, bool smooth)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (TextWidth.Of(full.ToString()) != 1)
            throw new ArgumentException("A bar character must occupy one terminal cell.", nameof(full));

        Name = name;
        Full = full;
        Smooth = smooth;
    }

    /// <summary>Gets the descriptive name of the style.</summary>
    public string Name { get; }

    /// <summary>Gets the character of a full cell.</summary>
    public char Full { get; }

    /// <summary>Gets whether a bar may end in eighths of a cell.</summary>
    public bool Smooth { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
