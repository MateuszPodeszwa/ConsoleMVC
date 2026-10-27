namespace ConsoleMVC.Rendering;

/// <summary>
/// The frames a spinner (<see cref="SpinnerElement"/>) cycles through, one per tick of its live region.
/// </summary>
/// <example><code>var spinner = new SpinnerElement { Label = "Loading", Style = SpinnerStyle.Line }; // | / - \</code></example>
public sealed class SpinnerStyle
{
    /// <summary>Braille dots turning in a circle: <c>⠋ ⠙ ⠹ ⠸ ⠼ ⠴ ⠦ ⠧ ⠇ ⠏</c>. The default.</summary>
    public static SpinnerStyle Dots { get; } = new("dots", ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"]);

    /// <summary>A turning line in plain ASCII: <c>| / - \</c>.</summary>
    public static SpinnerStyle Line { get; } = new("line", ["|", "/", "-", "\\"]);

    /// <summary>A circle filling in quarters: <c>◐ ◓ ◑ ◒</c>.</summary>
    public static SpinnerStyle Circle { get; } = new("circle", ["◐", "◓", "◑", "◒"]);

    /// <summary>
    /// Creates a custom spinner style.
    /// </summary>
    /// <param name="name">A descriptive name, used by <see cref="ToString"/>.</param>
    /// <param name="frames">The frames, in order. Every frame must be as wide as the first, so the label does not move.</param>
    /// <exception cref="ArgumentException">Thrown when there are no frames or they differ in width.</exception>
    /// <example><code>var moon = new SpinnerStyle("moon", ["🌑", "🌓", "🌕", "🌗"]);</code></example>
    public SpinnerStyle(string name, IReadOnlyList<string> frames)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            throw new ArgumentException("A spinner needs at least one frame.", nameof(frames));

        var safe = frames.Select(f => TextSanitiser.Sanitise(f ?? "").Replace('\n', ' ')).ToArray();
        var width = TextWidth.Of(safe[0]);
        if (width == 0 || safe.Any(f => TextWidth.Of(f) != width))
            throw new ArgumentException("Every spinner frame must have the same, non-zero width.", nameof(frames));

        Name = name;
        Frames = safe;
        Width = width;
    }

    /// <summary>Gets the descriptive name of the style.</summary>
    public string Name { get; }

    /// <summary>Gets the frames, in order.</summary>
    public IReadOnlyList<string> Frames { get; }

    /// <summary>Gets the width of each frame, in terminal cells.</summary>
    public int Width { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
