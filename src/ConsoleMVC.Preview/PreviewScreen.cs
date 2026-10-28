using ConsoleMVC.Rendering;

namespace ConsoleMVC.Preview;

/// <summary>
/// Draws the elements of a preview as the application draws them in a terminal. A form field and a menu are drawn by
/// their own <c>Read</c> or <c>Choose</c>, given a terminal whose input has already ended, so they show exactly the
/// frame the application shows when it first waits for the answer: a <c>&lt;checkbox checked&gt;</c> ticked, a
/// <c>&lt;number&gt;</c> or <c>&lt;date&gt;</c> with its default, a <c>&lt;select&gt;</c> or <c>&lt;radio&gt;</c> with its
/// default highlighted and the keys to press, an <c>&lt;input&gt;</c> or a <c>&lt;menu&gt;</c> with its prompt. The
/// line of a prompt ends there, as it does when the user presses Enter.
/// </summary>
/// <param name="renderer">The renderer that lays the elements out.</param>
/// <param name="target">Where <paramref name="renderer"/> writes, to end a prompt's line.</param>
/// <example>
/// <code>
/// var screen = new PreviewScreen(new MarkupRenderer(target, () => 80), target);
/// foreach (var element in elements) screen.Draw(element);
/// screen.Finish();
/// </code>
/// </example>
internal sealed class PreviewScreen(IMarkupRenderer renderer, IRenderTarget target) : IMarkupRenderer
{
    // Whether the last thing written was a prompt, whose line the user would end with Enter
    private bool _prompting;

    /// <inheritdoc />
    public int Width => renderer.Width;

    /// <inheritdoc />
    /// <remarks>
    /// <see langword="true"/>, so fields draw their terminal frame. Nothing is ever rewritten: the input ends before the
    /// first key press.
    /// </remarks>
    public bool CanRewrite => true;

    /// <summary>Draws one top-level element; a field or a menu as it first waits for the user.</summary>
    /// <param name="element">The element.</param>
    /// <example><code>screen.Draw(new CheckboxElement { Label = "Send news", Checked = true }); // [x] Send news  (Space, Enter)</code></example>
    public void Draw(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        // The previous field's prompt line ends here, as when the user pressed Enter; one field may write several
        // prompts on its line (a label, then the value Enter keeps)
        EndPrompt();
        switch (element)
        {
            case IFormField field:
                field.Read(this, EndedInput.Instance);
                break;
            case MenuElement menu:
                menu.Choose(this, EndedInput.Instance);
                break;
            default:
                Render(element);
                break;
        }
    }

    /// <summary>Ends the line of a prompt written last; call it once everything is drawn.</summary>
    /// <example><code>screen.Finish();</code></example>
    public void Finish() => EndPrompt();

    /// <inheritdoc />
    public void Render(Element element)
    {
        EndPrompt();
        renderer.Render(element);
    }

    /// <inheritdoc />
    public void Rewrite(Element element) => Render(element);

    /// <inheritdoc />
    public void WritePrompt(string text, TextStyle style)
    {
        renderer.WritePrompt(text, style);
        _prompting = true;
    }

    /// <inheritdoc />
    /// <remarks>A preview is one screen, so nothing is cleared.</remarks>
    public void Clear()
    {
    }

    private void EndPrompt()
    {
        if (!_prompting)
            return;

        target.Write(RenderedLine.Empty);
        _prompting = false;
    }

    /// <summary>A terminal's input that has already ended: no line and no key press is ever read.</summary>
    private sealed class EndedInput : ITextInput
    {
        public static readonly EndedInput Instance = new();

        public bool CanReadKeys => true;

        public string? ReadLine() => null;

        public string? ReadMasked(char mask) => null;

        public ConsoleKeyInfo? ReadKey() => null;
    }
}
