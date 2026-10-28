using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Serves a ConsoleMVC application over TCP: every client gets its own session
/// (<see cref="MvcApplication.RunSessionAsync(RemoteTerminal, CancellationToken)"/>) with its own screen, input, history and <see cref="ITerminalSession"/>,
/// all in one process. Clients connect with Telnet by default, optionally over TLS. See docs/design/remote-sessions.md.
/// </summary>
/// <remarks>
/// <para>
/// Secure by default: the server listens on the loopback address, refuses to listen on a network address without a
/// <see cref="TerminalServerOptions.Certificate"/> (unless <see cref="TerminalServerOptions.AllowUnencryptedNetworkAccess"/>
/// is set), limits the number of sessions, the input buffered and the window size, and disconnects idle clients.
/// </para>
/// <para>
/// It does not authenticate users: sign them in with a screen of your own (keeping who signed in in
/// <see cref="ITerminalSession.Items"/>), or let OpenSSH authenticate them and forward to the loopback address.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var server = new TerminalServer(app, new TerminalServerOptions());
/// server.Start();
/// Console.WriteLine($"telnet {server.LocalEndPoint}");
/// await shutdown.Task;
/// await server.StopAsync();
/// </code>
/// </example>
public sealed class TerminalServer : IAsyncDisposable
{
    /// <summary>How long a Telnet client has to report its window size and terminal type: 500 ms.</summary>
    public static readonly TimeSpan NegotiationTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>How long <see cref="StopAsync"/> lets sessions end by themselves before it closes their connections: 5 seconds.</summary>
    public static readonly TimeSpan StopGracePeriod = TimeSpan.FromSeconds(5);

    /// <summary>How long the server waits after accepting a connection failed (for example, out of file handles): 100 ms.</summary>
    private static readonly TimeSpan AcceptRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly MvcApplication _app;
    private readonly TerminalServerOptions _options;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<Task, bool> _sessions = new();

    /// <summary>The connections being served, closed by <see cref="StopAsync"/> when their sessions do not end in time.</summary>
    private readonly ConcurrentDictionary<TcpClient, bool> _clients = new();
    private TcpListener? _listener;
    private Task? _accepting;
    private int _sessionCount;
    private int _stopped;

    /// <summary>Creates a server for <paramref name="app"/>; call <see cref="Start"/> to listen.</summary>
    /// <param name="app">The application to serve.</param>
    /// <param name="options">Where to listen, the protocol, TLS and limits.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <example><code>var server = new TerminalServer(app, new TerminalServerOptions { EndPoint = new IPEndPoint(IPAddress.Loopback, 0) });</code></example>
    public TerminalServer(MvcApplication app, TerminalServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);
        _app = app;
        _options = options;
        _logger = app.Services.GetRequiredService<ILogger<TerminalServer>>();
    }

    /// <summary>Gets the address the server listens on (with the port chosen when port 0 was asked for).</summary>
    /// <exception cref="InvalidOperationException">Thrown before <see cref="Start"/>.</exception>
    /// <example><code>Console.WriteLine($"Connect with: telnet {server.LocalEndPoint}");</code></example>
    public IPEndPoint LocalEndPoint =>
        (IPEndPoint?)_listener?.LocalEndpoint ?? throw new InvalidOperationException("The server has not been started.");

    /// <summary>Gets how many sessions are running.</summary>
    /// <example><code>logger.LogInformation("{Count} users connected", server.SessionCount);</code></example>
    public int SessionCount => Volatile.Read(ref _sessionCount);

    /// <summary>Starts listening and accepting clients in the background.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the server was already started or stopped, or the options are not secure enough.</exception>
    /// <exception cref="SocketException">Thrown when the address cannot be listened on (for example, the port is in use).</exception>
    /// <example><code>server.Start();</code></example>
    public void Start()
    {
        _options.Validate();
        if (_listener is not null)
            throw new InvalidOperationException("The server has already been started.");
        if (Volatile.Read(ref _stopped) == 1)
            throw new InvalidOperationException("The server has been stopped; create a new one to serve again.");

        var listener = new TcpListener(_options.EndPoint);
        listener.Start();
        _listener = listener;
        MvcApplicationLog.Serving(_logger, LocalEndPoint, _options.Protocol, _options.Certificate is null ? "no TLS" : "TLS");
        _accepting = AcceptLoopAsync(listener);
    }

    /// <summary>Stops listening, disconnects every client and waits for their sessions to end.</summary>
    /// <returns>A task that completes when every session has ended.</returns>
    /// <example><code>await server.StopAsync();</code></example>
    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 1)
            return;

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();
        if (_accepting is { } accepting)
            await accepting.ConfigureAwait(false);

        // Cancelling disconnected every client, so sessions waiting for a key end too; a session stuck writing to a client
        // that stopped reading is ended by closing its connection
        var sessions = Task.WhenAll(_sessions.Keys);
        if (await Task.WhenAny(sessions, Task.Delay(StopGracePeriod)).ConfigureAwait(false) != sessions)
        {
            foreach (var client in _clients.Keys)
                client.Client.Close(0);
        }

        await sessions.ConfigureAwait(false);
        if (_listener is not null)
            MvcApplicationLog.StoppedServing(_logger);
    }

    /// <summary>Stops the server (<see cref="StopAsync"/>).</summary>
    /// <returns>A task that completes when every session has ended.</returns>
    /// <example><code>await using var server = new TerminalServer(app, options);</code></example>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _stopping.Dispose();
    }

    /// <summary>Accepts clients until the server stops; each is served on its own task.</summary>
    private async Task AcceptLoopAsync(TcpListener listener)
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException && _stopping.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException)
            {
                // A client that went away while being accepted, or the process is out of handles: wait, then carry on
                await Task.Delay(AcceptRetryDelay).ConfigureAwait(false);
                continue;
            }

            var remote = client.Client.RemoteEndPoint;
            if (!Accepts(remote))
            {
                client.Dispose();
                continue;
            }

            if (Interlocked.Increment(ref _sessionCount) > _options.MaxSessions)
            {
                Interlocked.Decrement(ref _sessionCount);
                MvcApplicationLog.ConnectionRefused(_logger, remote, "the session limit is reached");
                _ = RefuseBusyAsync(client);
                continue;
            }

            Track(ServeClientAsync(client));
        }
    }

    /// <summary>Runs <see cref="TerminalServerOptions.AcceptConnection"/>; a filter that throws refuses the connection.</summary>
    private bool Accepts(EndPoint? remote)
    {
        if (_options.AcceptConnection is not { } accept)
            return true;

        try
        {
            if (accept(remote ?? new IPEndPoint(IPAddress.None, 0)))
                return true;

            MvcApplicationLog.ConnectionRefused(_logger, remote, "not accepted by the filter");
        }
        catch (Exception e)
        {
            MvcApplicationLog.ConnectionFailed(_logger, e, remote);
        }

        return false;
    }

    /// <summary>Keeps <paramref name="session"/> until it ends, so <see cref="StopAsync"/> can wait for it.</summary>
    private void Track(Task session)
    {
        _sessions[session] = true;
        session.ContinueWith(ended => _sessions.TryRemove(ended, out _), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>Tells a client the server is busy, then closes the connection.</summary>
    private async Task RefuseBusyAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                // Without TLS the message can be read; with TLS the client sees the connection close
                if (_options.Certificate is null)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                    timeout.CancelAfter(_options.HandshakeTimeout);
                    await client.GetStream().WriteAsync(Encoding.UTF8.GetBytes(_options.BusyMessage + "\r\n"), timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException
                                          or InvalidOperationException or SocketException)
            {
                // The client went away first
            }
        }
    }

    /// <summary>Sets up the connection (TLS, the protocol, the terminal) and runs the client's session.</summary>
    private async Task ServeClientAsync(TcpClient client)
    {
        var remote = client.Client.RemoteEndPoint;
        RemoteTerminal? terminal = null;
        Task? session = null;
        _clients[client] = true;
        try
        {
            client.NoDelay = true;

            // A blocked write fails after this long, disconnecting a client that stopped reading
            client.SendTimeout = (int)_options.WriteTimeout.TotalMilliseconds;
            Stream stream = client.GetStream();
            if (_options.Certificate is { } certificate)
            {
                var tls = new SslStream(stream, leaveInnerStreamOpen: false);
                stream = tls;
                using var handshake = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                handshake.CancelAfter(_options.HandshakeTimeout);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = certificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateRequired = false,
                    CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck
                }, handshake.Token).ConfigureAwait(false);
            }

            var terminalOptions = new RemoteTerminalOptions { RemoteEndPoint = remote, IdleTimeout = _options.IdleTimeout };
            TelnetStream? telnet = null;
            if (_options.Protocol == TerminalProtocol.Telnet)
            {
                telnet = new TelnetStream(stream);
                stream = telnet;
                await telnet.NegotiateAsync(NegotiationTimeout, _stopping.Token).ConfigureAwait(false);
                if (telnet.WindowSize is var (columns, rows))
                {
                    terminalOptions.Columns = columns;
                    terminalOptions.Rows = rows;
                }

                terminalOptions.TerminalType = telnet.TerminalType;
            }

            terminal = new RemoteTerminal(stream, terminalOptions);
            if (telnet is not null)
                telnet.WindowSizeChanged += terminal.Resize;
            else
                AskWindowSize(terminal);

            session = RunSessionAsync(terminal);
            await session.ConfigureAwait(false);
        }
        catch (Exception e) when (session is null && e is AuthenticationException or IOException or OperationCanceledException or ObjectDisposedException)
        {
            MvcApplicationLog.ConnectionRefused(_logger, remote, e is AuthenticationException or OperationCanceledException ? "the TLS handshake failed or timed out" : "the connection was lost");
        }
        catch (Exception e) when (session is null)
        {
            // Anything else while setting up ends this connection only, and is logged rather than lost
            MvcApplicationLog.ConnectionFailed(_logger, e, remote);
        }
        finally
        {
            try
            {
                if (terminal is not null)
                    await terminal.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                MvcApplicationLog.ConnectionFailed(_logger, e, remote);
            }
            finally
            {
                _clients.TryRemove(client, out _);
                client.Dispose();
                Interlocked.Decrement(ref _sessionCount);
            }
        }
    }

    /// <summary>Runs one session, logging how it ended; an exception ends this session only.</summary>
    private async Task RunSessionAsync(RemoteTerminal terminal)
    {
        var id = "?";
        try
        {
            await _app.RunSessionAsync(terminal, _stopping.Token, started =>
            {
                id = started.Id;
                MvcApplicationLog.SessionStarted(_logger, started.Id, terminal.RemoteEndPoint, terminal.TerminalType, terminal.Width, terminal.Height);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // The server is stopping
        }
        catch (Exception e)
        {
            MvcApplicationLog.SessionFailed(_logger, e, id);
        }
        finally
        {
            MvcApplicationLog.SessionEnded(_logger, id);
        }
    }

    /// <summary>Asks a raw terminal for its size (<c>ESC [ 18 t</c>); the answer is read as input and resizes the terminal.</summary>
    private static void AskWindowSize(RemoteTerminal terminal)
    {
        try
        {
            terminal.AskWindowSize();
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // The client has gone; the session ends at once
        }
    }
}
