using ConsoleMVC.Rendering;

namespace ConsoleMVC.Testing;

/// <summary>
/// Scripted user input: typed lines and single key presses, read in order, then the end of input (which makes menus
/// quit, so a run always finishes). <see cref="CanReadKeys"/> is <see langword="true"/> exactly when the next step is
/// a key, so <c>&lt;select&gt;</c>, <c>&lt;radio&gt;</c> and <c>&lt;checkbox&gt;</c> are answered with the keys or a
/// typed line just as the script says.
/// </summary>
/// <example>
/// <code>
/// var input = new ScriptedInput("2", "Ada")                  // a menu choice, then a name
///     .Keys(ConsoleKey.DownArrow, ConsoleKey.Enter)          // a &lt;select&gt; with the arrow keys
///     .Key(ScriptedInput.Char('y'))                          // a &lt;checkbox&gt;
///     .Line("q");
/// var dashboard = new ScriptedInput().Wait().Wait().Line("q"); // a screen that refreshes twice, then quits
/// </code>
/// </example>
public sealed class ScriptedInput : ITextInput
{
    private readonly Queue<Step> _steps = new();

    /// <summary>
    /// Creates a script of typed lines (more steps can be added with <see cref="Line"/>, <see cref="Keys"/> and
    /// <see cref="Key"/>).
    /// </summary>
    /// <param name="lines">The lines, in order, without line breaks.</param>
    /// <example><code>var input = new ScriptedInput("1", "Ada", "q");</code></example>
    public ScriptedInput(params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        Lines(lines);
    }

    /// <summary>Gets how many steps have not been read yet.</summary>
    /// <example><code>Assert.Equal(0, input.Remaining);</code></example>
    public int Remaining => _steps.Count;

    /// <summary>Gets whether the next step is a key press.</summary>
    public bool CanReadKeys => _steps.TryPeek(out var step) && step.Key is not null;

    /// <summary>
    /// Adds a typed line.
    /// </summary>
    /// <param name="text">The line, without a line break.</param>
    /// <returns>This script, for chaining.</returns>
    /// <example><code>input.Line("Ada");</code></example>
    public ScriptedInput Line(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _steps.Enqueue(new Step(text, null));
        return this;
    }

    /// <summary>
    /// Adds several typed lines.
    /// </summary>
    /// <param name="lines">The lines, in order.</param>
    /// <returns>This script, for chaining.</returns>
    /// <example><code>input.Lines("Ada", "42");</code></example>
    public ScriptedInput Lines(params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (var line in lines)
            Line(line);
        return this;
    }

    /// <summary>
    /// Adds special keys such as the arrows, Space, Enter, Home or End.
    /// </summary>
    /// <param name="keys">The keys, in order.</param>
    /// <returns>This script, for chaining.</returns>
    /// <example><code>input.Keys(ConsoleKey.DownArrow, ConsoleKey.Spacebar, ConsoleKey.Enter);</code></example>
    public ScriptedInput Keys(params ConsoleKey[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
            Key(KeyOf(key));
        return this;
    }

    /// <summary>
    /// Adds one key press, for example a typed character from <see cref="Char"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>This script, for chaining.</returns>
    /// <example><code>input.Key(ScriptedInput.Char('2'));</code></example>
    public ScriptedInput Key(ConsoleKeyInfo key)
    {
        _steps.Enqueue(new Step(null, key));
        return this;
    }

    /// <summary>
    /// Adds a left click at a place in the window, counted from 0 at the top left, as a terminal reports it. With
    /// <c>builder.Mouse</c> on, clicking a menu option, a choice or a checkbox answers it.
    /// </summary>
    /// <param name="column">The column, from 0.</param>
    /// <param name="row">The row, from 0 at the top of the screen.</param>
    /// <returns>This script, for chaining.</returns>
    /// <example><code>input.Click(2, 5); // the option on the sixth row</code></example>
    public ScriptedInput Click(int column, int row) => Report(0, column, row);

    /// <summary>Adds a turn of the mouse wheel, which scrolls like the Up or Down arrow.</summary>
    /// <param name="down"><see langword="true"/> to scroll down.</param>
    /// <returns>This script, for chaining.</returns>
    /// <example><code>input.Wheel(down: true);</code></example>
    public ScriptedInput Wheel(bool down) => Report(down ? 65 : 64, 0, 0);

    /// <summary>Adds the keys of an SGR mouse report: ESC [ &lt; button ; column ; row M, then the release.</summary>
    private ScriptedInput Report(int button, int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfNegative(row);

        Key(KeyOf(ConsoleKey.Escape));
        foreach (var c in $"[<{button};{column + 1};{row + 1}M")
            Key(new ConsoleKeyInfo(c, ConsoleKey.NoName, shift: false, alt: false, control: false));
        return this;
    }

    /// <summary>
    /// Adds a pause in which the user types nothing, long enough for a screen with <c>@refresh</c> (or
    /// <see cref="Mvc.ViewResult.RefreshEvery"/>) to be drawn again. The screen's <see cref="WaitForInput"/> uses it up.
    /// </summary>
    /// <returns>This script, for chaining.</returns>
    /// <example><code>input.Wait().Wait().Line("q"); // two refreshes, then a menu choice</code></example>
    public ScriptedInput Wait()
    {
        _steps.Enqueue(new Step(null, null));
        return this;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns <see langword="false"/> and uses up the next step when it is a <see cref="Wait"/>; otherwise
    /// <see langword="true"/>, at once.
    /// </remarks>
    public bool WaitForInput(TimeSpan timeout)
    {
        if (_steps.TryPeek(out var step) && step.IsWait)
        {
            _steps.Dequeue();
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns the key information a terminal reports for a special key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The key with its character: <c>'\r'</c> for Enter, <c>' '</c> for Space, none for the arrows.</returns>
    /// <example><code>var enter = ScriptedInput.KeyOf(ConsoleKey.Enter);</code></example>
    public static ConsoleKeyInfo KeyOf(ConsoleKey key)
    {
        var character = key switch
        {
            ConsoleKey.Enter => '\r',
            ConsoleKey.Spacebar => ' ',
            ConsoleKey.Escape => '\u001b',
            ConsoleKey.Tab => '\t',
            ConsoleKey.Backspace => '\b',
            _ => '\0'
        };

        return new ConsoleKeyInfo(character, key, shift: false, alt: false, control: false);
    }

    /// <summary>
    /// Returns the key information for a typed character, such as a digit that jumps to a choice or <c>y</c>.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>The key press.</returns>
    /// <example><code>var two = ScriptedInput.Char('2');</code></example>
    public static ConsoleKeyInfo Char(char character)
    {
        var key = character switch
        {
            >= '0' and <= '9' => ConsoleKey.D0 + (character - '0'),
            >= 'a' and <= 'z' => ConsoleKey.A + (character - 'a'),
            >= 'A' and <= 'Z' => ConsoleKey.A + (character - 'A'),
            ' ' => ConsoleKey.Spacebar,
            _ => ConsoleKey.NoName
        };

        return new ConsoleKeyInfo(character, key, shift: char.IsUpper(character), alt: false, control: false);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the next step is a key, not a line.</exception>
    public string? ReadLine()
    {
        if (!_steps.TryPeek(out var step))
            return null;

        ThrowIfWait(step, "a line");
        if (step.Line is null)
            throw new InvalidOperationException(
                $"The application read a line, but the next step of the script is the key {Describe(step.Key!.Value)}. " +
                "Use Line(...) for text fields and menus, Keys(...) for arrow-key fields.");

        _steps.Dequeue();
        return step.Line;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the next step is a line, not a key.</exception>
    public string? ReadMasked(char mask) => ReadLine();

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when the next step is a line, not a key.</exception>
    public ConsoleKeyInfo? ReadKey()
    {
        if (!_steps.TryPeek(out var step))
            return null;

        ThrowIfWait(step, "a key");
        if (step.Key is null)
            throw new InvalidOperationException(
                $"The application read a key, but the next step of the script is the line '{step.Line}'. " +
                "Use Keys(...) or Key(...) for arrow-key fields.");

        _steps.Dequeue();
        return step.Key;
    }

    private static void ThrowIfWait(Step step, string what)
    {
        if (step.IsWait)
            throw new InvalidOperationException(
                $"The application read {what}, but the next step of the script is Wait(), which only a screen that " +
                "refreshes (@refresh or RefreshEvery) uses up.");
    }

    private static string Describe(ConsoleKeyInfo key) =>
        key.KeyChar is >= ' ' and not '\u007f' ? $"'{key.KeyChar}'" : key.Key.ToString();

    /// <summary>One step: a line, a key, or (neither) a pause for a refresh.</summary>
    private readonly record struct Step(string? Line, ConsoleKeyInfo? Key)
    {
        public bool IsWait => Line is null && Key is null;
    }
}
