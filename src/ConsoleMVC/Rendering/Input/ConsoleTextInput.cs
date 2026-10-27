namespace ConsoleMVC.Rendering;

/// <summary>
/// Reads input from <see cref="Console.In"/>. Masked reading intercepts keys so typed characters are never
/// echoed; when input is redirected (keys cannot be intercepted) it falls back to <see cref="Console.ReadLine"/>.
/// </summary>
/// <example><code>var password = ConsoleTextInput.Instance.ReadMasked('*');</code></example>
public sealed class ConsoleTextInput : ITextInput
{
    private ConsoleTextInput()
    {
    }

    /// <summary>Gets the shared instance.</summary>
    public static ConsoleTextInput Instance { get; } = new();

    /// <inheritdoc />
    public string? ReadLine() => Console.ReadLine();

    /// <inheritdoc />
    /// <remarks>
    /// Supports Backspace; Ctrl+D or Ctrl+Z on an empty line ends the input. See <see cref="MaskedLineEditor"/>.
    /// </remarks>
    public string? ReadMasked(char mask)
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine();

        var editor = new MaskedLineEditor(mask, Console.Out);
        string? value;

        while (!editor.Process(Console.ReadKey(intercept: true), out value))
        {
        }

        return value;
    }
}
