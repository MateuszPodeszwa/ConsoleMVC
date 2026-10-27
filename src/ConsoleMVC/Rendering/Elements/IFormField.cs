namespace ConsoleMVC.Rendering;

/// <summary>
/// An interactive element that asks the user for one form value: <see cref="InputElement"/>, <see cref="SelectElement"/>,
/// <see cref="RadioElement"/> and <see cref="CheckboxElement"/>. Views store the answer under <see cref="Name"/> in their
/// form, so it is posted to the next action like any other field.
/// </summary>
/// <example>
/// <code>
/// IFormField field = new CheckboxElement { Name = "Newsletter", Label = "Send me the newsletter" };
/// string? answer = field.Read(renderer, ConsoleTextInput.Instance); // "true", "false" or null at the end of input
/// </code>
/// </example>
public interface IFormField
{
    /// <summary>Gets the form key the answer is stored under.</summary>
    string Name { get; }

    /// <summary>
    /// Asks for the value, repeating until the answer is valid.
    /// </summary>
    /// <param name="renderer">Where the field, prompts and messages are written.</param>
    /// <param name="input">Where the answer is read from.</param>
    /// <returns>The answer as a form value, or <see langword="null"/> when the input ends first.</returns>
    /// <example><code>var colour = select.Read(renderer, input);</code></example>
    string? Read(IMarkupRenderer renderer, ITextInput input);
}
