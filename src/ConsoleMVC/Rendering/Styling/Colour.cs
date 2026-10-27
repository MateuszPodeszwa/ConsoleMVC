using System.Globalization;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Describes how a <see cref="Colour"/> value is represented.
/// </summary>
public enum ColourKind
{
    /// <summary>The terminal's default colour (no colour is applied).</summary>
    Default,

    /// <summary>One of the 16 standard console colours (<see cref="ConsoleColor"/>).</summary>
    Console,

    /// <summary>A 24-bit true colour value.</summary>
    Rgb
}

/// <summary>
/// An immutable terminal colour: the terminal default, one of the 16 standard console colours,
/// or a 24-bit RGB value.
/// </summary>
/// <remarks>
/// Colours can be created from names (<c>"cyan"</c>, <c>"darkgrey"</c>), hex strings
/// (<c>"#ff8800"</c>, <c>"#f80"</c>) or <see cref="ConsoleColor"/> values. Both British
/// (<c>grey</c>) and American (<c>gray</c>) spellings are accepted.
/// </remarks>
/// <example>
/// <code>
/// Colour a = Colour.Cyan;
/// Colour b = "#ff8800";              // implicit conversion from string
/// Colour c = ConsoleColor.DarkGreen; // implicit conversion from ConsoleColor
/// </code>
/// </example>
public readonly struct Colour : IEquatable<Colour>
{
    /// <summary>
    /// Name lookup for the 16 console colours. Keys are normalised (lower case, no dashes,
    /// underscores or spaces) and include British and American spellings of grey.
    /// </summary>
    private static readonly Dictionary<string, ConsoleColor> NamedColours = BuildNameTable();

    /// <summary>Gets the terminal's default colour. Rendering it emits no colour codes.</summary>
    public static Colour Default => default;

    /// <summary>Black (<see cref="ConsoleColor.Black"/>).</summary>
    public static Colour Black => new(ConsoleColor.Black);
    /// <summary>Dark blue (<see cref="ConsoleColor.DarkBlue"/>).</summary>
    public static Colour DarkBlue => new(ConsoleColor.DarkBlue);
    /// <summary>Dark green (<see cref="ConsoleColor.DarkGreen"/>).</summary>
    public static Colour DarkGreen => new(ConsoleColor.DarkGreen);
    /// <summary>Dark cyan (<see cref="ConsoleColor.DarkCyan"/>).</summary>
    public static Colour DarkCyan => new(ConsoleColor.DarkCyan);
    /// <summary>Dark red (<see cref="ConsoleColor.DarkRed"/>).</summary>
    public static Colour DarkRed => new(ConsoleColor.DarkRed);
    /// <summary>Dark magenta (<see cref="ConsoleColor.DarkMagenta"/>).</summary>
    public static Colour DarkMagenta => new(ConsoleColor.DarkMagenta);
    /// <summary>Dark yellow (<see cref="ConsoleColor.DarkYellow"/>).</summary>
    public static Colour DarkYellow => new(ConsoleColor.DarkYellow);
    /// <summary>Grey (<see cref="ConsoleColor.Gray"/>).</summary>
    public static Colour Grey => new(ConsoleColor.Gray);
    /// <summary>Dark grey (<see cref="ConsoleColor.DarkGray"/>).</summary>
    public static Colour DarkGrey => new(ConsoleColor.DarkGray);
    /// <summary>Bright blue (<see cref="ConsoleColor.Blue"/>).</summary>
    public static Colour Blue => new(ConsoleColor.Blue);
    /// <summary>Bright green (<see cref="ConsoleColor.Green"/>).</summary>
    public static Colour Green => new(ConsoleColor.Green);
    /// <summary>Bright cyan (<see cref="ConsoleColor.Cyan"/>).</summary>
    public static Colour Cyan => new(ConsoleColor.Cyan);
    /// <summary>Bright red (<see cref="ConsoleColor.Red"/>).</summary>
    public static Colour Red => new(ConsoleColor.Red);
    /// <summary>Bright magenta (<see cref="ConsoleColor.Magenta"/>).</summary>
    public static Colour Magenta => new(ConsoleColor.Magenta);
    /// <summary>Bright yellow (<see cref="ConsoleColor.Yellow"/>).</summary>
    public static Colour Yellow => new(ConsoleColor.Yellow);
    /// <summary>White (<see cref="ConsoleColor.White"/>).</summary>
    public static Colour White => new(ConsoleColor.White);

    private Colour(ConsoleColor consoleColour)
    {
        Kind = ColourKind.Console;
        ConsoleColour = consoleColour;
    }

    private Colour(byte r, byte g, byte b)
    {
        Kind = ColourKind.Rgb;
        R = r;
        G = g;
        B = b;
    }

    /// <summary>Gets how this colour is represented.</summary>
    public ColourKind Kind { get; }

    /// <summary>Gets the console colour. Only meaningful when <see cref="Kind"/> is <see cref="ColourKind.Console"/>.</summary>
    public ConsoleColor ConsoleColour { get; }

    /// <summary>Gets the red component. Only meaningful when <see cref="Kind"/> is <see cref="ColourKind.Rgb"/>.</summary>
    public byte R { get; }

    /// <summary>Gets the green component. Only meaningful when <see cref="Kind"/> is <see cref="ColourKind.Rgb"/>.</summary>
    public byte G { get; }

    /// <summary>Gets the blue component. Only meaningful when <see cref="Kind"/> is <see cref="ColourKind.Rgb"/>.</summary>
    public byte B { get; }

    /// <summary>Gets a value indicating whether this is the terminal default colour.</summary>
    public bool IsDefault => Kind == ColourKind.Default;

    /// <summary>
    /// Creates a colour from one of the 16 standard console colours.
    /// </summary>
    /// <param name="consoleColour">The console colour.</param>
    /// <returns>The equivalent <see cref="Colour"/>.</returns>
    /// <example><code>var c = Colour.FromConsoleColor(ConsoleColor.Cyan);</code></example>
    public static Colour FromConsoleColor(ConsoleColor consoleColour) => new(consoleColour);

    /// <summary>
    /// Creates a 24-bit true colour.
    /// </summary>
    /// <param name="r">Red component (0–255).</param>
    /// <param name="g">Green component (0–255).</param>
    /// <param name="b">Blue component (0–255).</param>
    /// <returns>The RGB <see cref="Colour"/>.</returns>
    /// <example><code>var orange = Colour.FromRgb(255, 136, 0);</code></example>
    public static Colour FromRgb(byte r, byte g, byte b) => new(r, g, b);

    /// <summary>
    /// Parses a colour name (<c>"red"</c>, <c>"darkgrey"</c>, <c>"default"</c>) or hex value
    /// (<c>"#rgb"</c>, <c>"#rrggbb"</c>). An empty string yields <see cref="Default"/>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed colour.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="text"/> is not a recognised colour.</exception>
    /// <example><code>var c = Colour.Parse("#f80");</code></example>
    public static Colour Parse(string text)
    {
        if (TryParse(text, out var colour))
            return colour;

        throw new FormatException(
            $"'{text}' is not a valid colour. Use a console colour name (e.g. 'cyan', 'darkgrey') " +
            "or a hex value such as '#ff8800'.");
    }

    /// <summary>
    /// Attempts to parse a colour name or hex value without throwing.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="colour">When successful, the parsed colour; otherwise <see cref="Default"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid colour.</returns>
    /// <example><code>if (Colour.TryParse(input, out var c)) { ... }</code></example>
    public static bool TryParse(string? text, out Colour colour)
    {
        colour = Default;
        var trimmed = text?.Trim() ?? "";

        if (trimmed.Length == 0 || trimmed.Equals("default", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed[0] == '#')
            return TryParseHex(trimmed.AsSpan(1), out colour);

        if (NamedColours.TryGetValue(Normalise(trimmed), out var consoleColour))
        {
            colour = new Colour(consoleColour);
            return true;
        }

        return false;
    }

    /// <summary>Converts a <see cref="ConsoleColor"/> to a <see cref="Colour"/>.</summary>
    /// <param name="consoleColour">The console colour.</param>
    public static implicit operator Colour(ConsoleColor consoleColour) => new(consoleColour);

    /// <summary>Parses a colour string. See <see cref="Parse"/>.</summary>
    /// <param name="text">The colour name or hex value.</param>
    /// <exception cref="FormatException">Thrown when the text is not a valid colour.</exception>
    public static implicit operator Colour(string text) => Parse(text);

    /// <inheritdoc />
    public bool Equals(Colour other) =>
        Kind == other.Kind && ConsoleColour == other.ConsoleColour && R == other.R && G == other.G && B == other.B;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Colour other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, ConsoleColour, R, G, B);

    /// <summary>Compares two colours for equality.</summary>
    public static bool operator ==(Colour left, Colour right) => left.Equals(right);

    /// <summary>Compares two colours for inequality.</summary>
    public static bool operator !=(Colour left, Colour right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        ColourKind.Console => ConsoleColour.ToString(),
        ColourKind.Rgb => $"#{R:x2}{G:x2}{B:x2}",
        _ => "default"
    };

    /// <summary>
    /// Parses the digits of a <c>#rgb</c> or <c>#rrggbb</c> hex colour (without the leading <c>#</c>).
    /// </summary>
    private static bool TryParseHex(ReadOnlySpan<char> digits, out Colour colour)
    {
        colour = Default;

        if (digits.Length == 3)
        {
            if (!TryHexDigit(digits[0], out var r) || !TryHexDigit(digits[1], out var g) || !TryHexDigit(digits[2], out var b))
                return false;

            // #f80 expands to #ff8800
            colour = new Colour((byte)(r * 17), (byte)(g * 17), (byte)(b * 17));
            return true;
        }

        if (digits.Length == 6 && uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            colour = new Colour((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }

        return false;
    }

    private static bool TryHexDigit(char c, out int value)
    {
        value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1
        };
        return value >= 0;
    }

    /// <summary>Lower-cases a colour name and removes dashes, underscores and spaces.</summary>
    private static string Normalise(string name) =>
        name.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();

    private static Dictionary<string, ConsoleColor> BuildNameTable()
    {
        var table = new Dictionary<string, ConsoleColor>(StringComparer.Ordinal);

        foreach (var value in Enum.GetValues<ConsoleColor>())
        {
            var name = value.ToString().ToLowerInvariant();
            table[name] = value;

            // British spelling alias: gray → grey, darkgray → darkgrey
            if (name.Contains("gray"))
                table[name.Replace("gray", "grey")] = value;
        }

        return table;
    }
}
