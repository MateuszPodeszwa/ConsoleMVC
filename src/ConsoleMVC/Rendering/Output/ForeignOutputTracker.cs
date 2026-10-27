using System.Text;

namespace ConsoleMVC.Rendering;

/// <summary>
/// Wraps <see cref="Console.Out"/> and tells a <see cref="MarkupRenderer"/> when something other than the renderer
/// writes to the screen (for example <c>Console.WriteLine</c> in a plain C# view). Such output cannot be laid out
/// again, so that screen is not redrawn when the terminal is resized; everything is still written unchanged.
/// </summary>
/// <remarks>
/// The renderer and the console input (which echoes masked characters) write inside <see cref="Suppress"/>, which is
/// per thread, so their own output is never counted as foreign.
/// </remarks>
/// <example>
/// <code>
/// Console.SetOut(new ForeignOutputTracker(Console.Out, renderer));
/// </code>
/// </example>
public sealed class ForeignOutputTracker : TextWriter
{
    [ThreadStatic]
    private static int _suppressed;

    private readonly TextWriter _inner;
    private readonly MarkupRenderer _renderer;

    /// <summary>
    /// Initialises a tracker.
    /// </summary>
    /// <param name="inner">The writer that receives everything.</param>
    /// <param name="renderer">The renderer to tell about foreign output.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public ForeignOutputTracker(TextWriter inner, MarkupRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(renderer);
        _inner = inner;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public override Encoding Encoding => _inner.Encoding;

    /// <summary>
    /// Starts a scope on the current thread in which writes are not counted as foreign.
    /// </summary>
    /// <returns>Dispose it to end the scope.</returns>
    /// <example><code>using (ForeignOutputTracker.Suppress()) Console.Write(echo);</code></example>
    public static IDisposable Suppress()
    {
        _suppressed++;
        return new SuppressScope();
    }

    /// <inheritdoc />
    public override void Write(char value)
    {
        Track();
        _inner.Write(value);
    }

    /// <inheritdoc />
    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            Track();
        _inner.Write(value);
    }

    /// <inheritdoc />
    public override void Write(char[] buffer, int index, int count)
    {
        if (count > 0)
            Track();
        _inner.Write(buffer, index, count);
    }

    /// <inheritdoc />
    public override void WriteLine()
    {
        Track();
        _inner.WriteLine();
    }

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        Track();
        _inner.WriteLine(value);
    }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    private void Track()
    {
        if (_suppressed == 0)
            _renderer.MarkForeignOutput();
    }

    private sealed class SuppressScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _suppressed--;
        }
    }
}
