using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Describes how a <see cref="Length"/> is resolved against the available width.
/// </summary>
public enum LengthKind
{
    /// <summary>Size to the content (or fill, for elements without natural width).</summary>
    Auto,

    /// <summary>A fixed number of terminal columns.</summary>
    Fixed,

    /// <summary>A percentage (0–100) of the available width.</summary>
    Percent,

    /// <summary>
    /// All the width left over (<c>*</c>). In <c>&lt;columns&gt;</c>, fill columns share what the other columns leave;
    /// elsewhere it is the whole available width.
    /// </summary>
    Fill
}

/// <summary>
/// An element width: <c>auto</c>, a fixed number of columns, or a percentage of the available width.
/// A resolved length never exceeds the available width.
/// </summary>
/// <example>
/// <code>
/// Length a = 40;                    // implicit conversion: 40 columns
/// var b = Length.Parse("50%");      // half of the available width
/// var c = Length.Auto;              // size to content
/// int columns = b.Resolve(available: 80, natural: 10); // 40
/// </code>
/// </example>
public readonly record struct Length
{
    private Length(LengthKind kind, int value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>Gets the automatic length.</summary>
    public static Length Auto => default;

    /// <summary>Gets a length that takes all the width left over (<c>*</c>).</summary>
    /// <example><code>new ColumnElement { Width = Length.Fill };</code></example>
    public static Length Fill => new(LengthKind.Fill, 0);

    /// <summary>Gets a value indicating whether this length depends on the available width (a percentage or <see cref="Fill"/>).</summary>
    public bool IsRelative => Kind is LengthKind.Percent or LengthKind.Fill;

    /// <summary>Gets how the length is resolved.</summary>
    public LengthKind Kind { get; }

    /// <summary>Gets the number of columns (<see cref="LengthKind.Fixed"/>) or the percentage (<see cref="LengthKind.Percent"/>).</summary>
    public int Value { get; }

    /// <summary>
    /// Creates a fixed length in columns.
    /// </summary>
    /// <param name="columns">The number of columns; must not be negative.</param>
    /// <returns>The fixed length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="columns"/> is negative.</exception>
    /// <example><code>var width = Length.Fixed(60);</code></example>
    public static Length Fixed(int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columns);
        return new Length(LengthKind.Fixed, columns);
    }

    /// <summary>
    /// Creates a percentage of the available width.
    /// </summary>
    /// <param name="percent">The percentage, from 0 to 100.</param>
    /// <returns>The percentage length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="percent"/> is outside 0–100.</exception>
    /// <example><code>var width = Length.Percent(50);</code></example>
    public static Length Percent(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        return new Length(LengthKind.Percent, percent);
    }

    /// <summary>
    /// Parses <c>"auto"</c>, a column count such as <c>"40"</c>, or a percentage such as <c>"50%"</c>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed length.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="text"/> is not a valid length.</exception>
    /// <example><code>var width = Length.Parse("75%");</code></example>
    public static Length Parse(string text)
    {
        if (TryParse(text, out var length))
            return length;

        throw new FormatException($"'{text}' is not a valid length. Use 'auto', a column count such as '40', a percentage such as '50%', or '*' for the remaining width.");
    }

    /// <summary>
    /// Attempts to parse a length without throwing.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="length">When successful, the parsed length; otherwise <see cref="Auto"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid length.</returns>
    public static bool TryParse(string? text, out Length length)
    {
        length = Auto;
        var trimmed = text?.Trim() ?? "";

        if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed == "*")
        {
            length = Fill;
            return true;
        }

        if (trimmed.EndsWith('%'))
        {
            if (!int.TryParse(trimmed.AsSpan(0, trimmed.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var percent)
                || percent > 100)
                return false;

            length = new Length(LengthKind.Percent, percent);
            return true;
        }

        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var columns))
            return false;

        length = new Length(LengthKind.Fixed, columns);
        return true;
    }

    /// <summary>
    /// Resolves this length to a column count.
    /// </summary>
    /// <param name="available">The width available to the element.</param>
    /// <param name="natural">The width the element's content would like (used by <see cref="LengthKind.Auto"/>).</param>
    /// <returns>The resolved width, never greater than <paramref name="available"/> nor less than zero.</returns>
    public int Resolve(int available, int natural)
    {
        var resolved = Kind switch
        {
            LengthKind.Fixed => Value,
            LengthKind.Percent => (int)((long)available * Value / 100),
            LengthKind.Fill => available,
            _ => natural
        };

        return Math.Clamp(resolved, 0, Math.Max(available, 0));
    }

    /// <summary>Converts a column count to a fixed <see cref="Length"/>.</summary>
    /// <param name="columns">The number of columns.</param>
    public static implicit operator Length(int columns) => Fixed(columns);

    /// <summary>Parses a length string. See <see cref="Parse"/>.</summary>
    /// <param name="text">The length text.</param>
    public static implicit operator Length(string text) => Parse(text);

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        LengthKind.Fixed => Value.ToString(CultureInfo.InvariantCulture),
        LengthKind.Percent => $"{Value}%",
        LengthKind.Fill => "*",
        _ => "auto"
    };
}
