namespace ConsoleMVC.Rendering;

/// <summary>
/// Renders markup elements. Views call <see cref="Render"/> for every top-level element in a
/// <c>.cvw</c> file; the framework calls <see cref="Clear"/> before each view.
/// </summary>
/// <example>
/// <code>
/// IMarkupRenderer renderer = MarkupRenderer.CreateForConsole();
/// renderer.Render(box);
/// </code>
/// </example>
public interface IMarkupRenderer
{
    /// <summary>Gets the width, in columns, that top-level elements are laid out to.</summary>
    int Width { get; }

    /// <summary>
    /// Lays out <paramref name="element"/> at <see cref="Width"/> and writes the result.
    /// </summary>
    /// <param name="element">The element to render.</param>
    void Render(Element element);

    /// <summary>
    /// Writes a prompt without a line break, so the user types on the same line. The text is sanitised.
    /// </summary>
    /// <param name="text">The prompt text.</param>
    /// <param name="style">The style of the prompt.</param>
    /// <example><code>renderer.WritePrompt("Name: ", TextStyle.Plain);</code></example>
    void WritePrompt(string text, TextStyle style);

    /// <summary>
    /// Clears the output, if supported. Never throws because output is redirected.
    /// </summary>
    void Clear();
}

/// <summary>
/// Default <see cref="IMarkupRenderer"/>: lays elements out and writes the lines to an <see cref="IRenderTarget"/>.
/// </summary>
/// <example>
/// <code>
/// // Console output with automatic terminal detection
/// var renderer = MarkupRenderer.CreateForConsole();
///
/// // Plain text into a buffer at a fixed width (useful in tests)
/// var buffer = new StringWriter();
/// var testRenderer = new MarkupRenderer(new PlainTextRenderTarget(buffer), () => 40);
/// </code>
/// </example>
public sealed class MarkupRenderer : IMarkupRenderer
{
    /// <summary>The smallest width elements are ever laid out to.</summary>
    public const int MinimumWidth = 10;

    private readonly IRenderTarget _target;
    private readonly Func<int> _widthProvider;

    /// <summary>
    /// Creates a renderer.
    /// </summary>
    /// <param name="target">Where rendered lines are written.</param>
    /// <param name="widthProvider">
    /// Returns the current width in columns. Called for every render so that resizing the terminal is honoured.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public MarkupRenderer(IRenderTarget target, Func<int> widthProvider)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(widthProvider);
        _target = target;
        _widthProvider = widthProvider;
    }

    /// <summary>
    /// Creates a renderer for standard output, detecting colour support and terminal width.
    /// </summary>
    /// <returns>A renderer writing to the console.</returns>
    public static MarkupRenderer CreateForConsole() =>
        new(TerminalCapabilities.CreateRenderTarget(), TerminalCapabilities.GetWidth);

    /// <inheritdoc />
    public int Width => Math.Max(_widthProvider(), MinimumWidth);

    /// <inheritdoc />
    public void Render(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        foreach (var line in element.Layout(Width))
            _target.Write(line);

        _target.Flush();
    }

    /// <inheritdoc />
    public void WritePrompt(string text, TextStyle style)
    {
        var safe = TextSanitiser.Sanitise(text).Replace('\n', ' ');
        _target.WriteInline(new RenderedLine([new Span(safe, style)]));
        _target.Flush();
    }

    /// <inheritdoc />
    public void Clear()
    {
        _target.Clear();
        _target.Flush();
    }
}
