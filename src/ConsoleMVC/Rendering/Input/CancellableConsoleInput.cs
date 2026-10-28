namespace ConsoleMVC.Rendering;

/// <summary>
/// Wraps the console's input so a run that is cancelled (Ctrl+C, or the token passed to <c>RunAsync</c>) stops while a
/// screen waits for the user, instead of after the next key: before each read it waits for a key in short steps,
/// checking the token in between. Only the console needs this; remote terminals end when their client is disconnected.
/// </summary>
/// <example><code>input = new CancellableConsoleInput(ConsoleTextInput.Instance, token);</code></example>
internal sealed class CancellableConsoleInput(ITextInput inner, CancellationToken cancellation) : ITextInput
{
    /// <summary>How often the token is checked while waiting for a key.</summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(100);

    /// <inheritdoc />
    public bool CanReadKeys => inner.CanReadKeys;

    /// <inheritdoc />
    public bool WaitForInput(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
                return false;

            if (inner.WaitForInput(left < CheckInterval ? left : CheckInterval))
                return true;
        }
    }

    /// <inheritdoc />
    /// <remarks>Waits for the first key cancellably; a line being typed is then read to its end.</remarks>
    public string? ReadLine()
    {
        AwaitKey();
        return inner.ReadLine();
    }

    /// <inheritdoc />
    public string? ReadMasked(char mask)
    {
        AwaitKey();
        return inner.ReadMasked(mask);
    }

    /// <inheritdoc />
    public ConsoleKeyInfo? ReadKey()
    {
        AwaitKey();
        return inner.ReadKey();
    }

    /// <summary>Waits until a key can be read, or throws <see cref="OperationCanceledException"/> once the run is cancelled.</summary>
    private void AwaitKey()
    {
        while (!inner.WaitForInput(CheckInterval))
            cancellation.ThrowIfCancellationRequested();

        cancellation.ThrowIfCancellationRequested();
    }
}
