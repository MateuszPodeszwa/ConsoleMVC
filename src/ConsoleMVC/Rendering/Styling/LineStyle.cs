namespace ConsoleMVC.Rendering;

/// <summary>
/// The character used to draw a horizontal rule (<see cref="SeparatorElement"/>).
/// </summary>
/// <example><code>var rule = new SeparatorElement { Style = LineStyle.Double };</code></example>
public sealed class LineStyle
{
    /// <summary>Single line: <c>─</c>.</summary>
    public static LineStyle Single { get; } = new("single", '─');

    /// <summary>Double line: <c>═</c>.</summary>
    public static LineStyle Double { get; } = new("double", '═');

    /// <summary>Heavy line: <c>━</c>.</summary>
    public static LineStyle Heavy { get; } = new("heavy", '━');

    /// <summary>Dashed line: <c>╌</c>.</summary>
    public static LineStyle Dashed { get; } = new("dashed", '╌');

    /// <summary>ASCII hyphen: <c>-</c>.</summary>
    public static LineStyle Ascii { get; } = new("ascii", '-');

    /// <summary>
    /// Creates a custom line style.
    /// </summary>
    /// <param name="name">A descriptive name, used by <see cref="ToString"/>.</param>
    /// <param name="character">The character repeated across the line. Must occupy a single terminal cell.</param>
    /// <example><code>var dots = new LineStyle("dots", '·');</code></example>
    public LineStyle(string name, char character)
    {
        Name = name;
        Character = character;
    }

    /// <summary>Gets the descriptive name of the style.</summary>
    public string Name { get; }

    /// <summary>Gets the character repeated across the line.</summary>
    public char Character { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
