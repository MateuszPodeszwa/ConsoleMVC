using System.Text;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Writes to a remote terminal in raw mode, where a line feed only moves the cursor down: every <c>\n</c> that does not
/// follow a <c>\r</c> is written as <c>\r\n</c>, so lines always start at the left edge.
/// </summary>
/// <remarks>
/// When the connection breaks, <paramref name="broken"/> is called and the write throws
/// <see cref="OperationCanceledException"/>, so a session ends as when the client disconnects rather than with an error.
/// </remarks>
/// <param name="inner">The writer to the terminal.</param>
/// <param name="broken">Called once the connection is found broken; <see langword="null"/> for nothing.</param>
/// <example>
/// <code>
/// var writer = new CrLfWriter(new StreamWriter(stream));
/// writer.Write("one\ntwo"); // "one\r\ntwo"
/// </code>
/// </example>
internal sealed class CrLfWriter(TextWriter inner, Action? broken = null) : TextWriter
{
    private char _last;

    /// <inheritdoc />
    public override Encoding Encoding => inner.Encoding;

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string NewLine
    {
        get => "\r\n";
        set
        {
            // Always CR LF: the terminal needs both
        }
    }

    /// <inheritdoc />
    public override void Write(char value)
    {
        Guard(() =>
        {
            if (value == '\n' && _last != '\r')
                inner.Write('\r');

            inner.Write(value);
        });
        _last = value;
    }

    /// <inheritdoc />
    public override void Write(string? value) => Write(value.AsSpan());

    /// <inheritdoc />
    public override void Write(char[] buffer, int index, int count) => Write(buffer.AsSpan(index, count));

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<char> buffer)
    {
        // Runs of text without a line feed are written in one call
        while (!buffer.IsEmpty)
        {
            var feed = buffer.IndexOf('\n');
            if (feed < 0)
            {
                WriteRun(buffer);
                return;
            }

            if (feed > 0)
                WriteRun(buffer[..feed]);

            Write('\n');
            buffer = buffer[(feed + 1)..];
        }
    }

    /// <summary>Writes text without a line feed.</summary>
    private void WriteRun(ReadOnlySpan<char> run)
    {
        try
        {
            inner.Write(run);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            throw Broken(e);
        }

        _last = run[^1];
    }

    /// <summary>Runs a write, turning a broken connection into the end of the session.</summary>
    private void Guard(Action write)
    {
        try
        {
            write();
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            throw Broken(e);
        }
    }

    private OperationCanceledException Broken(Exception e)
    {
        broken?.Invoke();
        return new OperationCanceledException("The remote terminal disconnected.", e);
    }

    /// <inheritdoc />
    public override void WriteLine() => Write("\r\n");

    /// <inheritdoc />
    public override void WriteLine(string? value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc />
    public override void Flush() => Guard(inner.Flush);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }
}
