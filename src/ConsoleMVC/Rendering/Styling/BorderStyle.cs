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
    public static BorderStyle None { get; } = new("none", ' ', ' ', ' ', ' ', ' ', ' ', isVisible: false,
        teeDown: ' ', teeUp: ' ', teeRight: ' ', teeLeft: ' ', cross: ' ');

    /// <summary>Single-line box drawing characters: <c>┌─┐│└┘</c>.</summary>
    public static BorderStyle Single { get; } = new("single", '┌', '┐', '└', '┘', '─', '│',
        teeDown: '┬', teeUp: '┴', teeRight: '├', teeLeft: '┤', cross: '┼');

    /// <summary>Double-line box drawing characters: <c>╔═╗║╚╝</c>.</summary>
    public static BorderStyle Double { get; } = new("double", '╔', '╗', '╚', '╝', '═', '║',
        teeDown: '╦', teeUp: '╩', teeRight: '╠', teeLeft: '╣', cross: '╬');

    /// <summary>Single-line characters with rounded corners: <c>╭─╮│╰╯</c>.</summary>
    public static BorderStyle Rounded { get; } = new("rounded", '╭', '╮', '╰', '╯', '─', '│',
        teeDown: '┬', teeUp: '┴', teeRight: '├', teeLeft: '┤', cross: '┼');

    /// <summary>Heavy (thick) line characters: <c>┏━┓┃┗┛</c>.</summary>
    public static BorderStyle Heavy { get; } = new("heavy", '┏', '┓', '┗', '┛', '━', '┃',
        teeDown: '┳', teeUp: '┻', teeRight: '┣', teeLeft: '┫', cross: '╋');

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
    /// <param name="teeDown">Junction on the top edge of a table (<c>┬</c>); defaults to <c>+</c>.</param>
    /// <param name="teeUp">Junction on the bottom edge of a table (<c>┴</c>); defaults to <c>+</c>.</param>
    /// <param name="teeRight">Junction on the left edge of a table (<c>├</c>); defaults to <c>+</c>.</param>
    /// <param name="teeLeft">Junction on the right edge of a table (<c>┤</c>); defaults to <c>+</c>.</param>
    /// <param name="cross">Junction inside a table (<c>┼</c>); defaults to <c>+</c>.</param>
    public BorderStyle(string name, char topLeft, char topRight, char bottomLeft, char bottomRight,
        char horizontal, char vertical, bool isVisible = true,
        char teeDown = '+', char teeUp = '+', char teeRight = '+', char teeLeft = '+', char cross = '+')
    {
        Name = name;
        TopLeft = topLeft;
        TopRight = topRight;
        BottomLeft = bottomLeft;
        BottomRight = bottomRight;
        Horizontal = horizontal;
        Vertical = vertical;
        IsVisible = isVisible;
        TeeDown = teeDown;
        TeeUp = teeUp;
        TeeRight = teeRight;
        TeeLeft = teeLeft;
        Cross = cross;
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

    /// <summary>Gets the junction where a column line meets the top edge (<c>┬</c>).</summary>
    public char TeeDown { get; }

    /// <summary>Gets the junction where a column line meets the bottom edge (<c>┴</c>).</summary>
    public char TeeUp { get; }

    /// <summary>Gets the junction where a row line meets the left edge (<c>├</c>).</summary>
    public char TeeRight { get; }

    /// <summary>Gets the junction where a row line meets the right edge (<c>┤</c>).</summary>
    public char TeeLeft { get; }

    /// <summary>Gets the junction where a row line crosses a column line (<c>┼</c>).</summary>
    public char Cross { get; }

    /// <summary>Gets a value indicating whether the border is drawn at all.</summary>
    public bool IsVisible { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
