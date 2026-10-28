using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Keeps the terminal in a mode for the length of a run, such as the alternate screen
/// (<see cref="MvcApplicationBuilder.FullScreen"/>) or mouse reporting (<see cref="MvcApplicationBuilder.Mouse"/>), and
/// makes sure the terminal is put back exactly once: when the run ends, when it fails, or when the process is stopped
/// with Ctrl+C.
/// </summary>
internal sealed class TerminalModeSession : IDisposable
{
    private readonly Func<bool, bool> _setMode;
    private readonly bool _console;
    private int _left;

    private TerminalModeSession(Func<bool, bool> setMode, bool console)
    {
        _setMode = setMode;
        _console = console;
        if (!console)
            return;

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>Switches the mode on; returns <see langword="null"/> when the output cannot.</summary>
    /// <param name="setMode">Turns the mode on (<see langword="true"/>) or off, and says whether it could.</param>
    /// <param name="console">
    /// Whether the terminal is this process's console, which is also put back on Ctrl+C and at exit. A remote terminal
    /// is put back by its own session only.
    /// </param>
    public static TerminalModeSession? Enter(Func<bool, bool> setMode, bool console = true) =>
        setMode(true) ? new TerminalModeSession(setMode, console) : null;

    /// <summary>Switches to the alternate screen; returns <see langword="null"/> when the output cannot.</summary>
    public static TerminalModeSession? FullScreen(IMarkupRenderer renderer, bool console = true) => Enter(renderer.SetFullScreen, console);

    /// <summary>Turns mouse reporting on; returns <see langword="null"/> when the output cannot.</summary>
    public static TerminalModeSession? Mouse(IMarkupRenderer renderer, bool console = true) => Enter(renderer.SetMouseReporting, console);

    /// <summary>Switches the mode off, once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _left, 1) == 1)
            return;

        if (_console)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            Console.CancelKeyPress -= OnCancelKeyPress;
        }

        try
        {
            _setMode(false);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The terminal has gone; there is nothing to put back
        }
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();

    // Not cancelled: Ctrl+C still stops the application, with the terminal put back
    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) => Dispose();
}
