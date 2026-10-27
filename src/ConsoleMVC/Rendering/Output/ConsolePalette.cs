namespace ConsoleMVC.Rendering;

/// <summary>
/// The 16 standard console colours with their ANSI SGR codes and approximate RGB values.
/// Used to emit escape codes and to map true colours onto terminals that only support 16 colours.
/// </summary>
internal static class ConsolePalette
{
    /// <summary>
    /// Entries indexed by <see cref="ConsoleColor"/> value. The RGB values follow the classic
    /// Windows console palette.
    /// </summary>
    private static readonly (int Ansi, byte R, byte G, byte B)[] Entries =
    [
        (30, 0, 0, 0),         // Black
        (34, 0, 0, 128),       // DarkBlue
        (32, 0, 128, 0),       // DarkGreen
        (36, 0, 128, 128),     // DarkCyan
        (31, 128, 0, 0),       // DarkRed
        (35, 128, 0, 128),     // DarkMagenta
        (33, 128, 128, 0),     // DarkYellow
        (37, 192, 192, 192),   // Gray
        (90, 128, 128, 128),   // DarkGray
        (94, 0, 0, 255),       // Blue
        (92, 0, 255, 0),       // Green
        (96, 0, 255, 255),     // Cyan
        (91, 255, 0, 0),       // Red
        (95, 255, 0, 255),     // Magenta
        (93, 255, 255, 0),     // Yellow
        (97, 255, 255, 255)    // White
    ];

    /// <summary>
    /// Returns the ANSI foreground code (30–37, 90–97) for a console colour.
    /// Add 10 for the background code.
    /// </summary>
    /// <param name="colour">The console colour.</param>
    public static int ForegroundCode(ConsoleColor colour) => Entries[(int)colour].Ansi;

    /// <summary>
    /// Finds the console colour closest to an RGB value, using squared Euclidean distance.
    /// </summary>
    /// <param name="r">Red component.</param>
    /// <param name="g">Green component.</param>
    /// <param name="b">Blue component.</param>
    /// <returns>The nearest console colour.</returns>
    public static ConsoleColor Nearest(byte r, byte g, byte b)
    {
        var best = 0;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < Entries.Length; i++)
        {
            var (_, er, eg, eb) = Entries[i];
            var distance = Square(r - er) + Square(g - eg) + Square(b - eb);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return (ConsoleColor)best;
    }

    /// <summary>
    /// Converts any colour to a console colour (RGB values are approximated).
    /// </summary>
    /// <param name="colour">A non-default colour.</param>
    public static ConsoleColor ToConsoleColour(Colour colour) =>
        colour.Kind == ColourKind.Rgb ? Nearest(colour.R, colour.G, colour.B) : colour.ConsoleColour;

    private static int Square(int value) => value * value;
}
