namespace ConsoleMVC.Rendering;

/// <summary>
/// The set of characters used to draw the frame of a <see cref="BoxElement"/>.
/// </summary>
/// <remarks>
/// Use one of the predefined styles (<see cref="Single"/>, <see cref="Double"/>, <see cref="Rounded"/>,
/// <see cref="Heavy"/>, <see cref="Ascii"/>, <see cref="None"/>) or create a custom one with the constructor.
/// </remarks>
/// <example>
/// <code>
/// var box = new BoxElement { Border = BorderStyle.Rounded };
/// var stars = new BorderStyle("stars", '*', '*', '*', '*', '*', '*');
/// </code>
/// </example>
public sealed class BorderStyle
{
    /// <summary>No border. The box's title, if any, is shown as a heading line.</summary>
    public static BorderStyle None { get; } = new("none", ' ', ' ', ' ', ' ', ' ', ' ', isVisible: false);

    /// <summary>Single-line box drawing characters: <c>┌─┐│└┘</c>.</summary>
    public static BorderStyle Single { get; } = new("single", '┌', '┐', '└', '┘', '─', '│');

    /// <summary>Double-line box drawing characters: <c>╔═╗║╚╝</c>.</summary>
    public static BorderStyle Double { get; } = new("double", '╔', '╗', '╚', '╝', '═', '║');

    /// <summary>Single-line characters with rounded corners: <c>╭─╮│╰╯</c>.</summary>
    public static BorderStyle Rounded { get; } = new("rounded", '╭', '╮', '╰', '╯', '─', '│');

    /// <summary>Heavy (thick) line characters: <c>┏━┓┃┗┛</c>.</summary>
    public static BorderStyle Heavy { get; } = new("heavy", '┏', '┓', '┗', '┛', '━', '┃');

    /// <summary>Plain ASCII characters for terminals without Unicode support: <c>+-+|++</c>.</summary>
    public static BorderStyle Ascii { get; } = new("ascii", '+', '+', '+', '+', '-', '|');

    /// <summary>
    /// Creates a custom border style.
    /// </summary>
    /// <param name="name">A descriptive name, used by <see cref="ToString"/>.</param>
    /// <param name="topLeft">Top-left corner character.</param>
    /// <param name="topRight">Top-right corner character.</param>
    /// <param name="bottomLeft">Bottom-left corner character.</param>
    /// <param name="bottomRight">Bottom-right corner character.</param>
    /// <param name="horizontal">Character for the top and bottom edges.</param>
    /// <param name="vertical">Character for the left and right edges.</param>
    /// <param name="isVisible"><see langword="false"/> for a style that draws nothing.</param>
    public BorderStyle(string name, char topLeft, char topRight, char bottomLeft, char bottomRight,
        char horizontal, char vertical, bool isVisible = true)
    {
        Name = name;
        TopLeft = topLeft;
        TopRight = topRight;
        BottomLeft = bottomLeft;
        BottomRight = bottomRight;
        Horizontal = horizontal;
        Vertical = vertical;
        IsVisible = isVisible;
    }

    /// <summary>Gets the descriptive name of the style.</summary>
    public string Name { get; }

    /// <summary>Gets the top-left corner character.</summary>
    public char TopLeft { get; }

    /// <summary>Gets the top-right corner character.</summary>
    public char TopRight { get; }

    /// <summary>Gets the bottom-left corner character.</summary>
    public char BottomLeft { get; }

    /// <summary>Gets the bottom-right corner character.</summary>
    public char BottomRight { get; }

    /// <summary>Gets the character used for horizontal edges.</summary>
    public char Horizontal { get; }

    /// <summary>Gets the character used for vertical edges.</summary>
    public char Vertical { get; }

    /// <summary>Gets a value indicating whether the border is drawn at all.</summary>
    public bool IsVisible { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
