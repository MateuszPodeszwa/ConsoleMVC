using System.Threading.Channels;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The input of a <see cref="RemoteTerminal"/>: characters arrive from the connection into a bounded channel and are
/// decoded into keys by <see cref="TerminalKeyDecoder"/>. Lines are edited key by key and echoed to the terminal, which
/// is in raw mode and echoes nothing itself. See docs/design/remote-sessions.md, section 5.
/// </summary>
/// <example>
/// <code>
/// var input = new RemoteTextInput(output, (columns, rows) => terminal.Resize(columns, rows), terminal.Disconnect);
/// await input.Writer.WriteAsync('q');
/// string? line = input.ReadLine();
/// </code>
/// </example>
internal sealed class RemoteTextInput : ITextInput
{
    /// <summary>The most characters waiting to be read; the connection is not read further until some are.</summary>
    public const int BufferSize = 4096;

    private readonly Channel<char> _chars = Channel.CreateBounded<char>(new BoundedChannelOptions(BufferSize)
    {
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });

    private readonly TerminalKeyDecoder _decoder;
    private readonly TextWriter _echo;
    private readonly Action _interrupted;
    private volatile bool _ended;

    /// <summary>Creates the input.</summary>
    /// <param name="echo">Where typed characters are echoed: the terminal.</param>
    /// <param name="windowSize">Called with a window size the terminal reports.</param>
    /// <param name="interrupted">Called when the user presses Ctrl+C, which ends the session.</param>
    public RemoteTextInput(TextWriter echo, Action<int, int> windowSize, Action interrupted)
    {
        _echo = echo;
        _interrupted = interrupted;
        _decoder = new TerminalKeyDecoder(TryRead, windowSize);
    }

    /// <summary>Gets where the connection's characters are written; completed when the connection ends.</summary>
    public ChannelWriter<char> Writer => _chars.Writer;

    /// <inheritdoc />
    public bool CanReadKeys => true;

    /// <inheritdoc />
    public bool WaitForInput(TimeSpan timeout)
    {
        if (_ended || _decoder.HasPending || _chars.Reader.Count > 0)
            return true;

        // True also when the input ended, so the read goes ahead and returns the end
        return Wait(timeout);
    }

    /// <inheritdoc />
    /// <remarks>Ctrl+D and Ctrl+Z end the input, as on the console; Ctrl+C ends the session.</remarks>
    public ConsoleKeyInfo? ReadKey()
    {
        if (_ended)
            return null;

        if (_decoder.Next() is not { } key)
        {
            _ended = true;
            return null;
        }

        if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.C)
        {
            _ended = true;
            _interrupted();
            return null;
        }

        var endOfInput = key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key is ConsoleKey.D or ConsoleKey.Z;
        return endOfInput ? null : key;
    }

    /// <inheritdoc />
    public string? ReadLine()
    {
        var editor = new LineEditor(_echo);
        while (true)
        {
            if (ReadKey() is not { } key)
                return editor.Text.Length == 0 ? null : editor.Text;

            var done = editor.Process(key, out var line);
            _echo.Flush();
            if (done)
                return line;
        }
    }

    /// <inheritdoc />
    public string? ReadMasked(char mask)
    {
        var editor = new MaskedLineEditor(mask, _echo);
        while (true)
        {
            if (ReadKey() is not { } key)
                return null;

            var done = editor.Process(key, out var value);
            _echo.Flush();
            if (done)
                return value;
        }
    }

    /// <summary>The decoder's source: a character from the channel, waiting at most <paramref name="timeout"/>.</summary>
    private bool TryRead(TimeSpan? timeout, out char character)
    {
        while (true)
        {
            if (_chars.Reader.TryRead(out character))
                return true;

            if (_chars.Reader.Completion.IsCompleted)
                return false;

            // Waits once: what arrived in time, or nothing
            if (timeout is { } limit)
                return Wait(limit) && _chars.Reader.TryRead(out character);

            if (!_chars.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
                return false;
        }
    }

    /// <summary>Waits until a character is available or the channel completes; <see langword="false"/> when the time passed.</summary>
    private bool Wait(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            return _chars.Reader.Count > 0 || _chars.Reader.Completion.IsCompleted;

        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            _chars.Reader.WaitToReadAsync(cancellation.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
