namespace ConsoleMVC.Rendering;

/// <summary>
/// Text decorations that can be combined on a <see cref="TextStyle"/>.
/// </summary>
/// <example><code>var decoration = TextDecoration.Bold | TextDecoration.Underline;</code></example>
[Flags]
public enum TextDecoration
{
    /// <summary>No decoration.</summary>
    None = 0,

    /// <summary>Bold (increased intensity).</summary>
    Bold = 1,

    /// <summary>Dim (decreased intensity).</summary>
    Dim = 2,

    /// <summary>Italic. Not supported by every terminal.</summary>
    Italic = 4,

    /// <summary>Underlined.</summary>
    Underline = 8
}

/// <summary>
/// An immutable combination of foreground colour, background colour and decorations
/// applied to a run of text.
/// </summary>
/// <param name="Foreground">The text colour.</param>
/// <param name="Background">The background colour.</param>
/// <param name="Decoration">Bold, dim, italic and underline flags.</param>
/// <example>
/// <code>
/// var warning = TextStyle.Plain.WithForeground(Colour.Yellow).WithDecoration(TextDecoration.Bold);
/// </code>
/// </example>
public readonly record struct TextStyle(Colour Foreground, Colour Background, TextDecoration Decoration)
{
    /// <summary>Gets the style with default colours and no decoration.</summary>
    public static TextStyle Plain => default;

    /// <summary>Gets a value indicating whether this style changes nothing about the text.</summary>
    public bool IsPlain => Foreground.IsDefault && Background.IsDefault && Decoration == TextDecoration.None;

    /// <summary>Returns a copy of this style with a different foreground colour.</summary>
    /// <param name="colour">The new foreground colour.</param>
    public TextStyle WithForeground(Colour colour) => this with { Foreground = colour };

    /// <summary>Returns a copy of this style with a different background colour.</summary>
    /// <param name="colour">The new background colour.</param>
    public TextStyle WithBackground(Colour colour) => this with { Background = colour };

    /// <summary>Returns a copy of this style with the given decoration flags added.</summary>
    /// <param name="decoration">The decoration flags to add.</param>
    public TextStyle WithDecoration(TextDecoration decoration) => this with { Decoration = Decoration | decoration };
}
