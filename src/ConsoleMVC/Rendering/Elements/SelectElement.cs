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

    /// <inheritdoc />
    protected override bool AllowsSeveral => Multiple;

    /// <inheritdoc />
    protected override bool Horizontal => false;

    /// <inheritdoc />
    protected override string ElementName => "<select>";
}
