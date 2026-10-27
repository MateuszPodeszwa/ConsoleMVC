namespace ConsoleMVC.Rendering;

/// <summary>
/// Writes lines as plain text with no styling and treats <see cref="Clear"/> as a no-op. Used when
/// output is redirected to a file or pipe, and handy for tests.
/// </summary>
/// <example>
/// <code>
/// var buffer = new StringWriter();
/// var renderer = new MarkupRenderer(new PlainTextRenderTarget(buffer), () => 80);
/// </code>
/// </example>
public sealed class PlainTextRenderTarget : IRenderTarget
{
    private readonly TextWriter _writer;

    /// <summary>
    /// Creates a plain-text render target.
    /// </summary>
    /// <param name="writer">The writer to send output to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is <see langword="null"/>.</exception>
    public PlainTextRenderTarget(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
    }

    /// <inheritdoc />
    public void Write(RenderedLine line) => _writer.WriteLine(line.ToPlainText());

    /// <inheritdoc />
    public void WriteInline(RenderedLine line) => _writer.Write(line.ToPlainText());

    /// <inheritdoc />
    public void Clear()
    {
        // Redirected output cannot be cleared; deliberately do nothing.
    }

    /// <inheritdoc />
    public void Flush() => _writer.Flush();
}
