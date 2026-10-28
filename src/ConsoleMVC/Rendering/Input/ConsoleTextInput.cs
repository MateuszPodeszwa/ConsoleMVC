namespace ConsoleMVC.Rendering;

/// <summary>
/// Reads input from <see cref="Console.In"/>. Masked reading intercepts keys so typed characters are never
/// echoed; when input is redirected (keys cannot be intercepted) it falls back to <see cref="Console.ReadLine"/>.
/// </summary>
/// <example>
/// <code>
/// var password = ConsoleTextInput.Instance.ReadMasked('*');
/// ConsoleKeyInfo? key = ConsoleTextInput.Instance.CanReadKeys ? ConsoleTextInput.Instance.ReadKey() : null;
/// </code>
/// </example>
public sealed class ConsoleTextInput : ITextInput
{
    private ConsoleTextInput()
    {
    }

    /// <summary>Gets the shared instance.</summary>
    public static ConsoleTextInput Instance { get; } = new();

    /// <inheritdoc />
    public string? ReadLine()
    {
        // The runtime echoes what is typed; that is the user's input, not output from a view
        using (ForeignOutputTracker.Suppress())
            return Console.ReadLine();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Supports Backspace; Ctrl+D or Ctrl+Z on an empty line ends the input. See <see cref="MaskedLineEditor"/>.
    /// </remarks>
    public string? ReadMasked(char mask)
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        using (ForeignOutputTracker.Suppress())
        {
            var editor = new MaskedLineEditor(mask, Console.Out);
            string? value;

            while (!editor.Process(Console.ReadKey(intercept: true), out value))
            {
            }

            return value;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see langword="true"/> when both input and output are a terminal: keys can be intercepted and the element can be
    /// redrawn where the user sees it.
    /// </remarks>
    public bool CanReadKeys => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    /// <summary>How often <see cref="WaitForInput"/> checks for a key press.</summary>
    private static readonly TimeSpan KeyPollInterval = TimeSpan.FromMilliseconds(15);

    /// <inheritdoc />
    /// <remarks>
    /// Checks <see cref="Console.KeyAvailable"/> until a key arrives or the time passes. Redirected input cannot be
    /// checked without reading it, so this returns <see langword="true"/> straight away and the screen never refreshes.
    /// </remarks>
    public bool WaitForInput(TimeSpan timeout)
    {
        if (Console.IsInputRedirected)
            return true;

        var deadline = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(Math.Max(timeout.TotalSeconds, 0) * System.Diagnostics.Stopwatch.Frequency);
        try
        {
            while (!Console.KeyAvailable)
            {
                if (System.Diagnostics.Stopwatch.GetTimestamp() >= deadline)
                    return false;

                Thread.Sleep(KeyPollInterval);
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            return true; // no console to ask: read as usual
        }
    }

    /// <inheritdoc />
    /// <remarks>Ctrl+D and Ctrl+Z end the input, as they do for a typed line.</remarks>
    public ConsoleKeyInfo? ReadKey()
    {
        try
        {
            var key = Console.ReadKey(intercept: true);
            var endOfInput = key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key is ConsoleKey.D or ConsoleKey.Z;
            return endOfInput ? null : key;
        }
        catch (InvalidOperationException)
        {
            // Input became redirected or has no console
            return null;
        }
    }
}
