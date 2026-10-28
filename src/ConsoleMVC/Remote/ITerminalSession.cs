using System.Collections.Concurrent;
using System.Net;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The terminal session a controller, view or service runs in: the local console, or one client of a
/// <see cref="TerminalServer"/>. Resolve it from services (a constructor parameter or <c>[Inject]</c>) to tell users
/// apart and to keep state for one user, such as who signed in. See docs/design/remote-sessions.md, section 4.
/// </summary>
/// <example>
/// <code>
/// public sealed class AccountController(ITerminalSession session) : Controller
/// {
///     public ActionResult Index() =>
///         session.Items.TryGetValue("user", out var user) ? View(user) : RedirectToAction("SignIn");
/// }
/// </code>
/// </example>
public interface ITerminalSession
{
    /// <summary>Gets the session's identifier: <c>local</c> for the console, a new random one for every remote client.</summary>
    /// <example><code>logger.LogInformation("Session {Id}", session.Id);</code></example>
    string Id { get; }

    /// <summary>Gets whether the session is a remote client rather than the local console.</summary>
    /// <example><code>if (session.IsRemote) { /* hide the server's settings */ }</code></example>
    bool IsRemote { get; }

    /// <summary>Gets the client's address, or <see langword="null"/> for the local console.</summary>
    /// <example><code>var address = session.RemoteEndPoint?.ToString() ?? "console";</code></example>
    EndPoint? RemoteEndPoint { get; }

    /// <summary>Gets the terminal type the client reported (such as <c>XTERM-256COLOR</c>), or <see langword="null"/>.</summary>
    /// <example><code>var type = session.TerminalType ?? "unknown";</code></example>
    string? TerminalType { get; }

    /// <summary>Gets the terminal's current width in columns.</summary>
    /// <example><code>var wide = session.Width >= 100;</code></example>
    int Width { get; }

    /// <summary>Gets the terminal's current height in rows.</summary>
    /// <example><code>var rows = session.Height;</code></example>
    int Height { get; }

    /// <summary>
    /// Gets values kept for the length of the session, shared by every screen it shows and by no other session. It is
    /// safe to use from several threads.
    /// </summary>
    /// <example><code>session.Items["user"] = "ada";</code></example>
    IDictionary<string, object?> Items { get; }

    /// <summary>Ends a remote session as if the client had disconnected; does nothing for the local console.</summary>
    /// <example><code>session.Disconnect(); // after too many failed sign-ins</code></example>
    void Disconnect();
}

/// <summary>
/// The framework's <see cref="ITerminalSession"/>, and the session the current code runs in
/// (<see cref="Current"/>, which flows with the async context like <c>FrameworkText.Current</c>).
/// </summary>
internal sealed class TerminalSession : ITerminalSession
{
    private static readonly AsyncLocal<TerminalSession?> CurrentSession = new();

    private readonly RemoteTerminal? _terminal;

    private TerminalSession(string id, RemoteTerminal? terminal)
    {
        Id = id;
        _terminal = terminal;
    }

    /// <summary>Gets the session the current code runs in, or <see langword="null"/> outside any run.</summary>
    public static TerminalSession? Current => CurrentSession.Value;

    /// <summary>Gets the session of the local console, used outside any run and by a local run.</summary>
    public static TerminalSession Local { get; } = new("local", null);

    /// <summary>Creates the session of a remote client, with a new random identifier.</summary>
    /// <param name="terminal">The client's terminal.</param>
    /// <param name="liveDisplay">The live display drawing on the terminal.</param>
    /// <returns>The session.</returns>
    public static TerminalSession ForRemote(RemoteTerminal terminal, Rendering.ILiveDisplay liveDisplay) =>
        new(Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(8)), terminal) { LiveDisplay = liveDisplay };

    /// <summary>Makes <paramref name="session"/> current until the returned scope is disposed.</summary>
    /// <param name="session">The session.</param>
    /// <returns>Restores the previous session when disposed.</returns>
    public static IDisposable Use(TerminalSession session)
    {
        var previous = CurrentSession.Value;
        CurrentSession.Value = session;
        return new Restore(previous);
    }

    /// <summary>Gets the live display drawing on this session's terminal; set for remote sessions.</summary>
    public Rendering.ILiveDisplay? LiveDisplay { get; init; }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public bool IsRemote => _terminal is not null;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => _terminal?.RemoteEndPoint;

    /// <inheritdoc />
    public string? TerminalType => _terminal is null ? System.Environment.GetEnvironmentVariable("TERM") : _terminal.TerminalType;

    /// <inheritdoc />
    public int Width => _terminal?.Width ?? ConsoleSize(static () => Console.WindowWidth, Rendering.TerminalCapabilities.FallbackWidth);

    /// <inheritdoc />
    public int Height => _terminal?.Height ?? ConsoleSize(static () => Console.WindowHeight, 24);

    /// <inheritdoc />
    public IDictionary<string, object?> Items { get; } = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Disconnect() => _terminal?.Disconnect();

    /// <summary>Creates a copy of the local session for one local run, so its <see cref="Items"/> start empty.</summary>
    public static TerminalSession ForLocalRun() => new("local", null);

    /// <summary>A size of the local console, or <paramref name="fallback"/> when there is no console window.</summary>
    private static int ConsoleSize(Func<int> read, int fallback)
    {
        if (Console.IsOutputRedirected)
            return fallback;

        try
        {
            var size = read();
            return size > 0 ? size : fallback;
        }
        catch (Exception e) when (e is IOException or PlatformNotSupportedException)
        {
            return fallback;
        }
    }

    private sealed class Restore(TerminalSession? previous) : IDisposable
    {
        public void Dispose() => CurrentSession.Value = previous;
    }
}
