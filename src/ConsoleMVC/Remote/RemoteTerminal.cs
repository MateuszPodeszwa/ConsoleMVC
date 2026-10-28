using System.Net;
using System.Text;
using System.Threading.Channels;
using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Settings for a <see cref="RemoteTerminal"/>: its size until the client reports one, its type and colours, and how
/// long it may stay idle.
/// </summary>
/// <example>
/// <code>
/// var options = new RemoteTerminalOptions { Columns = 120, Rows = 40, TerminalType = "xterm-256color" };
/// await using var terminal = new RemoteTerminal(stream, options);
/// </code>
/// </example>
public sealed class RemoteTerminalOptions
{
    /// <summary>The idle time after which a terminal is disconnected unless another is chosen: 30 minutes.</summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>Gets or sets the width in columns until the client reports its own. The default is 80.</summary>
    /// <example><code>options.Columns = 120;</code></example>
    public int Columns { get; set; } = 80;

    /// <summary>Gets or sets the height in rows until the client reports its own. The default is 24.</summary>
    /// <example><code>options.Rows = 40;</code></example>
    public int Rows { get; set; } = 24;

    /// <summary>Gets or sets the terminal type, such as <c>xterm-256color</c>; it chooses the colours when <see cref="Colours"/> is not set.</summary>
    /// <example><code>options.TerminalType = "xterm-256color";</code></example>
    public string? TerminalType { get; set; }

    /// <summary>
    /// Gets or sets the colours used, or <see langword="null"/> (the default) to choose them from <see cref="TerminalType"/>:
    /// none for <c>dumb</c>, true colour for types that name it (<c>*-truecolor</c>, <c>*-direct</c>, kitty, Alacritty,
    /// WezTerm, foot), and the 16 basic colours otherwise.
    /// </summary>
    /// <example><code>options.Colours = ColourSupport.TrueColour;</code></example>
    public ColourSupport? Colours { get; set; }

    /// <summary>
    /// Gets or sets how long the client may send nothing before it is disconnected; <see cref="Timeout.InfiniteTimeSpan"/>
    /// never disconnects it. The default is <see cref="DefaultIdleTimeout"/>.
    /// </summary>
    /// <example><code>options.IdleTimeout = TimeSpan.FromMinutes(5);</code></example>
    public TimeSpan IdleTimeout { get; set; } = DefaultIdleTimeout;

    /// <summary>Gets or sets the client's address, shown in logs and by <see cref="ITerminalSession.RemoteEndPoint"/>.</summary>
    /// <example><code>options.RemoteEndPoint = client.Client.RemoteEndPoint;</code></example>
    public EndPoint? RemoteEndPoint { get; set; }
}

/// <summary>
/// A terminal at the other end of a connection: what the application draws is written to the connection as ANSI text,
/// and what the user types is read from it, so <see cref="MvcApplication.RunSessionAsync(RemoteTerminal, CancellationToken)"/> can run a session on it.
/// <see cref="TerminalServer"/> creates one for every client; create one yourself to serve the application over another
/// transport, such as a WebSocket or an SSH library's channel. See docs/design/remote-sessions.md.
/// </summary>
/// <remarks>
/// The terminal at the other end must be in raw mode (Telnet clients switch to it when the server asks): it sends every
/// key as it is pressed and echoes nothing. The terminal owns the streams and disposes them.
/// </remarks>
/// <example>
/// <code>
/// await using var terminal = new RemoteTerminal(webSocketStream, new RemoteTerminalOptions { Columns = 100, Rows = 30 });
/// webSocket.Resized += (columns, rows) => terminal.Resize(columns, rows);
/// await app.RunSessionAsync(terminal, cancellationToken);
/// </code>
/// </example>
public sealed class RemoteTerminal : IAsyncDisposable
{
    /// <summary>The widest terminal accepted; a wider report is taken as this width.</summary>
    public const int MaxColumns = 1000;

    /// <summary>The tallest terminal accepted; a taller report is taken as this height.</summary>
    public const int MaxRows = 500;

    private readonly Stream _input;
    private readonly Stream _output;
    private readonly TimeSpan _idleTimeout;
    private readonly CancellationTokenSource _disconnected = new();
    private readonly RemoteTextInput _textInput;
    private readonly MarkupRenderer _renderer;
    private Task? _pump;
    private int _columns;
    private int _rows;
    private int _disposed;

    /// <summary>Creates a terminal on one stream that is both read and written, such as a network connection.</summary>
    /// <param name="stream">The connection.</param>
    /// <param name="options">The size, type, colours and idle timeout; <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <example><code>await using var terminal = new RemoteTerminal(tcpClient.GetStream());</code></example>
    public RemoteTerminal(Stream stream, RemoteTerminalOptions? options = null)
        : this(stream, stream, options)
    {
    }

    /// <summary>Creates a terminal that reads keys from one stream and writes the screen to another.</summary>
    /// <param name="input">Where the user's keys arrive, as UTF-8.</param>
    /// <param name="output">Where the screen is written, as UTF-8 with ANSI escape sequences.</param>
    /// <param name="options">The size, type, colours and idle timeout; <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when a stream is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the idle timeout is not positive or infinite.</exception>
    /// <example><code>await using var terminal = new RemoteTerminal(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);</code></example>
    public RemoteTerminal(Stream input, Stream output, RemoteTerminalOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        options ??= new RemoteTerminalOptions();
        if (options.IdleTimeout <= TimeSpan.Zero && options.IdleTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(options), options.IdleTimeout, "The idle timeout must be positive, or Timeout.InfiniteTimeSpan.");

        _input = input;
        _output = output;
        _idleTimeout = options.IdleTimeout;
        _columns = Clamp(options.Columns, MaxColumns);
        _rows = Clamp(options.Rows, MaxRows);
        TerminalType = options.TerminalType;
        RemoteEndPoint = options.RemoteEndPoint;

        // Resize redraws come from the pump while the session writes, so writes are serialised
        Output = TextWriter.Synchronized(new CrLfWriter(new StreamWriter(output, new UTF8Encoding(false), bufferSize: 4096, leaveOpen: true), Disconnect));
        Colours = options.Colours ?? ColoursFor(options.TerminalType);
        _textInput = new RemoteTextInput(Output, Resize, Disconnect);
        _renderer = new MarkupRenderer(new AnsiRenderTarget(Output, Colours), () => Width - 1, () => Height);
    }

    /// <summary>Gets the width in columns.</summary>
    /// <example><code>var columns = terminal.Width;</code></example>
    public int Width => Volatile.Read(ref _columns);

    /// <summary>Gets the height in rows.</summary>
    /// <example><code>var rows = terminal.Height;</code></example>
    public int Height => Volatile.Read(ref _rows);

    /// <summary>Gets the terminal type the client reported, or <see langword="null"/>.</summary>
    /// <example><code>logger.LogInformation("Terminal: {Type}", terminal.TerminalType);</code></example>
    public string? TerminalType { get; }

    /// <summary>Gets the colours the terminal is drawn with.</summary>
    /// <example><code>if (terminal.Colours == ColourSupport.None) { /* monochrome */ }</code></example>
    public ColourSupport Colours { get; }

    /// <summary>Gets the client's address, or <see langword="null"/> when it is not known.</summary>
    /// <example><code>logger.LogInformation("From {Address}", terminal.RemoteEndPoint);</code></example>
    public EndPoint? RemoteEndPoint { get; }

    /// <summary>Gets a token cancelled when the client disconnects, goes idle for too long, or presses Ctrl+C.</summary>
    /// <example><code>terminal.Disconnected.Register(() => logger.LogInformation("Gone"));</code></example>
    public CancellationToken Disconnected => _disconnected.Token;

    /// <summary>Gets the writer to the terminal, which turns line feeds into CR LF and is safe to use from several threads.</summary>
    internal TextWriter Output { get; }

    /// <summary>Gets the input the session reads keys and lines from.</summary>
    internal ITextInput Input => _textInput;

    /// <summary>How long a session waits for the answer to <see cref="AskWindowSize"/> before its first screen.</summary>
    internal static readonly TimeSpan WindowSizeWait = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets whether the terminal was asked for its size (<c>ESC [ 18 t</c>), whose answer the session waits for.</summary>
    internal bool WindowSizeAsked { get; private set; }

    /// <summary>Asks the terminal for its size; <see cref="AwaitWindowSize"/> applies the answer.</summary>
    /// <exception cref="IOException">Thrown when the client has gone.</exception>
    internal void AskWindowSize()
    {
        WindowSizeAsked = true;
        Output.Write("\u001b[18t");
        Output.Flush();
    }

    /// <summary>
    /// Waits briefly for the answer to <see cref="AskWindowSize"/> once the terminal has started, so the first screen is
    /// laid out at the client's size rather than the default 80 by 24.
    /// </summary>
    internal void AwaitWindowSize()
    {
        if (WindowSizeAsked)
            _textInput.AwaitWindowSize(WindowSizeWait);
    }

    /// <summary>Gets the renderer drawing on the terminal with ANSI escape sequences.</summary>
    internal MarkupRenderer Renderer => _renderer;

    /// <summary>Creates a renderer for screen-reader mode: plain text, without colours or cursor movement.</summary>
    internal IMarkupRenderer CreateScreenReaderRenderer() =>
        new ScreenReaderRenderer(new MarkupRenderer(new PlainTextRenderTarget(Output), () => Width - 1, () => Height));

    /// <summary>
    /// Changes the size, as when the user resizes the window; the screen is laid out again when the width changed.
    /// Sizes are kept between 1 and <see cref="MaxColumns"/> columns and 1 and <see cref="MaxRows"/> rows.
    /// </summary>
    /// <param name="columns">The width in columns.</param>
    /// <param name="rows">The height in rows.</param>
    /// <example><code>terminal.Resize(120, 40);</code></example>
    public void Resize(int columns, int rows)
    {
        Volatile.Write(ref _columns, Clamp(columns, MaxColumns));
        Volatile.Write(ref _rows, Clamp(rows, MaxRows));
        try
        {
            _renderer.RedrawIfWidthChanged();
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException)
        {
            // The client has gone; the session ends when it next reads
        }
    }

    /// <summary>Disconnects the client: the session's reads end, and it finishes as when its input ends.</summary>
    /// <example><code>terminal.Disconnect();</code></example>
    public void Disconnect()
    {
        try
        {
            _disconnected.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
        catch (AggregateException)
        {
            // A callback registered on Disconnected failed; the terminal is disconnected all the same
        }

        _textInput.Writer.TryComplete();
    }

    /// <summary>Starts reading the connection into the input; called once when a session starts.</summary>
    internal void Start()
    {
        if (Interlocked.CompareExchange(ref _pump, Task.CompletedTask, null) is null)
            _pump = Task.Run(PumpAsync);
    }

    /// <summary>Reads the connection, decodes UTF-8 and writes the characters into the input until it ends.</summary>
    private async Task PumpAsync()
    {
        var bytes = new byte[4096];
        var chars = new char[bytes.Length + 1];
        var decoder = Encoding.UTF8.GetDecoder();
        var writer = _textInput.Writer;
        try
        {
            while (!_disconnected.IsCancellationRequested)
            {
                int count;
                using (var idle = CancellationTokenSource.CreateLinkedTokenSource(_disconnected.Token))
                {
                    if (_idleTimeout != Timeout.InfiniteTimeSpan)
                        idle.CancelAfter(_idleTimeout);

                    count = await _input.ReadAsync(bytes, idle.Token).ConfigureAwait(false);
                }

                if (count == 0)
                    break;

                var decoded = decoder.GetChars(bytes, 0, count, chars, 0, flush: false);
                for (var i = 0; i < decoded; i++)
                    await writer.WriteAsync(chars[i], _disconnected.Token).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException or ChannelClosedException)
        {
            // Idle for too long, disconnected, or the connection broke: all end the session
        }
        finally
        {
            Disconnect();
        }
    }

    /// <summary>Disconnects the client, waits for the reading to stop and disposes the streams.</summary>
    /// <returns>A task that completes when everything is disposed.</returns>
    /// <example><code>await terminal.DisposeAsync();</code></example>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        Disconnect();
        try
        {
            await Output.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or NotSupportedException or OperationCanceledException)
        {
            // The client has gone
        }

        // Disposing the streams also ends a read that ignores cancellation
        await _input.DisposeAsync().ConfigureAwait(false);
        if (!ReferenceEquals(_input, _output))
            await _output.DisposeAsync().ConfigureAwait(false);

        if (_pump is { } pump)
            await pump.ConfigureAwait(false);

        _renderer.Dispose();
        _disconnected.Dispose();
    }

    /// <summary>The colours for a terminal type.</summary>
    /// <param name="terminalType">The type, such as <c>xterm-256color</c>, or <see langword="null"/>.</param>
    /// <returns>The colours to draw with.</returns>
    internal static ColourSupport ColoursFor(string? terminalType)
    {
        var type = terminalType?.Trim().ToLowerInvariant() ?? "";
        if (type == "dumb")
            return ColourSupport.None;

        string[] trueColour = ["truecolor", "24bit", "direct", "kitty", "alacritty", "wezterm", "foot"];
        return trueColour.Any(type.Contains) ? ColourSupport.TrueColour : ColourSupport.Basic;
    }

    private static int Clamp(int value, int max) => Math.Clamp(value, 1, max);
}
