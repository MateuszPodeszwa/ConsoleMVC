namespace ConsoleMVC.Rendering;

/// <summary>
/// The characters a progress bar (<see cref="ProgressElement"/>) is drawn with: one for the done part, one for the rest.
/// </summary>
/// <example><code>var bar = new ProgressElement { Value = 40, Style = ProgressStyle.Ascii }; // ####------</code></example>
public sealed class ProgressStyle
{
    /// <summary>Solid and shaded blocks: <c>████░░░░</c>. The default.</summary>
    public static ProgressStyle Block { get; } = new("block", '█', '░');

    /// <summary>A heavy line over a light one: <c>━━━━────</c>.</summary>
    public static ProgressStyle Line { get; } = new("line", '━', '─');

    /// <summary>Plain ASCII for terminals without Unicode: <c>####----</c>.</summary>
    public static ProgressStyle Ascii { get; } = new("ascii", '#', '-');

    /// <summary>
    /// Creates a custom progress style.
    /// </summary>
    /// <param name="name">A descriptive name, used by <see cref="ToString"/>.</param>
    /// <param name="filled">The character for the done part. Must occupy a single terminal cell.</param>
    /// <param name="empty">The character for the part still to do. Must occupy a single terminal cell.</param>
    /// <exception cref="ArgumentException">Thrown when a character does not occupy exactly one terminal cell.</exception>
    /// <example><code>var dots = new ProgressStyle("dots", '●', '·');</code></example>
    public ProgressStyle(string name, char filled, char empty)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (TextWidth.Of(filled.ToString()) != 1 || TextWidth.Of(empty.ToString()) != 1)
            throw new ArgumentException("Progress characters must each occupy one terminal cell.", nameof(filled));

        Name = name;
        Filled = filled;
        Empty = empty;
    }

    /// <summary>Gets the descriptive name of the style.</summary>
    public string Name { get; }

    /// <summary>Gets the character for the done part of the bar.</summary>
    public char Filled { get; }

    /// <summary>Gets the character for the part still to do.</summary>
    public char Empty { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
