namespace ConsoleMVC.Rendering;

/// <summary>
/// An element kept up to date on the screen, started with <see cref="ILiveDisplay.Start"/>. Change the element inside
/// <see cref="Update"/> so the change and the redraw happen together; spinners turn on their own. Dispose the region when
/// the work is done: the element is drawn one last time and stays on the screen.
/// </summary>
/// <remarks>
/// The region's timer and <see cref="Update"/> take the same lock, so they never draw at the same time. Rewriting
/// replaces the last element drawn, so draw nothing else while the region is live.
/// </remarks>
/// <example>
/// <code>
/// var bar = new ProgressElement { Max = files.Count };
/// using (var region = live.Start(bar))
/// {
///     foreach (var file in files)
///     {
///         Copy(file);
///         region.Update(() => bar.Value++);
///     }
/// }
/// </code>
/// </example>
public sealed class LiveRegion : IDisposable, IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly IMarkupRenderer _renderer;
    private readonly bool _rewrites;
    private readonly ITimer? _timer;
    private bool _disposed;

    /// <summary>Draws the element and starts the timer that turns its spinners.</summary>
    internal LiveRegion(IMarkupRenderer renderer, Element element, TimeSpan interval, TimeProvider time)
    {
        _renderer = renderer;
        Element = element;
        _rewrites = renderer.CanRewrite;
        if (!_rewrites)
            return; // a pipe or a file: drawn once, when the region ends

        renderer.Render(element);
        _timer = time.CreateTimer(_ => Tick(), null, interval, interval);
    }

    /// <summary>Gets the element kept up to date.</summary>
    public Element Element { get; }

    /// <summary>
    /// Changes the element and redraws it straight away. After the region is disposed the change is still made, but not drawn.
    /// </summary>
    /// <param name="change">The change, e.g. <c>() =&gt; bar.Value++</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="change"/> is <see langword="null"/>.</exception>
    /// <example><code>region.Update(() => { bar.Value = done; bar.Label = $"{done} of {total} "; });</code></example>
    public void Update(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);

        lock (_sync)
        {
            change();
            if (!_disposed)
                Draw();
        }
    }

    /// <summary>Redraws the element now, for changes made without <see cref="Update"/>.</summary>
    /// <example><code>bar.Value = 10; region.Refresh();</code></example>
    public void Refresh()
    {
        lock (_sync)
        {
            if (!_disposed)
                Draw();
        }
    }

    /// <summary>Turns every spinner in the element's tree by one frame and redraws; the timer calls this.</summary>
    internal void Tick()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            Advance(Element);
            Draw();
        }
    }

    /// <summary>Stops the timer and draws the element's final state. Calling it again does nothing.</summary>
    /// <example><code>region.Dispose();</code></example>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer?.Dispose();
            if (_rewrites)
                Draw();
            else
                _renderer.Render(Element);
        }
    }

    /// <summary>Stops the timer and draws the element's final state, as <see cref="Dispose"/> does.</summary>
    /// <returns>A completed task.</returns>
    /// <example><code>await using var region = live.Start(spinner);</code></example>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void Draw()
    {
        if (!_rewrites)
            return;

        try
        {
            _renderer.Rewrite(Element);
        }
        catch (IOException)
        {
            // The terminal went away; the work carries on without its display
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void Advance(Element element)
    {
        if (element is IAnimatedElement animated)
            animated.Advance();

        if (element is ContainerElement container)
        {
            foreach (var child in container.Children)
                Advance(child);
        }
    }
}
