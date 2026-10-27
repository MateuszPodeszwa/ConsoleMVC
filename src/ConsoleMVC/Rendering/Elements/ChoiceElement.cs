namespace ConsoleMVC.Rendering;

/// <summary>
/// One choice of a <see cref="SelectElement"/> or <see cref="RadioElement"/>: the <c>&lt;choice&gt;</c> markup element.
/// Its content is the label the user sees; <see cref="Value"/> is what the form receives.
/// </summary>
/// <example>
/// <code>
/// var green = new ChoiceElement { Value = "green" };
/// green.AppendText("Green");
/// select.Add(green);
/// </code>
/// </example>
public sealed class ChoiceElement : InlineElement
{
    /// <summary>
    /// Gets or sets the form value of this choice. When <see langword="null"/>, the label text is the value.
    /// </summary>
    /// <example><code>new ChoiceElement { Value = "42" }; // the label can then be a product name</code></example>
    public string? Value { get; set; }

    /// <summary>Gets the label as one line of plain text, as typed answers are matched against it.</summary>
    /// <example><code>string shown = choice.LabelText; // "Green"</code></example>
    public string LabelText => Content.Replace('\n', ' ').Trim();

    /// <summary>Gets the value the form receives: <see cref="Value"/>, or the label text when no value is set.</summary>
    /// <example><code>string posted = choice.EffectiveValue; // "green"</code></example>
    public string EffectiveValue => Value ?? LabelText;
}
