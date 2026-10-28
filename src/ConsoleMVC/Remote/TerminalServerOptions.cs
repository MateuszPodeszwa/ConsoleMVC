using System.Globalization;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace ConsoleMVC.Mvc;

/// <summary>How a <see cref="TerminalServer"/> talks to its clients.</summary>
/// <example><code>options.Protocol = TerminalProtocol.Raw;</code></example>
public enum TerminalProtocol
{
    /// <summary>
    /// Telnet (RFC 854): any Telnet client connects (<c>telnet host 2323</c>, PuTTY, <c>ttyd telnet ...</c>), and reports
    /// its window size and terminal type. The default.
    /// </summary>
    Telnet,

    /// <summary>
    /// Bytes as they are, for a client that is already in raw mode, such as
    /// <c>socat -,raw,echo=0 TCP:host:2323</c> or a custom transport. The window size is asked of the terminal itself.
    /// </summary>
    Raw
}

/// <summary>
/// Settings for a <see cref="TerminalServer"/>: where it listens, the protocol, TLS, and the limits that protect it.
/// The defaults are safe: the loopback address only, at most 16 sessions, idle clients dropped after 30 minutes.
/// </summary>
/// <example>
/// <code>
/// var options = new TerminalServerOptions
/// {
///     EndPoint = new IPEndPoint(IPAddress.Any, 2323),
///     Certificate = X509CertificateLoader.LoadPkcs12FromFile("server.pfx", password),
///     MaxSessions = 50
/// };
/// await app.ServeAsync(options, cancellationToken);
/// </code>
/// </example>
public sealed class TerminalServerOptions
{
    /// <summary>The port used when none is given: 2323, the usual unprivileged Telnet port.</summary>
    public const int DefaultPort = 2323;

    /// <summary>The session limit unless another is chosen: 16.</summary>
    public const int DefaultMaxSessions = 16;

    /// <summary>The setting that serves the application instead of running it locally: <c>ConsoleMVC:Serve</c>, such as <c>2323</c> or <c>127.0.0.1:2323</c>.</summary>
    public const string ServeSetting = "ConsoleMVC:Serve";

    /// <summary>The setting that chooses the protocol: <c>ConsoleMVC:ServeProtocol</c>, <c>Telnet</c> or <c>Raw</c>.</summary>
    public const string ProtocolSetting = "ConsoleMVC:ServeProtocol";

    /// <summary>The setting naming a PKCS #12 (<c>.pfx</c>) file whose certificate turns on TLS: <c>ConsoleMVC:ServeCertificate</c>.</summary>
    public const string CertificateSetting = "ConsoleMVC:ServeCertificate";

    /// <summary>The setting holding the certificate file's password: <c>ConsoleMVC:ServeCertificatePassword</c>. Keep it in user secrets or the environment.</summary>
    public const string CertificatePasswordSetting = "ConsoleMVC:ServeCertificatePassword";

    /// <summary>The setting for <see cref="MaxSessions"/>: <c>ConsoleMVC:ServeMaxSessions</c>.</summary>
    public const string MaxSessionsSetting = "ConsoleMVC:ServeMaxSessions";

    /// <summary>The setting for <see cref="AllowUnencryptedNetworkAccess"/>: <c>ConsoleMVC:ServeAllowUnencrypted</c>.</summary>
    public const string AllowUnencryptedSetting = "ConsoleMVC:ServeAllowUnencrypted";

    /// <summary>Gets or sets where the server listens. The default is the loopback address on <see cref="DefaultPort"/>; port 0 picks a free port.</summary>
    /// <example><code>options.EndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 2323);</code></example>
    public IPEndPoint EndPoint { get; set; } = new(IPAddress.Loopback, DefaultPort);

    /// <summary>Gets or sets the protocol. The default is <see cref="TerminalProtocol.Telnet"/>.</summary>
    /// <example><code>options.Protocol = TerminalProtocol.Raw;</code></example>
    public TerminalProtocol Protocol { get; set; } = TerminalProtocol.Telnet;

    /// <summary>
    /// Gets or sets the certificate (with its private key) that turns on TLS 1.2 or 1.3 for every connection, or
    /// <see langword="null"/> for none. Clients then connect with TLS, for example
    /// <c>socat -,raw,echo=0 OPENSSL:host:2323</c> with <see cref="TerminalProtocol.Raw"/>.
    /// </summary>
    /// <example><code>options.Certificate = X509CertificateLoader.LoadPkcs12FromFile("server.pfx", password);</code></example>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// Gets or sets whether the server may listen on an address other than loopback without a <see cref="Certificate"/>.
    /// Off by default, because everything typed, passwords included, would cross the network in clear text; turn it on
    /// only on a network you trust, or behind something that encrypts (an SSH tunnel, a VPN, a TLS proxy).
    /// </summary>
    /// <example><code>options.AllowUnencryptedNetworkAccess = true; // a lab network</code></example>
    public bool AllowUnencryptedNetworkAccess { get; set; }

    /// <summary>Gets or sets the most sessions at once; later clients are told the server is busy. The default is <see cref="DefaultMaxSessions"/>.</summary>
    /// <example><code>options.MaxSessions = 50;</code></example>
    public int MaxSessions { get; set; } = DefaultMaxSessions;

    /// <summary>Gets or sets how long a client may send nothing before it is disconnected. The default is 30 minutes.</summary>
    /// <example><code>options.IdleTimeout = TimeSpan.FromMinutes(10);</code></example>
    public TimeSpan IdleTimeout { get; set; } = RemoteTerminalOptions.DefaultIdleTimeout;

    /// <summary>
    /// Gets or sets how long writing to a client may block before the client is disconnected: a client that stops reading
    /// (its connection full) cannot hold its session, or the server's shutdown, for longer. The default is 30 seconds.
    /// </summary>
    /// <example><code>options.WriteTimeout = TimeSpan.FromSeconds(10);</code></example>
    public TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how long a client has to finish the TLS handshake. The default is 10 seconds.</summary>
    /// <example><code>options.HandshakeTimeout = TimeSpan.FromSeconds(5);</code></example>
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a filter run for every connection before anything is sent: return <see langword="false"/> to close
    /// it, for example to accept only some addresses. <see langword="null"/> (the default) accepts every connection.
    /// </summary>
    /// <example><code>options.AcceptConnection = endPoint => endPoint is IPEndPoint ip &amp;&amp; IPAddress.IsLoopback(ip.Address);</code></example>
    public Func<EndPoint, bool>? AcceptConnection { get; set; }

    /// <summary>Gets or sets the message sent to a client refused because <see cref="MaxSessions"/> are running.</summary>
    /// <example><code>options.BusyMessage = "All terminals are in use; try again shortly.";</code></example>
    public string BusyMessage { get; set; } = "The server is busy. Please try again later.";

    /// <summary>
    /// Reads where to listen: a port (<c>2323</c>, on the loopback address), or an address and port (<c>127.0.0.1:2323</c>,
    /// <c>0.0.0.0:2323</c>, <c>[::1]:2323</c>, <c>localhost:2323</c>).
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The end point.</returns>
    /// <exception cref="FormatException">Thrown when the address is not a port or an address and port.</exception>
    /// <example><code>options.EndPoint = TerminalServerOptions.ParseEndPoint("0.0.0.0:2323");</code></example>
    public static IPEndPoint ParseEndPoint(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var text = address.Trim();

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port <= IPEndPoint.MaxPort)
            return new IPEndPoint(IPAddress.Loopback, port);

        var colon = text.LastIndexOf(':');
        if (colon > 0 && text[..colon] is var host
            && int.TryParse(text[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port) && port <= IPEndPoint.MaxPort)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                return new IPEndPoint(IPAddress.Loopback, port);

            if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
                return new IPEndPoint(ip, port);
        }

        throw new FormatException($"'{address}' is not an address to listen on. Use a port such as '2323', or an address and port such as '127.0.0.1:2323' or '[::1]:2323'.");
    }

    /// <summary>
    /// Reads the options from the <see cref="ServeSetting"/> settings, or returns <see langword="null"/> when
    /// <see cref="ServeSetting"/> is not set.
    /// </summary>
    /// <param name="configuration">The application's configuration.</param>
    /// <returns>The options, or <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a setting is not valid.</exception>
    /// <example><code>var options = TerminalServerOptions.FromConfiguration(app.Configuration);</code></example>
    public static TerminalServerOptions? FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration[ServeSetting] is not { } address || string.IsNullOrWhiteSpace(address))
            return null;

        var options = new TerminalServerOptions();
        try
        {
            options.EndPoint = ParseEndPoint(address);
        }
        catch (FormatException e)
        {
            throw new InvalidOperationException($"The {ServeSetting} setting: {e.Message}", e);
        }

        if (configuration[ProtocolSetting] is { Length: > 0 } protocol)
        {
            if (!Enum.TryParse<TerminalProtocol>(protocol, ignoreCase: true, out var chosen) || !Enum.IsDefined(chosen))
                throw new InvalidOperationException($"The {ProtocolSetting} setting '{protocol}' is not a protocol; use Telnet or Raw.");
            options.Protocol = chosen;
        }

        if (configuration[MaxSessionsSetting] is { Length: > 0 } max)
        {
            if (!int.TryParse(max, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 1)
                throw new InvalidOperationException($"The {MaxSessionsSetting} setting '{max}' is not a positive number.");
            options.MaxSessions = limit;
        }

        if (configuration[AllowUnencryptedSetting] is { Length: > 0 } allow)
        {
            if (!bool.TryParse(allow, out var allowed))
                throw new InvalidOperationException($"The {AllowUnencryptedSetting} setting '{allow}' is not true or false.");
            options.AllowUnencryptedNetworkAccess = allowed;
        }

        if (configuration[CertificateSetting] is { Length: > 0 } path)
        {
            try
            {
                options.Certificate = X509CertificateLoader.LoadPkcs12FromFile(path, configuration[CertificatePasswordSetting]);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
            {
                throw new InvalidOperationException($"The {CertificateSetting} setting: the certificate '{path}' could not be loaded ({e.Message}).", e);
            }
        }

        return options;
    }

    /// <summary>Checks the options before the server starts.</summary>
    /// <exception cref="InvalidOperationException">Thrown when a value is out of range or the server would not be secure.</exception>
    internal void Validate()
    {
        if (EndPoint is null)
            throw new InvalidOperationException("TerminalServerOptions.EndPoint must be set.");

        if (MaxSessions < 1)
            throw new InvalidOperationException("TerminalServerOptions.MaxSessions must be at least 1.");

        if (IdleTimeout <= TimeSpan.Zero && IdleTimeout != Timeout.InfiniteTimeSpan)
            throw new InvalidOperationException("TerminalServerOptions.IdleTimeout must be positive, or Timeout.InfiniteTimeSpan.");

        if (HandshakeTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("TerminalServerOptions.HandshakeTimeout must be positive.");

        if (WriteTimeout <= TimeSpan.Zero || WriteTimeout.TotalMilliseconds > int.MaxValue)
            throw new InvalidOperationException("TerminalServerOptions.WriteTimeout must be positive.");

        if (Certificate is { HasPrivateKey: false })
            throw new InvalidOperationException("TerminalServerOptions.Certificate has no private key, so it cannot be used for TLS.");

        if (Certificate is null && !IPAddress.IsLoopback(EndPoint.Address) && !AllowUnencryptedNetworkAccess)
        {
            throw new InvalidOperationException(
                $"Listening on {EndPoint} would send everything typed, passwords included, across the network in clear text. " +
                "Set TerminalServerOptions.Certificate (or the ConsoleMVC:ServeCertificate setting) to use TLS, listen on the " +
                "loopback address behind SSH or a proxy, or set AllowUnencryptedNetworkAccess on a network you trust.");
        }
    }
}
