namespace ConsoleMVC.Rendering;

/// <summary>
/// Destination for laid-out lines. Implementations decide how styles are expressed — ANSI escape
/// sequences, the legacy console colour API, or not at all.
/// </summary>
/// <remarks>
/// Implement this interface to render markup somewhere other than the console, for example into a
/// log file, a test buffer or a GUI terminal widget.
/// </remarks>
/// <example>
/// <code>
/// IRenderTarget target = new PlainTextRenderTarget(Console.Out);
/// target.Write(new RenderedLine([new Span("Hello", TextStyle.Plain)]));
/// target.Flush();
/// </code>
/// </example>
public interface IRenderTarget
{
    /// <summary>
    /// Writes one line, followed by a line terminator.
    /// </summary>
    /// <param name="line">The line to write.</param>
    void Write(RenderedLine line);

    /// <summary>
    /// Writes one line without a line terminator, leaving the cursor after it (used for prompts).
    /// </summary>
    /// <param name="line">The text to write.</param>
    void WriteInline(RenderedLine line);

    /// <summary>
    /// Clears the output, if the destination supports it. Must never throw because the output is redirected.
    /// </summary>
    void Clear();

    /// <summary>
    /// Flushes any buffered output.
    /// </summary>
    void Flush();

    /// <summary>
    /// Erases the last <paramref name="count"/> lines written with <see cref="Write"/> and moves the cursor to the start
    /// of the first of them, so they can be written again (used to update <c>&lt;select&gt;</c> as keys are pressed).
    /// The cursor must be at the start of the line after them.
    /// </summary>
    /// <param name="count">The number of lines to erase.</param>
    /// <returns>
    /// <see langword="true"/> when the lines were erased; the default implementation returns <see langword="false"/>
    /// for targets that cannot move the cursor (plain text, files, pipes).
    /// </returns>
    /// <example><code>if (!target.TryEraseLines(3)) target.Clear();</code></example>
    bool TryEraseLines(int count) => false;

    /// <summary>
    /// Switches to the terminal's alternate screen (<paramref name="on"/>), where a full-screen application draws without
    /// touching the user's scrollback, or back to the normal screen and what was on it.
    /// </summary>
    /// <param name="on"><see langword="true"/> to switch to the alternate screen, <see langword="false"/> to switch back.</param>
    /// <returns>
    /// <see langword="true"/> when the target switched; the default implementation returns <see langword="false"/> for
    /// targets without one (plain text, files, the legacy Windows console API).
    /// </returns>
    /// <example><code>if (target.TrySetAlternateScreen(true)) { /* draw, then */ target.TrySetAlternateScreen(false); }</code></example>
    bool TrySetAlternateScreen(bool on) => false;
}
