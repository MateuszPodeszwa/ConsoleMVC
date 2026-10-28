namespace ConsoleMVC.Rendering;

/// <summary>
/// A renderer for people using a screen reader: every element is described in plain lines of text, without borders,
/// bars or other box-drawing characters (<see cref="ScreenReaderText"/>); nothing is redrawn in place, so arrow-key
/// fields ask for typed answers and live elements are drawn once; and screens are separated by a blank line instead of
/// being erased, so what was said stays in the reader's history. Turned on with <c>builder.ScreenReader = true</c> or
/// the <c>ConsoleMVC:ScreenReader</c> setting.
/// </summary>
/// <example>
/// <code>
/// builder.Renderer = new ScreenReaderRenderer(MarkupRenderer.CreateForScreenReader());
/// </code>
/// </example>
public sealed class ScreenReaderRenderer : IMarkupRenderer
{
    private readonly IMarkupRenderer _inner;
    private bool _drawn;

    /// <summary>
    /// Wraps <paramref name="inner"/>, which draws the plain descriptions.
    /// </summary>
    /// <param name="inner">The renderer to draw with, usually <see cref="MarkupRenderer.CreateForScreenReader"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is <see langword="null"/>.</exception>
    /// <example><code>var renderer = new ScreenReaderRenderer(new MarkupRenderer(new PlainTextRenderTarget(Console.Out), () => 80));</code></example>
    public ScreenReaderRenderer(IMarkupRenderer inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public int Width => _inner.Width;

    /// <inheritdoc />
    public int Height => _inner.Height;

    /// <inheritdoc />
    /// <remarks>Always <see langword="false"/>: text that changes in place is not read out again.</remarks>
    public bool CanRewrite => false;

    /// <inheritdoc />
    public void Render(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _drawn = true;
        _inner.Render(ScreenReaderText.Describe(element));
    }

    /// <inheritdoc />
    public void Rewrite(Element element) => Render(element);

    /// <inheritdoc />
    public void WritePrompt(string text, TextStyle style)
    {
        _drawn = true;
        _inner.WritePrompt(text, style);
    }

    /// <inheritdoc />
    public void RecordInput(string text) => _inner.RecordInput(text);

    /// <inheritdoc />
    /// <remarks>Writes a blank line between screens rather than erasing what the reader has already announced.</remarks>
    public void Clear()
    {
        if (_drawn)
            _inner.Render(new LineBreakElement());
        _drawn = false;
    }

    /// <inheritdoc />
    /// <remarks>Always <see langword="false"/>: the alternate screen would hide the history the reader relies on.</remarks>
    public bool SetFullScreen(bool on) => false;
}
