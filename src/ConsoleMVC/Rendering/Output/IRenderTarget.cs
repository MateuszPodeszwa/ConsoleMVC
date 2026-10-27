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
    /// Clears the output, if the destination supports it. Must never throw because the output is redirected.
    /// </summary>
    void Clear();

    /// <summary>
    /// Flushes any buffered output.
    /// </summary>
    void Flush();
}
