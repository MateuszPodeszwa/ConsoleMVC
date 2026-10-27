namespace ConsoleMVC.Rendering;

/// <summary>
/// One choice of a <see cref="MenuElement"/> — the <c>&lt;option&gt;</c> markup element. Its content is the label.
/// </summary>
/// <remarks>
/// The rendering engine does not know about MVC routes: <see cref="Target"/>, <see cref="Quit"/> and
/// <see cref="PostForm"/> are plain data that the MVC layer turns into a navigation result.
/// </remarks>
/// <example>
/// <code>
/// var about = new MenuOptionElement { Key = "1", Target = "Home/About" };
/// about.AppendText("About");
/// </code>
/// </example>
public sealed class MenuOptionElement : InlineElement
{
    /// <summary>Gets or sets the text the user types to choose this option (matched case-insensitively).</summary>
    public string Key { get; set; } = "";

    /// <summary>Gets or sets the destination: <c>"Controller/Action"</c>, or <c>"Action"</c> on the current controller.</summary>
    public string? Target { get; set; }

    /// <summary>Gets or sets whether choosing this option quits the application.</summary>
    public bool Quit { get; set; }

    /// <summary>Gets or sets whether the form values collected by the view are posted with the navigation.</summary>
    public bool PostForm { get; set; }
}

/// <summary>
/// A list of keyed choices — the <c>&lt;menu&gt;</c> markup element. <see cref="Choose"/> renders the options,
/// prompts, and repeats until the user types a valid key.
/// </summary>
/// <example>
/// <code>
/// var menu = new MenuElement { KeyColour = Colour.Cyan };
/// menu.Add(about);
/// menu.Add(quit);
/// MenuOptionElement? chosen = menu.Choose(renderer, ConsoleTextInput.Instance);
/// // [1] About
/// // [q] Quit
/// // Select an option: _
/// </code>
/// </example>
public sealed class MenuElement : Element
{
    /// <summary>The prompt shown when <see cref="Prompt"/> is not set.</summary>
    public const string DefaultPrompt = "Select an option: ";

    /// <summary>The message shown when <see cref="ErrorMessage"/> is not set.</summary>
    public const string DefaultErrorMessage = "Invalid choice, please try again.";

    private readonly List<MenuOptionElement> _options = [];

    /// <summary>Gets or sets the prompt written before reading the choice.</summary>
    public string Prompt { get; set; } = DefaultPrompt;

    /// <summary>Gets or sets the message shown after an invalid choice.</summary>
    public string ErrorMessage { get; set; } = DefaultErrorMessage;

    /// <summary>Gets or sets the colour of the <c>[key]</c> labels.</summary>
    public Colour KeyColour { get; set; }

    /// <summary>Gets the options in display order.</summary>
    public IReadOnlyList<MenuOptionElement> Options => _options;

    /// <summary>
    /// Appends an option.
    /// </summary>
    /// <param name="option">The option to append.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="option"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the key is empty or already used by another option.</exception>
    public void Add(MenuOptionElement option)
    {
        ArgumentNullException.ThrowIfNull(option);

        var key = option.Key.Trim();
        if (key.Length == 0)
            throw new ArgumentException("A menu option must have a key.", nameof(option));

        if (_options.Any(o => string.Equals(o.Key.Trim(), key, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"The menu already has an option with the key '{key}'.", nameof(option));

        _options.Add(option);
    }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) =>
        _options.Count == 0
            ? 0
            : Math.Min(_options.Max(o => PrefixWidth(o) + o.MeasureWidth(int.MaxValue)), Math.Max(maxWidth, 0));

    /// <inheritdoc />
    public override IReadOnlyList<RenderedLine> Layout(int width)
    {
        var keyStyle = TextStyle.Plain.WithForeground(KeyColour).WithDecoration(TextDecoration.Bold);
        var lines = new List<RenderedLine>();

        foreach (var option in _options)
        {
            var prefixWidth = PrefixWidth(option);
            var labelLines = option.Layout(Math.Max(width - prefixWidth, 1));

            for (var i = 0; i < labelLines.Count; i++)
            {
                IEnumerable<Span> prefix = i == 0
                    ? [new Span(KeyLabel(option), keyStyle), Span.Spaces(1)]
                    : [Span.Spaces(prefixWidth)];

                lines.Add(labelLines[i].Surround(prefix, []));
            }
        }

        return lines;
    }

    /// <summary>
    /// Renders the menu, then prompts until the user types the key of an option.
    /// </summary>
    /// <param name="renderer">Where the menu, prompt and error messages are written.</param>
    /// <param name="input">Where the choice is read from.</param>
    /// <returns>The chosen option, or <see langword="null"/> when the input ends before a valid choice.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public MenuOptionElement? Choose(IMarkupRenderer renderer, ITextInput input)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        renderer.Render(this);

        while (true)
        {
            renderer.WritePrompt(Prompt, TextStyle.Plain);

            var line = input.ReadLine();
            if (line is null)
                return null;

            var choice = line.Trim();
            var option = _options.FirstOrDefault(o => string.Equals(o.Key.Trim(), choice, StringComparison.OrdinalIgnoreCase));
            if (option is not null)
                return option;

            var error = new TextElement { Foreground = Colour.Red };
            error.AppendText(ErrorMessage);
            renderer.Render(error);
        }
    }

    private static string KeyLabel(MenuOptionElement option) => "[" + TextSanitiser.Sanitise(option.Key.Trim()) + "]";

    /// <summary>Width of "[key] ".</summary>
    private static int PrefixWidth(MenuOptionElement option) => TextWidth.Of(KeyLabel(option)) + 1;
}
