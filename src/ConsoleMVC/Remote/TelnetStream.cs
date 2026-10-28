using System.Text;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The Telnet protocol (RFC 854) over a connection: reads return only the user's data, with the commands taken out and
/// answered, and the window size (NAWS, RFC 1073) and terminal type (TTYPE, RFC 1091) reported through events. Writes
/// pass through unchanged: UTF-8 never contains the byte 255, so output needs no escaping.
/// See docs/design/remote-sessions.md, section 6.
/// </summary>
/// <example>
/// <code>
/// var telnet = new TelnetStream(network);
/// telnet.WindowSizeChanged += (columns, rows) => terminal.Resize(columns, rows);
/// await telnet.NegotiateAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
/// </code>
/// </example>
internal sealed class TelnetStream : Stream
{
    // Commands (RFC 854)
    private const byte Se = 240;
    private const byte Sb = 250;
    private const byte Will = 251;
    private const byte Wont = 252;
    private const byte Do = 253;
    private const byte Dont = 254;
    private const byte Iac = 255;

    // Options
    private const byte EchoOption = 1;
    private const byte SuppressGoAhead = 3;
    private const byte TerminalTypeOption = 24;
    private const byte WindowSizeOption = 31;

    // TTYPE subnegotiation (RFC 1091)
    private const byte TerminalTypeIs = 0;
    private const byte TerminalTypeSend = 1;

    /// <summary>The most data bytes kept while negotiating; a client that sends more is not waited for any longer.</summary>
    public const int MaxNegotiationBuffer = 4096;

    /// <summary>The longest subnegotiation kept; longer ones are dropped.</summary>
    public const int MaxSubnegotiationLength = 64;

    private enum State
    {
        Data,
        Command,
        Option,
        Subnegotiation,
        SubnegotiationCommand
    }

    private readonly Stream _inner;
    private readonly object _writeLock = new();
    private readonly byte[] _received = new byte[4096];
    private readonly List<byte> _subnegotiation = [];

    /// <summary>User data parsed but not returned yet.</summary>
    private readonly Queue<byte> _data = new();

    private State _state;
    private byte _command;
    private bool _subnegotiationTooLong;

    /// <summary>Wraps a connection.</summary>
    /// <param name="inner">The connection, read and written by this stream from now on.</param>
    public TelnetStream(Stream inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Raised with the columns and rows when the client reports its window size.</summary>
    public event Action<int, int>? WindowSizeChanged;

    /// <summary>Gets the window size last reported, or <see langword="null"/> before the client reported one.</summary>
    public (int Columns, int Rows)? WindowSize { get; private set; }

    /// <summary>Gets the terminal type the client reported, such as <c>XTERM-256COLOR</c>, or <see langword="null"/>.</summary>
    public string? TerminalType { get; private set; }

    /// <summary>
    /// Asks the client to let the server echo and to send characters as they are typed, its window size and its terminal
    /// type, then waits until both are known or <paramref name="timeout"/> passes. Data typed meanwhile is kept for reads.
    /// </summary>
    /// <param name="timeout">How long to wait for the answers.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>A task that completes when the answers arrived or the time passed.</returns>
    /// <example><code>await telnet.NegotiateAsync(TimeSpan.FromMilliseconds(500), token);</code></example>
    public async Task NegotiateAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        WriteCommand(Will, EchoOption);
        WriteCommand(Will, SuppressGoAhead);
        WriteCommand(Do, SuppressGoAhead);
        WriteCommand(Do, WindowSizeOption);
        WriteCommand(Do, TerminalTypeOption);
        await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);

        // A read is never cancelled (that can break a TLS stream): when the time passes it is left for the next read
        var deadline = Task.Delay(timeout, cancellationToken);
        // Data typed meanwhile is kept for reads, up to a limit: a client that floods data is not waited for
        while ((WindowSize is null || TerminalType is null) && _data.Count < MaxNegotiationBuffer)
        {
            _pendingRead ??= _inner.ReadAsync(_received, CancellationToken.None).AsTask();
            if (await Task.WhenAny(_pendingRead, deadline).ConfigureAwait(false) == deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return; // the client did not answer everything in time (many raw TCP clients answer nothing)
            }

            if (!CompletePendingRead(await _pendingRead.ConfigureAwait(false)))
                return;
        }
    }

    /// <summary>Gets how many data bytes are parsed and waiting to be read.</summary>
    internal int BufferedDataCount => _data.Count;

    /// <summary>A read started while negotiating that had not finished when the time passed.</summary>
    private Task<int>? _pendingRead;

    /// <summary>Parses what the pending read received; <see langword="false"/> when the connection ended.</summary>
    private bool CompletePendingRead(int count)
    {
        _pendingRead = null;
        if (count == 0)
            return false;

        Parse(_received.AsSpan(0, count));
        return true;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        while (_data.Count == 0)
        {
            var count = _pendingRead is { } pending ? pending.GetAwaiter().GetResult() : _inner.Read(_received, 0, _received.Length);
            if (!CompletePendingRead(count))
                return 0;
        }

        return Take(buffer);
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (_data.Count == 0)
        {
            var count = _pendingRead is { } pending
                ? await pending.WaitAsync(cancellationToken).ConfigureAwait(false)
                : await _inner.ReadAsync(_received, cancellationToken).ConfigureAwait(false);
            if (!CompletePendingRead(count))
                return 0;
        }

        return Take(buffer.Span);
    }

    /// <summary>Copies parsed data into <paramref name="buffer"/>.</summary>
    private int Take(Span<byte> buffer)
    {
        var taken = 0;
        while (taken < buffer.Length && _data.TryDequeue(out var b))
            buffer[taken++] = b;
        return taken;
    }

    /// <summary>Runs the received bytes through the protocol: data is queued, commands are answered.</summary>
    /// <param name="bytes">The bytes received.</param>
    internal void Parse(ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            switch (_state)
            {
                case State.Data:
                    if (b == Iac)
                        _state = State.Command;
                    else
                        _data.Enqueue(b);
                    break;

                case State.Command:
                    switch (b)
                    {
                        case Iac:
                            _data.Enqueue(Iac); // IAC IAC is the byte 255
                            _state = State.Data;
                            break;
                        case Will or Wont or Do or Dont:
                            _command = b;
                            _state = State.Option;
                            break;
                        case Sb:
                            _subnegotiation.Clear();
                            _subnegotiationTooLong = false;
                            _state = State.Subnegotiation;
                            break;
                        default:
                            _state = State.Data; // NOP, go ahead, and the other two-byte commands
                            break;
                    }

                    break;

                case State.Option:
                    Answer(_command, b);
                    _state = State.Data;
                    break;

                case State.Subnegotiation:
                    if (b == Iac)
                        _state = State.SubnegotiationCommand;
                    else
                        AddToSubnegotiation(b);
                    break;

                case State.SubnegotiationCommand:
                    if (b == Se)
                    {
                        if (!_subnegotiationTooLong)
                            Subnegotiated(_subnegotiation);
                        _state = State.Data;
                    }
                    else
                    {
                        AddToSubnegotiation(b); // IAC IAC inside a subnegotiation is the byte 255
                        _state = State.Subnegotiation;
                    }

                    break;
            }
        }
    }

    private void AddToSubnegotiation(byte b)
    {
        if (_subnegotiation.Count < MaxSubnegotiationLength)
            _subnegotiation.Add(b);
        else
            _subnegotiationTooLong = true;
    }

    /// <summary>Answers WILL, WONT, DO and DONT: the options offered are kept, any other is refused.</summary>
    private void Answer(byte command, byte option)
    {
        switch (command)
        {
            case Will when option == TerminalTypeOption:
                WriteSubnegotiation(TerminalTypeOption, TerminalTypeSend);
                break;
            case Will when option != WindowSizeOption:
                WriteCommand(Dont, option);
                break;
            case Do when option is not (EchoOption or SuppressGoAhead):
                WriteCommand(Wont, option);
                break;
            // WONT and DONT are never answered, so the two sides cannot loop
        }
    }

    /// <summary>Handles a complete subnegotiation: the window size or the terminal type.</summary>
    private void Subnegotiated(List<byte> data)
    {
        if (data.Count == 5 && data[0] == WindowSizeOption)
        {
            var columns = (data[1] << 8) | data[2];
            var rows = (data[3] << 8) | data[4];
            WindowSize = (columns, rows);
            WindowSizeChanged?.Invoke(columns, rows);
        }
        else if (data.Count >= 2 && data[0] == TerminalTypeOption && data[1] == TerminalTypeIs)
        {
            var name = Encoding.ASCII.GetString(data.Skip(2).Where(b => b is >= 0x21 and <= 0x7e).ToArray());
            TerminalType = name.Length == 0 ? "UNKNOWN" : name;
        }
    }

    private void WriteCommand(byte command, byte option) => WriteRaw([Iac, command, option]);

    private void WriteSubnegotiation(byte option, byte value) => WriteRaw([Iac, Sb, option, value, Iac, Se]);

    private void WriteRaw(byte[] bytes)
    {
        lock (_writeLock)
        {
            _inner.Write(bytes);
            _inner.Flush();
        }
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_writeLock)
            _inner.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        lock (_writeLock)
            _inner.Write(buffer);
    }

    /// <inheritdoc />
    public override void Flush()
    {
        lock (_writeLock)
            _inner.Flush();
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();

            // A read left from negotiating fails when the connection closes; nobody waits for it any more
            _pendingRead?.ContinueWith(static read => _ = read.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        base.Dispose(disposing);
    }
}
