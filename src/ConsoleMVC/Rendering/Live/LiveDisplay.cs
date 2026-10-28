namespace ConsoleMVC.Rendering;

/// <summary>
/// The framework's <see cref="ILiveDisplay"/>: draws live elements with the application's <see cref="IMarkupRenderer"/>.
/// On a terminal the element is redrawn in place; when the output cannot be rewritten (a pipe or a file) it is drawn once,
/// in its final state, when the region ends.
/// </summary>
/// <example>
/// <code>
/// var display = new LiveDisplay(MarkupRenderer.CreateForConsole());
/// using (var region = display.Start(new SpinnerElement { Label = "Working" }))
///     Thread.Sleep(1000);
/// </code>
/// </example>
public sealed class LiveDisplay : ILiveDisplay
{
    /// <summary>How often a live element is redrawn unless another interval is given: 100 ms.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The shortest interval accepted: 20 ms.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>The longest interval accepted: one minute.</summary>
    public static readonly TimeSpan MaximumInterval = TimeSpan.FromMinutes(1);

    private readonly IMarkupRenderer _renderer;
    private readonly TimeProvider _time;

    /// <summary>
    /// Creates a live display.
    /// </summary>
    /// <param name="renderer">The renderer to draw with.</param>
    /// <param name="time">The clock driving the redraws; <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderer"/> is <see langword="null"/>.</exception>
    /// <example><code>var display = new LiveDisplay(renderer, TimeProvider.System);</code></example>
    public LiveDisplay(IMarkupRenderer renderer, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public LiveRegion Start(Element element, TimeSpan? interval = null)
    {
        ArgumentNullException.ThrowIfNull(element);

        var every = interval ?? DefaultInterval;
        if (every < MinimumInterval || every > MaximumInterval)
            throw new ArgumentOutOfRangeException(nameof(interval), every,
                $"A live element is redrawn every {MinimumInterval.TotalMilliseconds} ms to {MaximumInterval.TotalMinutes} minute.");

        return new LiveRegion(_renderer, element, every, _time);
    }
}
