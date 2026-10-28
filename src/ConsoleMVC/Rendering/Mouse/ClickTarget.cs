namespace ConsoleMVC.Rendering;

/// <summary>
/// What clicking a line of an element means, as the keys that would have the same effect: a menu option types its key
/// and Enter, a choice moves to itself and presses Enter. Elements register their targets with
/// <see cref="IMarkupRenderer.MapClicks"/> after drawing; <c>builder.Mouse</c> turns clicks into these keys.
/// </summary>
/// <param name="Line">The line of the element, counted from 0 at its first line.</param>
/// <param name="Keys">The keys a click on that line stands for.</param>
/// <example><code>renderer.MapClicks([ClickTarget.Typing(2, "q")]); // clicking the third line answers "q"</code></example>
public sealed record ClickTarget(int Line, IReadOnlyList<ConsoleKeyInfo> Keys)
{
    /// <summary>
    /// Gets the first column of the target, counted from 0; with <see cref="ToColumn"/> it lets several targets share a
    /// line, as the choices of a side-by-side <c>&lt;radio&gt;</c> do. The default, 0, starts the target at the left.
    /// </summary>
    /// <example><code>new ClickTarget(0, keys) { FromColumn = 10, ToColumn = 18 }</code></example>
    public int FromColumn { get; init; }

    /// <summary>
    /// Gets the column after the last one of the target; the default, <see cref="int.MaxValue"/>, is the whole line from
    /// <see cref="FromColumn"/>.
    /// </summary>
    /// <example><code>new ClickTarget(0, keys) { FromColumn = 10, ToColumn = 18 } // columns 10 to 17</code></example>
    public int ToColumn { get; init; } = int.MaxValue;

    /// <summary>Whether a click at <paramref name="column"/> is on this target.</summary>
    /// <param name="column">The column clicked, from 0.</param>
    /// <returns><see langword="true"/> when the column is within the target.</returns>
    /// <example><code>if (target.Covers(12)) { /* clicked */ }</code></example>
    public bool Covers(int column) => column >= FromColumn && column < ToColumn;

    /// <summary>
    /// A target that answers a typed prompt with <paramref name="text"/>: it erases what was typed so far
    /// (<see cref="ConsoleKey.Clear"/>), types the text and presses Enter.
    /// </summary>
    /// <param name="line">The line of the element.</param>
    /// <param name="text">The answer, e.g. a menu option's key.</param>
    /// <returns>The target.</returns>
    /// <example><code>ClickTarget.Typing(0, "1");</code></example>
    public static ClickTarget Typing(int line, string text) =>
        new(line, [Key(ConsoleKey.Clear), ..text.Select(c => new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false)), Key(ConsoleKey.Enter)]);

    /// <summary>Returns the key information for a special key, as a terminal reports it.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The key, with its character for Enter, Space and Escape.</returns>
    /// <example><code>var space = ClickTarget.Key(ConsoleKey.Spacebar);</code></example>
    public static ConsoleKeyInfo Key(ConsoleKey key) => new(key switch
    {
        ConsoleKey.Enter => '\r',
        ConsoleKey.Spacebar => ' ',
        ConsoleKey.Escape => '\u001b',
        _ => '\0'
    }, key, false, false, false);
}
