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

    /// <summary>
    /// Gets whether <see cref="Rewrite"/> can replace an element in place, so interactive elements can be answered with
    /// the arrow keys. The default is <see langword="false"/>.
    /// </summary>
    /// <example><code>if (renderer.CanRewrite) renderer.Rewrite(select);</code></example>
    bool CanRewrite => false;

    /// <summary>
    /// Replaces what the most recent <see cref="Render"/> call wrote with <paramref name="element"/>, laid out again.
    /// <c>&lt;select&gt;</c>, <c>&lt;radio&gt;</c> and <c>&lt;checkbox&gt;</c> call this after every key press. The
    /// default implementation renders the element again below.
    /// </summary>
    /// <param name="element">The element to show instead; usually the same element in a new state.</param>
    /// <example><code>renderer.Render(select); /* a key is pressed */ renderer.Rewrite(select);</code></example>
    void Rewrite(Element element) => Render(element);

    /// <summary>
    /// Gets the height of the output in rows, or <see cref="int.MaxValue"/> when it has none (a file, a pipe, a test).
    /// <c>&lt;scroll&gt;</c> uses it to fit a page in the window.
    /// </summary>
    /// <example><code>var rows = renderer.Height;</code></example>
    int Height => int.MaxValue;

    /// <summary>
    /// Switches the output to the terminal's alternate screen, or back, for <c>builder.FullScreen</c>.
    /// </summary>
    /// <param name="on"><see langword="true"/> to enter full-screen mode, <see langword="false"/> to leave it.</param>
    /// <returns><see langword="true"/> when the output switched; the default implementation returns <see langword="false"/>.</returns>
    /// <example><code>if (renderer.SetFullScreen(true)) { try { Run(); } finally { renderer.SetFullScreen(false); } }</code></example>
    bool SetFullScreen(bool on) => false;
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
    private readonly Func<int> _heightProvider;
    private readonly bool _canEraseLines;
    private readonly object _sync = new();

    // What is on the screen since the renderer last cleared it, to lay out again when the terminal is resized
    private readonly List<ScreenEntry> _history = [];
    private bool _ownsScreen;
    private bool _historyComplete = true;
    private int _renderedWidth;
    private IDisposable? _resizeWatcher;

    // The lines and width of the last element written, while nothing else has been written after it; -1 otherwise
    private int _lastElementLines = -1;
    private int _lastElementWidth;

    /// <summary>
    /// Initialises a renderer.
    /// </summary>
    /// <param name="target">Where rendered lines are written.</param>
    /// <param name="widthProvider">Returns the current width; called for every top-level element so resizing the terminal takes effect.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public MarkupRenderer(IRenderTarget target, Func<int> widthProvider)
        : this(target, widthProvider, static () => int.MaxValue)
    {
    }

    /// <summary>
    /// Initialises a renderer that knows the terminal height, so <see cref="Rewrite"/> never tries to move the cursor
    /// above the top of the window.
    /// </summary>
    /// <param name="target">Where rendered lines are written.</param>
    /// <param name="widthProvider">Returns the current width.</param>
    /// <param name="heightProvider">Returns the current height in rows.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <example><code>var renderer = new MarkupRenderer(target, () => 80, () => 24);</code></example>
    public MarkupRenderer(IRenderTarget target, Func<int> widthProvider, Func<int> heightProvider)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(widthProvider);
        ArgumentNullException.ThrowIfNull(heightProvider);
        _target = target;
        _widthProvider = widthProvider;
        _heightProvider = heightProvider;

        // Erasing no lines writes nothing; it only tells whether the target can move the cursor
        _canEraseLines = target.TryEraseLines(0);
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
        var renderer = new MarkupRenderer(TerminalCapabilities.CreateRenderTarget(), TerminalCapabilities.GetWidth,
            TerminalCapabilities.GetHeight);

        if (redrawOnResize && !Console.IsOutputRedirected)
        {
            Console.SetOut(new ForeignOutputTracker(Console.Out, renderer));
            renderer._resizeWatcher = TerminalResizeWatcher.Start(() => renderer.RedrawIfWidthChanged());
        }

        return renderer;
    }

    /// <summary>
    /// Creates a renderer for <see cref="ScreenReaderRenderer"/>: plain text to <see cref="Console.Out"/>, without colours
    /// or cursor movement, at the terminal's width.
    /// </summary>
    /// <returns>A renderer writing plain text.</returns>
    /// <example><code>builder.Renderer = new ScreenReaderRenderer(MarkupRenderer.CreateForScreenReader());</code></example>
    public static MarkupRenderer CreateForScreenReader() =>
        new(new PlainTextRenderTarget(Console.Out), TerminalCapabilities.GetWidth, TerminalCapabilities.GetHeight);

    /// <summary>Gets the current layout width (at least <see cref="MinimumWidth"/>).</summary>
    public int Width => Math.Max(_widthProvider(), MinimumWidth);

    /// <inheritdoc />
    public int Height => Math.Max(_heightProvider(), 1);

    /// <inheritdoc />
    /// <remarks>
    /// The screen history starts again on the new screen: the alternate screen starts empty, and the normal one is
    /// restored by the terminal as it was.
    /// </remarks>
    public bool SetFullScreen(bool on)
    {
        lock (_sync)
        {
            bool switched;
            using (ForeignOutputTracker.Suppress())
                switched = _target.TrySetAlternateScreen(on);

            if (switched)
            {
                _history.Clear();
                _historyComplete = true;
                _ownsScreen = on;
                _lastElementLines = -1;
            }

            return switched;
        }
    }

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
            _lastElementLines = -1;
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
            _lastElementLines = -1;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see langword="true"/> when the target can erase lines (ANSI terminals). Other targets still accept
    /// <see cref="Rewrite"/>, but it renders the element again below.
    /// </remarks>
    public bool CanRewrite => _canEraseLines;

    /// <inheritdoc />
    /// <remarks>
    /// When the last thing written was an element at the current width and it fits in the window, its lines are erased
    /// and <paramref name="element"/> is written in their place. Otherwise the screen is redrawn from its history with
    /// the last element replaced, or, when there is no usable history, the element is rendered again.
    /// </remarks>
    public void Rewrite(Element element)
    {
        ArgumentNullException.ThrowIfNull(element);

        lock (_sync)
        {
            var width = Width;
            var last = _history.FindLastIndex(e => e.Element is not null);

            if (_canEraseLines && _lastElementLines >= 0 && _lastElementWidth == width && _lastElementLines < _heightProvider())
            {
                if (_historyComplete && last == _history.Count - 1 && last >= 0)
                    _history[last] = new ScreenEntry(element, null, default, null);

                using (ForeignOutputTracker.Suppress())
                    _target.TryEraseLines(_lastElementLines);
                WriteElement(element, width);
                _target.Flush();
                return;
            }

            if (_ownsScreen && _historyComplete && last >= 0)
            {
                _history[last] = new ScreenEntry(element, null, default, null);
                RedrawLocked(width);
                return;
            }
        }

        Render(element);
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

            RedrawLocked(width);
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
        {
            _historyComplete = false;
            _lastElementLines = -1;
        }
    }

    /// <summary>Stops watching the terminal for resizes.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _resizeWatcher, null)?.Dispose();
    }

    /// <summary>Clears the screen and writes the history again at <paramref name="width"/>. Call inside the lock.</summary>
    private void RedrawLocked(int width)
    {
        _renderedWidth = width;
        using (ForeignOutputTracker.Suppress())
        {
            _target.Clear();
            foreach (var entry in _history)
            {
                if (entry.Element is not null)
                {
                    WriteElement(entry.Element, width);
                    continue;
                }

                if (entry.Prompt is not null)
                    _target.WriteInline(new RenderedLine([new Span(entry.Prompt, entry.PromptStyle)]));
                else
                    _target.Write(new RenderedLine([new Span(entry.Input!, TextStyle.Plain)]));

                _lastElementLines = -1;
            }
        }

        _target.Flush();
    }

    private void WriteElement(Element element, int width)
    {
        var lines = element.Layout(width);
        using (ForeignOutputTracker.Suppress())
        {
            foreach (var line in lines)
                _target.Write(line);
        }

        _lastElementLines = lines.Count;
        _lastElementWidth = width;
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
