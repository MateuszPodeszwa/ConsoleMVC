namespace ConsoleMVC.Rendering;

/// <summary>
/// A single-line form field — the <c>&lt;input&gt;</c> markup element. <see cref="Read"/> writes the label,
/// reads a value and applies the default and required rules.
/// </summary>
/// <example>
/// <code>
/// var name = new InputElement { Name = "Name", Label = "Name: ", Required = true };
/// string? value = name.Read(renderer, ConsoleTextInput.Instance);
///
/// var password = new InputElement { Name = "Password", Label = "Password: ", Masked = true };
/// </code>
/// </example>
public sealed class InputElement : Element, IFormField
{
    /// <summary>The message shown when a required field is left empty.</summary>
    public const string RequiredMessage = "This field is required.";

    /// <summary>The character shown for each typed character of a masked field.</summary>
    public const char MaskCharacter = '*';

    /// <summary>Gets or sets the form key the value is stored under.</summary>
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the text written before reading the value (no line break is added).</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the value used when the user enters nothing.</summary>
    public string? Default { get; set; }

    /// <summary>Gets or sets whether an empty value is rejected and the field asked again.</summary>
    public bool Required { get; set; }

    /// <summary>Gets or sets whether typed characters are hidden (for passwords and other secrets).</summary>
    public bool Masked { get; set; }

    /// <summary>Gets or sets the colour of the label.</summary>
    public Colour LabelColour { get; set; }

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) => Math.Min(TextWidth.Of(SafeLabel), Math.Max(maxWidth, 0));

    /// <inheritdoc />
    /// <remarks>Laying out an input statically shows only its label; use <see cref="Read"/> to collect a value.</remarks>
    public override IReadOnlyList<RenderedLine> Layout(int width) =>
        [new RenderedLine([new Span(TextWidth.Truncate(SafeLabel, width), LabelStyle)])];

    /// <summary>
    /// Writes the label and reads a value, repeating while a required field is empty.
    /// </summary>
    /// <param name="renderer">Where the label and messages are written.</param>
    /// <param name="input">Where the value is read from.</param>
    /// <returns>The trimmed value (or <see cref="Default"/>), or <see langword="null"/> at the end of input.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public string? Read(IMarkupRenderer renderer, ITextInput input)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        while (true)
        {
            if (SafeLabel.Length > 0)
                renderer.WritePrompt(SafeLabel, LabelStyle);

            var raw = Masked ? input.ReadMasked(MaskCharacter) : input.ReadLine();
            if (raw is null)
                return null;

            // What the terminal now shows after the label, for a redraw when the window is resized
            renderer.RecordInput(Masked ? new string(MaskCharacter, raw.Length) : raw);

            var value = raw.Trim();
            if (value.Length == 0)
                value = Default ?? "";

            if (!Required || value.Length > 0)
                return value;

            var error = new TextElement { Foreground = Colour.Red };
            error.AppendText(FrameworkText.Current.Required);
            renderer.Render(error);
        }
    }

    private string SafeLabel => TextSanitiser.Sanitise(Label).Replace('\n', ' ');

    private TextStyle LabelStyle => TextStyle.Plain.WithForeground(LabelColour);
}
