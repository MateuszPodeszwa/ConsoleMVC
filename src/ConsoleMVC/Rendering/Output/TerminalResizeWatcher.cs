using System.Runtime.InteropServices;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Calls back shortly after the terminal window is resized. On macOS and Linux it listens for <c>SIGWINCH</c>; on
/// Windows, which has no such signal for console applications, it checks the window width four times a second.
/// </summary>
/// <remarks>
/// Resizing a window produces a burst of events, so the callback runs once the size has been stable for
/// <see cref="SettleDelay"/>, on a thread-pool thread.
/// </remarks>
/// <example><code>using var watcher = TerminalResizeWatcher.Start(() => renderer.RedrawIfWidthChanged());</code></example>
internal sealed class TerminalResizeWatcher : IDisposable
{
    /// <summary>How long the size must stay the same before the callback runs.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(120);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly Action _onResized;
    private readonly Timer _settle;
    private readonly PosixSignalRegistration? _signal;
    private readonly Timer? _poll;
    private int _lastWidth;

    private TerminalResizeWatcher(Action onResized)
    {
        _onResized = onResized;
        _settle = new Timer(_ => Invoke(), null, Timeout.Infinite, Timeout.Infinite);
        _lastWidth = TerminalCapabilities.GetWidth();

        if (OperatingSystem.IsWindows())
        {
            _poll = new Timer(_ => Poll(), null, PollInterval, PollInterval);
        }
        else
        {
            // The handler runs alongside the runtime's own SIGWINCH handling, which refreshes Console.WindowWidth
            _signal = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, _ => Schedule());
        }
    }

    /// <summary>
    /// Starts watching.
    /// </summary>
    /// <param name="onResized">Called after a resize, on a thread-pool thread.</param>
    /// <returns>The watcher, or <see langword="null"/> when the platform cannot report resizes.</returns>
    public static TerminalResizeWatcher? Start(Action onResized)
    {
        ArgumentNullException.ThrowIfNull(onResized);

        try
        {
            return new TerminalResizeWatcher(onResized);
        }
        catch (Exception e) when (e is PlatformNotSupportedException or NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _signal?.Dispose();
        _poll?.Dispose();
        _settle.Dispose();
    }

    private void Schedule()
    {
        try
        {
            _settle.Change(SettleDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
            // Disposed while a resize was being reported
        }
    }

    private void Poll()
    {
        var width = TerminalCapabilities.GetWidth();
        if (Interlocked.Exchange(ref _lastWidth, width) != width)
            Schedule();
    }

    private void Invoke()
    {
        try
        {
            _onResized();
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // The console went away (e.g. the application is closing); there is nothing to redraw
        }
    }
}
