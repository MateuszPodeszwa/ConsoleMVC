namespace ConsoleMVC.Rendering;

/// <summary>
/// A short list of choices shown side by side, of which exactly one is the answer: the <c>&lt;radio&gt;</c> markup
/// element. Left and Right (or Up and Down) move the selection in a terminal.
/// </summary>
/// <example>
/// <code>
/// var size = new RadioElement { Name = "Size", Label = "Size:", Default = "M" };
/// // add ChoiceElements S, M and L
///
/// // Arrow keys: Size: ( ) S  (*) M  ( ) L  (Left/Right, Enter)
/// // Typed:      Size: 1) S  2) M  3) L
/// //             Choose 1-3 [M]: _
/// string? answer = size.Read(renderer, ConsoleTextInput.Instance);
/// </code>
/// </example>
public sealed class RadioElement : ChoiceFieldElement
{
    /// <inheritdoc />
    protected override bool Horizontal => true;

    /// <inheritdoc />
    protected override string ElementName => "<radio>";
}
