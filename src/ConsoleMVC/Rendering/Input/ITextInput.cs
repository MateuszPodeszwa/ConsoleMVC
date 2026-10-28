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
/// ConsoleKeyInfo? key = input.CanReadKeys ? input.ReadKey() : null;
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

    /// <summary>
    /// Gets whether <see cref="ReadKey"/> can read single key presses, so <c>&lt;select&gt;</c>, <c>&lt;radio&gt;</c>
    /// and <c>&lt;checkbox&gt;</c> can be answered with the arrow keys. The default is <see langword="false"/>: those
    /// elements then read a typed answer with <see cref="ReadLine"/>.
    /// </summary>
    /// <example><code>if (input.CanReadKeys) { var key = input.ReadKey(); }</code></example>
    bool CanReadKeys => false;

    /// <summary>
    /// Reads one key press without echoing it. Only called when <see cref="CanReadKeys"/> is <see langword="true"/>.
    /// </summary>
    /// <returns>The key, or <see langword="null"/> at the end of input. The default implementation returns <see langword="null"/>.</returns>
    /// <example><code>ConsoleKeyInfo? key = input.ReadKey();</code></example>
    ConsoleKeyInfo? ReadKey() => null;

    /// <summary>
    /// Waits until the user starts typing, or until <paramref name="timeout"/> passes, without reading anything. A screen
    /// that refreshes on a timer (<c>@refresh</c>) asks this before it reads, and refreshes when nothing arrives in time.
    /// </summary>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>
    /// <see langword="true"/> when input is waiting (or the input cannot tell, so reading should go ahead and block);
    /// <see langword="false"/> when the time passed with nothing typed. The default implementation returns
    /// <see langword="true"/>, so inputs that do not implement it never refresh a screen.
    /// </returns>
    /// <example><code>if (!input.WaitForInput(TimeSpan.FromSeconds(5))) { /* refresh the screen */ }</code></example>
    bool WaitForInput(TimeSpan timeout) => true;
}
