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

    /// <summary>
    /// Records what the user typed after a prompt, as the terminal showed it (masked input as mask characters), so a
    /// redraw after the terminal is resized can show it again. <c>&lt;input&gt;</c> and <c>&lt;menu&gt;</c> call this.
    /// The default implementation does nothing.
    /// </summary>
    /// <param name="text">The text the user typed, without the line break.</param>
    /// <example><code>renderer.RecordInput(answer);</code></example>
    void RecordInput(string text)
    {
    }
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
public sealed class MarkupRenderer : IMarkupRenderer, IDisposable
{
    /// <summary>The smallest width elements are laid out to, so very narrow terminals still render something sensible.</summary>
    public const int MinimumWidth = 10;

    /// <summary>
    /// The most render operations kept for one screen. Longer screens (for example a view that prints a log) are not
    /// redrawn on resize, so memory stays bounded.
    /// </summary>
    public const int MaxRedrawHistory = 2000;

    private readonly IRenderTarget _target;
    private readonly Func<int> _widthProvider;
    private readonly object _sync = new();

    // What is on the screen since the renderer last cleared it, to lay out again when the terminal is resized
    private readonly List<ScreenEntry> _history = [];
    private bool _ownsScreen;
    private bool _historyComplete = true;
    private int _renderedWidth;
    private IDisposable? _resizeWatcher;

    /// <summary>
    /// Initialises a renderer.
    /// </summary>
    /// <param name="target">Where rendered lines are written.</param>
    /// <param name="widthProvider">Returns the current width; called for every top-level element so resizing the terminal takes effect.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public MarkupRenderer(IRenderTarget target, Func<int> widthProvider)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(widthProvider);
        _target = target;
        _widthProvider = widthProvider;
    }

    /// <summary>
    /// Creates a renderer for the current console, choosing ANSI, legacy Windows console or plain-text output
    /// automatically (see <see cref="TerminalCapabilities"/>).
    /// </summary>
    /// <param name="redrawOnResize">
    /// When <see langword="true"/> (the default) and the output is a terminal, the current screen is laid out again
    /// whenever the terminal window is resized, so tables, boxes and columns follow the new width.
    /// </param>
    /// <returns>A renderer writing to <see cref="Console.Out"/>.</returns>
    /// <example><code>builder.Renderer = MarkupRenderer.CreateForConsole(redrawOnResize: false);</code></example>
    public static MarkupRenderer CreateForConsole(bool redrawOnResize = true)
    {
        // The target keeps the real console writer; what views write to Console.Out afterwards is tracked separately
        var renderer = new MarkupRenderer(TerminalCapabilities.CreateRenderTarget(), TerminalCapabilities.GetWidth);

        if (redrawOnResize && !Console.IsOutputRedirected)
        {
            Console.SetOut(new ForeignOutputTracker(Console.Out, renderer));
            renderer._resizeWatcher = TerminalResizeWatcher.Start(() => renderer.RedrawIfWidthChanged());
        }

        return renderer;
    }

    /// <summary>Gets the current layout width (at least <see cref="MinimumWidth"/>).</summary>
    public int Width => Math.Max(_widthProvider(), MinimumWidth);

    /// <inheritdoc />
    public void Render(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        lock (_sync)
        {
            Remember(new ScreenEntry(element, null, default, null));
            WriteElement(element, Width);
            _target.Flush();
        }
    }

    /// <inheritdoc />
    public void WritePrompt(string text, TextStyle style)
    {
        lock (_sync)
        {
            var safe = TextSanitiser.Sanitise(text).Replace('\n', ' ');
            Remember(new ScreenEntry(null, safe, style, null));
            using (ForeignOutputTracker.Suppress())
                _target.WriteInline(new RenderedLine([new Span(safe, style)]));
            _target.Flush();
        }
    }

    /// <inheritdoc />
    public void RecordInput(string text)
    {
        lock (_sync)
            Remember(new ScreenEntry(null, null, default, TextSanitiser.Sanitise(text).Replace('\n', ' ')));
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_sync)
        {
            using (ForeignOutputTracker.Suppress())
                _target.Clear();
            _target.Flush();

            _history.Clear();
            _historyComplete = true;
            _ownsScreen = true;
            _renderedWidth = Width;
        }
    }

    /// <summary>
    /// Clears the screen and lays out everything rendered since the last <see cref="Clear"/> again when the width has
    /// changed since it was drawn. The terminal resize watcher calls this; it is safe to call from any thread.
    /// </summary>
    /// <returns><see langword="true"/> when the screen was redrawn.</returns>
    /// <remarks>
    /// Nothing is redrawn when the renderer has not cleared the screen yet (it would erase what was there before the
    /// application started), when something other than the renderer wrote to the console since the last clear (that
    /// output cannot be laid out again), or when the screen history exceeded <see cref="MaxRedrawHistory"/>.
    /// </remarks>
    /// <example><code>if (renderer.RedrawIfWidthChanged()) log.Add("redrawn");</code></example>
    public bool RedrawIfWidthChanged()
    {
        lock (_sync)
        {
            var width = Width;
            if (!_ownsScreen || !_historyComplete || width == _renderedWidth)
                return false;

            _renderedWidth = width;
            using (ForeignOutputTracker.Suppress())
            {
                _target.Clear();
                foreach (var entry in _history)
                {
                    if (entry.Element is not null)
                        WriteElement(entry.Element, width);
                    else if (entry.Prompt is not null)
                        _target.WriteInline(new RenderedLine([new Span(entry.Prompt, entry.PromptStyle)]));
                    else
                        _target.Write(new RenderedLine([new Span(entry.Input!, TextStyle.Plain)]));
                }
            }

            _target.Flush();
            return true;
        }
    }

    /// <summary>
    /// Records that something other than the renderer wrote to the screen, so the current screen is not redrawn on
    /// resize. <see cref="ForeignOutputTracker"/> calls this for writes to <see cref="Console.Out"/> from views.
    /// </summary>
    public void MarkForeignOutput()
    {
        lock (_sync)
            _historyComplete = false;
    }

    /// <summary>Stops watching the terminal for resizes.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _resizeWatcher, null)?.Dispose();
    }

    private void WriteElement(Element element, int width)
    {
        using (ForeignOutputTracker.Suppress())
        {
            foreach (var line in element.Layout(width))
                _target.Write(line);
        }
    }

    private void Remember(ScreenEntry entry)
    {
        if (!_historyComplete)
            return;

        if (_history.Count >= MaxRedrawHistory)
        {
            _history.Clear();
            _historyComplete = false;
            return;
        }

        _history.Add(entry);
    }

    /// <summary>One thing on the screen: an element, a prompt, or what the user typed after a prompt.</summary>
    private readonly record struct ScreenEntry(Element? Element, string? Prompt, TextStyle PromptStyle, string? Input);
}
