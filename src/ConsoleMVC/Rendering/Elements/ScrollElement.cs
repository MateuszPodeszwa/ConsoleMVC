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

        // The page lays the content out at the width it is drawn at, so a redraw after a resize fits the new window
        var page = new ScrollPage(this, pageSize, StatusColour);
        renderer.Render(page);
        onPage?.Invoke();

        while (input.ReadKey() is { } key)
        {
            int? next = key.Key switch
            {
                ConsoleKey.UpArrow or ConsoleKey.K => page.Top - 1,
                ConsoleKey.DownArrow or ConsoleKey.J => page.Top + 1,
                ConsoleKey.PageUp or ConsoleKey.B => page.Top - page.Size,
                ConsoleKey.PageDown or ConsoleKey.Spacebar => page.Top + page.Size,
                ConsoleKey.Home => 0,
                ConsoleKey.End => int.MaxValue,
                ConsoleKey.Enter or ConsoleKey.Escape or ConsoleKey.Q => null,
                _ => page.Top
            };

            if (next is null)
                break;

            // The window may have been resized: the page size follows its height, the lines its width
            page.Size = PageSize(renderer);
            page.Top = Math.Clamp(next.Value, 0, page.LastTop(renderer.Width));
            renderer.Rewrite(page);
            onPage?.Invoke();
        }

        page.WithStatus = false;
        renderer.Rewrite(page);
    }

    private int PageSize(IMarkupRenderer renderer) =>
        Math.Max(Height ?? (renderer.Height == int.MaxValue ? int.MaxValue : renderer.Height - ReservedRows), 1);

    /// <summary>
    /// One page of the content, with the status line under it while the user scrolls. It lays the content out at the
    /// width it is drawn at (laid out once per width), so the renderer's redraw after a resize shows the page at the new
    /// width with the counts of the new layout.
    /// </summary>
    private sealed class ScrollPage(ScrollElement content, int size, Colour colour) : Element
    {
        private int _width = -1;
        private IReadOnlyList<RenderedLine> _lines = [];

        /// <summary>The first line shown, counted from 0; kept within the content at every width.</summary>
        public int Top { get; set; }

        /// <summary>The lines per page.</summary>
        public int Size { get; set; } = size;

        /// <summary>Whether the status line is shown under the page.</summary>
        public bool WithStatus { get; set; } = true;

        /// <summary>The last first line that still fills a page at <paramref name="width"/>.</summary>
        public int LastTop(int width) => Math.Max(LinesAt(width).Count - Size, 0);

        public override int MeasureWidth(int maxWidth) => 0;

        public override IReadOnlyList<RenderedLine> Layout(int width)
        {
            var lines = LinesAt(width);
            Top = Math.Clamp(Top, 0, LastTop(width));
            var page = lines.Skip(Top).Take(Size).ToList();
            if (WithStatus)
            {
                var status = FrameworkText.Format(FrameworkText.Current.ScrollStatus, Top + 1, Top + page.Count, lines.Count);
                page.Add(new RenderedLine([new Span(TextWidth.Truncate(status, width), TextStyle.Plain.WithForeground(colour))]));
            }

            return page;
        }

        private IReadOnlyList<RenderedLine> LinesAt(int width)
        {
            if (width != _width)
            {
                _lines = content.LayoutChildren(width);
                _width = width;
            }

            return _lines;
        }
    }
}
