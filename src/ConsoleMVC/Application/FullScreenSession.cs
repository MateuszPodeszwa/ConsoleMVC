using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Keeps the application on the terminal's alternate screen (<see cref="MvcApplicationBuilder.FullScreen"/>) and makes
/// sure the normal screen comes back exactly once: when the run ends, when it fails, or when the process is stopped
/// with Ctrl+C.
/// </summary>
internal sealed class FullScreenSession : IDisposable
{
    private readonly IMarkupRenderer _renderer;
    private int _left;

    private FullScreenSession(IMarkupRenderer renderer)
    {
        _renderer = renderer;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>Switches to the alternate screen; returns <see langword="null"/> when the output cannot.</summary>
    public static FullScreenSession? Enter(IMarkupRenderer renderer) =>
        renderer.SetFullScreen(true) ? new FullScreenSession(renderer) : null;

    /// <summary>Switches back to the normal screen, once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _left, 1) == 1)
            return;

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Console.CancelKeyPress -= OnCancelKeyPress;
        try
        {
            _renderer.SetFullScreen(false);
        }
        catch (IOException)
        {
            // The terminal has gone; there is no screen to restore
        }
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();

    // Not cancelled: Ctrl+C still stops the application, on the normal screen
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) => Dispose();
}
