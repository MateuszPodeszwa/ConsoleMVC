namespace ConsoleMVC.Rendering;

/// <summary>
/// A list of choices shown one under another: the <c>&lt;select&gt;</c> markup element. With <see cref="Multiple"/>
/// the user may tick several, and the form value is their values separated by commas (which binds to a list).
/// </summary>
/// <example>
/// <code>
/// var colour = new SelectElement { Name = "Colour", Label = "Favourite colour:", Default = "green" };
/// colour.Add(new ChoiceElement { Value = "red" });   // AppendText("Red") on each choice for its label
/// colour.Add(new ChoiceElement { Value = "green" });
///
/// // Arrow keys in a terminal:          Typed answers otherwise:
/// // Favourite colour: (Up/Down, Enter)  Favourite colour:
/// //  > red                                1) red
/// //    green                              2) green
/// //                                     Choose 1-2 [green]: _
/// string? answer = colour.Read(renderer, ConsoleTextInput.Instance);
/// </code>
/// </example>
public sealed class SelectElement : ChoiceFieldElement
{
    /// <summary>
    /// Gets or sets whether several choices may be ticked (Space in arrow-key mode, <c>1,3</c> when typed). The answer
    /// is the ticked values in the order of the choices, separated by commas.
    /// </summary>
    /// <example><code>new SelectElement { Name = "Toppings", Multiple = true };</code></example>
    public bool Multiple { get; set; }

    /// <summary>
    /// Gets or sets whether typing narrows a long list: in a terminal the typed text filters the choices (Backspace
    /// edits it, Esc clears it); a typed answer may be part of a name when it matches only one choice.
    /// </summary>
    /// <example><code>new SelectElement { Name = "Country", Filter = true, Size = 8 };</code></example>
    public bool Filter { get; set; }

    /// <summary>
    /// Gets or sets how many choices are shown at once in a terminal; the list scrolls with the highlight and PageUp and
    /// PageDown move a page. 0 (the default) shows every choice.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    /// <example><code>new SelectElement { Name = "Country", Size = 8 };</code></example>
    public int Size
    {
        get => _size;
        set => _size = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Size cannot be negative.");
    }

    private int _size;

    /// <inheritdoc />
    protected override bool AllowsSeveral => Multiple;

    /// <inheritdoc />
    protected override bool AllowsFilter => Filter;

    /// <inheritdoc />
    protected override int VisibleRows => Size;

    /// <inheritdoc />
    protected override bool Horizontal => false;

    /// <inheritdoc />
    protected override string ElementName => "<select>";
}
