namespace ConsoleMVC.Mvc;

/// <summary>
/// Turns the first Ctrl+C (or Ctrl+Break) during a local run into a cancellation, so the application ends cleanly:
/// the screen it waits on is left, services are disposed and the terminal is put back. A second Ctrl+C, for an
/// application stuck in an action that ignores its token, ends the process as usual.
/// </summary>
/// <example>
/// <code>
/// using var interrupt = ConsoleInterrupt.Listen(cancellationToken);
/// await RunLoopAsync(..., interrupt.Token);
/// </code>
/// </example>
internal sealed class ConsoleInterrupt : IDisposable
{
    private readonly CancellationTokenSource _source;
    private int _pressed;

    private ConsoleInterrupt(CancellationToken outer)
    {
        _source = CancellationTokenSource.CreateLinkedTokenSource(outer);
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>Gets the token that is cancelled by Ctrl+C or by the caller's token.</summary>
    public CancellationToken Token => _source.Token;

    /// <summary>Gets whether Ctrl+C was pressed.</summary>
    public bool Pressed => Volatile.Read(ref _pressed) != 0;

    /// <summary>Starts listening for Ctrl+C until disposed.</summary>
    /// <param name="outer">The caller's token, which also cancels <see cref="Token"/>.</param>
    /// <returns>The listener.</returns>
    public static ConsoleInterrupt Listen(CancellationToken outer) => new(outer);

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // The first press stops the run gracefully; a second one is left to end the process
        if (Interlocked.Exchange(ref _pressed, 1) == 1)
            return;

        e.Cancel = true;
        _source.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _source.Dispose();
    }
}
