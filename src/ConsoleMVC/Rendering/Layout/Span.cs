namespace ConsoleMVC.Rendering;

/// <summary>
/// A run of text drawn with a single <see cref="TextStyle"/>. The text must not contain line breaks.
/// </summary>
/// <param name="Text">The sanitised text of the run.</param>
/// <param name="Style">The style applied to the run.</param>
/// <example><code>var span = new Span("Error", TextStyle.Plain.WithForeground(Colour.Red));</code></example>
public readonly record struct Span(string Text, TextStyle Style)
{
    /// <summary>Gets the display width of the run in terminal columns.</summary>
    public int Width => TextWidth.Of(Text);

    /// <summary>
    /// Creates an unstyled run of <paramref name="count"/> spaces.
    /// </summary>
    /// <param name="count">The number of spaces.</param>
    /// <returns>The padding run.</returns>
    public static Span Spaces(int count) => new(new string(' ', Math.Max(count, 0)), TextStyle.Plain);
}
