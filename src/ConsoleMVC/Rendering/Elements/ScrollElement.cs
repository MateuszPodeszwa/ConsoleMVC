namespace ConsoleMVC.Rendering;

/// <summary>
/// Content longer than the screen, shown a page at a time: the <c>&lt;scroll&gt;</c> markup element. In a terminal the
/// user scrolls with the arrow keys, Page Up, Page Down, Space, Home and End, and continues with Enter, Esc or Q; with
/// piped input or output, or when the content fits, everything is drawn at once.
/// </summary>
/// <example>
/// <code>
/// var log = new ScrollElement { Height = 15 };
/// foreach (var entry in entries)
///     log.Add(new TextElement(entry));
/// log.Show(renderer, input);
/// </code>
/// </example>
public sealed class ScrollElement : ContainerElement
{
    /// <summary>The rows of the window a page leaves free when <see cref="Height"/> is not set: the status line, the prompt and one more.</summary>
    public const int ReservedRows = 3;

    /// <summary>Gets or sets the lines per page, or <see langword="null"/> (the default) to fill the window.</summary>
    /// <example><code>scroll.Height = 10;</code></example>
    public int? Height { get; set; }

    /// <summary>Gets or sets the colour of the status line shown while scrolling.</summary>
    public Colour StatusColour { get; set; } = Colour.DarkGrey;

    /// <inheritdoc />
    public override int MeasureWidth(int maxWidth) => MeasureChildren(maxWidth);

    /// <inheritdoc />
    /// <remarks>Laid out as an element (inside a box, or by <see cref="IMarkupRenderer.Render"/>), all the content is drawn.</remarks>
    public override IReadOnlyList<RenderedLine> Layout(int width) => LayoutChildren(width);

    /// <summary>
    /// Shows the content a page at a time and waits until the user continues; draws it all at once when it fits or the
    /// keys cannot be read. The page the user ends on stays on the screen.
    /// </summary>
    /// <param name="renderer">The renderer to draw with.</param>
    /// <param name="input">Where the keys come from.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <example><code>scroll.Show(Renderer, Input);</code></example>
    public void Show(IMarkupRenderer renderer, ITextInput input) => Show(renderer, input, onPage: null);

    /// <summary>Shows the content; <paramref name="onPage"/> is called after each page is drawn (for tests).</summary>
    internal void Show(IMarkupRenderer renderer, ITextInput input, Action? onPage)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(input);

        var pageSize = PageSize(renderer);
        var lines = Layout(renderer.Width);
        if (lines.Count <= pageSize || !renderer.CanRewrite || !input.CanReadKeys)
        {
            renderer.Render(this);
            return;
        }

        var top = 0;
        renderer.Render(new ScrollPage(lines, top, pageSize, StatusColour, withStatus: true));
        onPage?.Invoke();

        while (input.ReadKey() is { } key)
        {
            var last = Math.Max(lines.Count - pageSize, 0);
            int? next = key.Key switch
            {
                ConsoleKey.UpArrow or ConsoleKey.K => top - 1,
                ConsoleKey.DownArrow or ConsoleKey.J => top + 1,
                ConsoleKey.PageUp or ConsoleKey.B => top - pageSize,
                ConsoleKey.PageDown or ConsoleKey.Spacebar => top + pageSize,
                ConsoleKey.Home => 0,
                ConsoleKey.End => last,
                ConsoleKey.Enter or ConsoleKey.Escape or ConsoleKey.Q => null,
                _ => top
            };

            if (next is null)
                break;

            // The window may have been resized: lay the content out again at the current width
            lines = Layout(renderer.Width);
            pageSize = PageSize(renderer);
            top = Math.Clamp(next.Value, 0, Math.Max(lines.Count - pageSize, 0));
            renderer.Rewrite(new ScrollPage(lines, top, pageSize, StatusColour, withStatus: true));
            onPage?.Invoke();
        }

        renderer.Rewrite(new ScrollPage(lines, top, pageSize, StatusColour, withStatus: false));
    }

    private int PageSize(IMarkupRenderer renderer) =>
        Math.Max(Height ?? (renderer.Height == int.MaxValue ? int.MaxValue : renderer.Height - ReservedRows), 1);

    /// <summary>One page of the content, with the status line under it while the user scrolls.</summary>
    private sealed class ScrollPage(IReadOnlyList<RenderedLine> lines, int top, int size, Colour colour, bool withStatus) : Element
    {
        public override int MeasureWidth(int maxWidth) => 0;

        public override IReadOnlyList<RenderedLine> Layout(int width)
        {
            var page = lines.Skip(top).Take(size).ToList();
            if (withStatus)
            {
                var status = FrameworkText.Format(FrameworkText.Current.ScrollStatus, top + 1, top + page.Count, lines.Count);
                page.Add(new RenderedLine([new Span(TextWidth.Truncate(status, width), TextStyle.Plain.WithForeground(colour))]));
            }

            return page;
        }
    }
}
