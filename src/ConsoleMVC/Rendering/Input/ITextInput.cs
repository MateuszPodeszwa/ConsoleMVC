namespace ConsoleMVC.Rendering;

/// <summary>
/// A source of user input lines. Interactive elements (<see cref="MenuElement"/>, <see cref="InputElement"/>)
/// read through this interface so they can be driven by scripted input in tests.
/// </summary>
/// <example>
/// <code>
/// ITextInput input = ConsoleTextInput.Instance;
/// string? name = input.ReadLine();
/// string? password = input.ReadMasked('*');
/// </code>
/// </example>
public interface ITextInput
{
    /// <summary>
    /// Reads one line of input.
    /// </summary>
    /// <returns>The line without its terminator, or <see langword="null"/> at the end of input.</returns>
    string? ReadLine();

    /// <summary>
    /// Reads one line of input without echoing it, showing <paramref name="mask"/> for each character typed.
    /// </summary>
    /// <param name="mask">The character displayed instead of each typed character.</param>
    /// <returns>The line, or <see langword="null"/> at the end of input.</returns>
    string? ReadMasked(char mask);
}
