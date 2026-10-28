using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ConsoleMVC.Mvc;
using Microsoft.Extensions.Configuration;
using static ConsoleMVC.Tests.Remote.RemoteTestSupport;

namespace ConsoleMVC.Tests.Remote;

/// <summary>Tests for <see cref="TerminalServer"/> over real loopback connections (docs/design/remote-sessions.md, section 6).</summary>
public class TerminalServerTests
{
    private const byte Iac = 255, Sb = 250, Se = 240, Will = 251, Naws = 31, Ttype = 24;

    private static TerminalServerOptions Loopback(Action<TerminalServerOptions>? configure = null)
    {
        var options = new TerminalServerOptions { EndPoint = new IPEndPoint(IPAddress.Loopback, 0) };
        configure?.Invoke(options);
        return options;
    }

    /// <summary>Answers the server's Telnet negotiation as a client with the given size and type would.</summary>
    private static async Task AnswerTelnetAsync(Stream stream, int columns, int rows, string type)
    {
        byte[] answer =
        [
            Iac, Will, Naws, Iac, Sb, Naws, (byte)(columns >> 8), (byte)columns, (byte)(rows >> 8), (byte)rows, Iac, Se,
            Iac, Will, Ttype, Iac, Sb, Ttype, 0, .. Encoding.ASCII.GetBytes(type), Iac, Se
        ];
        await stream.WriteAsync(answer);
        await stream.FlushAsync();
    }

    [Fact]
    public async Task ATelnetClient_GetsASession_WithItsSizeAndType()
    {
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback());
        server.Start();

        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        await AnswerTelnetAsync(stream, 100, 30, "XTERM-256COLOR");
        var seen = new StringBuilder();
        await ReadUntilAsync(stream, seen, "visit 1 remote=True width=100 type=XTERM-256COLOR");
        Assert.Equal(1, server.SessionCount);

        await SendAsync(stream, "q\r\n");
        await ReadToEndAsync(stream);
        await EventuallyAsync(() => server.SessionCount == 0, "the session ended");
    }

    [Fact]
    public async Task TwoClients_HaveTheirOwnSessions()
    {
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback());
        server.Start();

        using var first = await ConnectAsync(server);
        using var second = await ConnectAsync(server);
        var (a, b) = (first.GetStream(), second.GetStream());
        var (seenA, seenB) = (new StringBuilder(), new StringBuilder());
        await ReadUntilAsync(a, seenA, "visit 1");
        await ReadUntilAsync(b, seenB, "visit 1");

        await SendAsync(a, "a\r");
        await SendAsync(a, "a\r");
        await ReadUntilAsync(a, seenA, "visit 3");
        await SendAsync(b, "n\r");
        await ReadUntilAsync(b, seenB, "Name: ");
        await SendAsync(b, "Grace\r");
        await ReadUntilAsync(b, seenB, "Hello, Grace!");
        await ReadUntilAsync(b, seenB, "visit 2");

        Assert.DoesNotContain("Grace", seenA.ToString());
        Assert.Equal(2, server.SessionCount);
        var idA = SessionId(seenA.ToString());
        var idB = SessionId(seenB.ToString());
        Assert.NotEqual(idA, idB);
        Assert.Matches("^[0-9a-f]{16}$", idA);
    }

    private static string SessionId(string output) =>
        System.Text.RegularExpressions.Regex.Match(output, "Session ([0-9a-f]+) visit").Groups[1].Value;

    [Fact]
    public async Task ClientsOverTheLimit_AreToldTheServerIsBusy()
    {
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback(o => o.MaxSessions = 1));
        server.Start();

        using var first = await ConnectAsync(server);
        await ReadUntilAsync(first.GetStream(), new StringBuilder(), "visit 1");
        using var second = await ConnectAsync(server);

        var refused = await ReadToEndAsync(second.GetStream());

        Assert.Contains("The server is busy", refused);
        Assert.Equal(1, server.SessionCount);
    }

    [Fact]
    public async Task TheFilter_CanRefuseAConnection()
    {
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback(o => o.AcceptConnection = _ => false));
        server.Start();

        using var client = await ConnectAsync(server);

        Assert.Equal("", await ReadToEndAsync(client.GetStream()));
    }

    [Fact]
    public async Task Stopping_DisconnectsEveryClient()
    {
        await using var app = BuildApp();
        var server = new TerminalServer(app, Loopback());
        server.Start();
        using var client = await ConnectAsync(server);
        await ReadUntilAsync(client.GetStream(), new StringBuilder(), "visit 1");

        await server.StopAsync().WaitAsync(Patience);

        await ReadToEndAsync(client.GetStream());
        Assert.Equal(0, server.SessionCount);
        await server.DisposeAsync();
    }

    [Fact]
    public async Task AClientThatNeverReads_CannotStopTheServerStopping()
    {
        await using var app = BuildApp();
        var server = new TerminalServer(app, Loopback());
        server.Start();
        // A small receive window, set before connecting, so the echo fills the connection quickly
        using var client = new System.Net.Sockets.TcpClient { ReceiveBufferSize = 2048 };
        await client.ConnectAsync(server.LocalEndPoint);

        // Typing is echoed; a client that never reads fills the connection until the server's writes block
        var typing = Task.Run(async () =>
        {
            var keys = Enumerable.Repeat((byte)'n', 64 * 1024).ToArray();
            try
            {
                await SendAsync(client.GetStream(), "n\r");
                for (var i = 0; i < 1024; i++)
                    await client.GetStream().WriteAsync(keys);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // The server closed the connection
            }
        });
        await Task.Delay(3000);

        await server.StopAsync().WaitAsync(Patience);

        Assert.Equal(0, server.SessionCount);
        client.Dispose();
        await typing.WaitAsync(Patience);
        await server.DisposeAsync();
    }

    [Fact]
    public async Task AFilterThatThrows_RefusesThatConnection_AndTheServerCarriesOn()
    {
        var calls = 0;
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback(o => o.AcceptConnection = _ =>
            ++calls == 1 ? throw new InvalidOperationException("filter bug") : true));
        server.Start();

        using var refused = await ConnectAsync(server);
        Assert.Equal("", await ReadToEndAsync(refused.GetStream()));

        using var accepted = await ConnectAsync(server);
        await ReadUntilAsync(accepted.GetStream(), new StringBuilder(), "visit 1");
    }

    [Fact]
    public async Task AStoppedServer_CannotBeStartedAgain()
    {
        await using var app = BuildApp();
        var server = new TerminalServer(app, Loopback());
        await server.StopAsync();

        Assert.Throws<InvalidOperationException>(server.Start);
    }

    [Fact]
    public async Task TheRawProtocol_AsksTheTerminalForItsSize()
    {
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback(o => o.Protocol = TerminalProtocol.Raw));
        server.Start();

        using var client = await ConnectAsync(server);
        var stream = client.GetStream();
        var seen = new StringBuilder();
        await ReadUntilAsync(stream, seen, "\u001b[18t");
        await SendAsync(stream, "\u001b[8;33;120t");
        await SendAsync(stream, "a\r");

        await ReadUntilAsync(stream, seen, "visit 2 remote=True width=120");
        Assert.DoesNotContain('ÿ', seen.ToString()); // no Telnet commands
    }

    [Fact]
    public async Task TLS_EncryptsTheSession()
    {
        using var certificate = SelfSignedCertificate();
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback(o =>
        {
            o.Certificate = certificate;
            o.Protocol = TerminalProtocol.Raw;
        }));
        server.Start();

        using var client = await ConnectAsync(server);
        await using var tls = new SslStream(client.GetStream(), false,
            (_, presented, _, _) => presented?.GetCertHashString() == certificate.GetCertHashString());
        await tls.AuthenticateAsClientAsync("localhost");
        var seen = new StringBuilder();
        await ReadUntilAsync(tls, seen, "visit 1");
        await SendAsync(tls, "q\r");
        await ReadToEndAsync(tls);
    }

    [Fact]
    public async Task AClientThatNeverFinishesTheHandshake_IsDropped()
    {
        using var certificate = SelfSignedCertificate();
        await using var app = BuildApp();
        await using var server = new TerminalServer(app, Loopback(o =>
        {
            o.Certificate = certificate;
            o.HandshakeTimeout = TimeSpan.FromMilliseconds(200);
        }));
        server.Start();

        using var client = await ConnectAsync(server);

        await ReadToEndAsync(client.GetStream());
        await EventuallyAsync(() => server.SessionCount == 0, "the connection was dropped");
    }

    [Fact]
    public async Task ANetworkAddress_NeedsTLS_UnlessAllowed()
    {
        await using var app = BuildApp();
        var options = new TerminalServerOptions { EndPoint = new IPEndPoint(IPAddress.Any, 0) };

        var refused = Assert.Throws<InvalidOperationException>(() => new TerminalServer(app, options).Start());
        Assert.Contains("clear text", refused.Message);

        options.AllowUnencryptedNetworkAccess = true;
        await using var server = new TerminalServer(app, options);
        server.Start();
        Assert.NotEqual(0, server.LocalEndPoint.Port);
    }

    [Fact]
    public async Task InvalidOptions_AreRefused()
    {
        await using var app = BuildApp();

        Assert.Throws<InvalidOperationException>(() => new TerminalServer(app, Loopback(o => o.MaxSessions = 0)).Start());
        Assert.Throws<InvalidOperationException>(() => new TerminalServer(app, Loopback(o => o.IdleTimeout = TimeSpan.Zero)).Start());
        Assert.Throws<InvalidOperationException>(() => _ = new TerminalServer(app, Loopback()).LocalEndPoint);

        await using var started = new TerminalServer(app, Loopback());
        started.Start();
        Assert.Throws<InvalidOperationException>(started.Start);
    }

    [Fact]
    public async Task ServeAsync_ServesUntilCancelled()
    {
        await using var app = BuildApp();
        using var cancellation = new CancellationTokenSource();

        var serving = app.ServeAsync(Loopback(o => o.EndPoint = new IPEndPoint(IPAddress.Loopback, FreePort())), cancellation.Token);
        await Task.Delay(100);
        await cancellation.CancelAsync();

        await serving.WaitAsync(Patience); // stopping is not an error
    }

    [Fact]
    public async Task TheServeSetting_MakesRunAsyncServe()
    {
        var port = FreePort();
        await using var app = BuildApp(builder => builder.Configuration[TerminalServerOptions.ServeSetting] = $"127.0.0.1:{port}");
        using var cancellation = new CancellationTokenSource();

        var running = app.RunAsync(cancellation.Token);
        using var client = new System.Net.Sockets.TcpClient();
        await EventuallyAsync(() =>
        {
            try
            {
                client.Connect(IPAddress.Loopback, port);
                return true;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return false;
            }
        }, "the server listens");
        await ReadUntilAsync(client.GetStream(), new StringBuilder(), "visit 1");

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(Patience));
    }

    [Fact]
    public void Settings_AreRead()
    {
        IConfiguration Settings(params (string Key, string Value)[] values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

        Assert.Null(TerminalServerOptions.FromConfiguration(Settings()));

        var options = TerminalServerOptions.FromConfiguration(Settings(
            (TerminalServerOptions.ServeSetting, "0.0.0.0:2400"),
            (TerminalServerOptions.ProtocolSetting, "raw"),
            (TerminalServerOptions.MaxSessionsSetting, "5"),
            (TerminalServerOptions.AllowUnencryptedSetting, "true")))!;
        Assert.Equal(new IPEndPoint(IPAddress.Any, 2400), options.EndPoint);
        Assert.Equal(TerminalProtocol.Raw, options.Protocol);
        Assert.Equal(5, options.MaxSessions);
        Assert.True(options.AllowUnencryptedNetworkAccess);

        Assert.Throws<InvalidOperationException>(() => TerminalServerOptions.FromConfiguration(Settings((TerminalServerOptions.ServeSetting, "nowhere"))));
        Assert.Throws<InvalidOperationException>(() => TerminalServerOptions.FromConfiguration(Settings((TerminalServerOptions.ServeSetting, "1"), (TerminalServerOptions.ProtocolSetting, "ssh"))));
        Assert.Throws<InvalidOperationException>(() => TerminalServerOptions.FromConfiguration(Settings((TerminalServerOptions.ServeSetting, "1"), (TerminalServerOptions.MaxSessionsSetting, "0"))));
        Assert.Throws<InvalidOperationException>(() => TerminalServerOptions.FromConfiguration(Settings((TerminalServerOptions.ServeSetting, "1"), (TerminalServerOptions.CertificateSetting, "missing.pfx"))));
    }

    [Theory]
    [InlineData("2323", "127.0.0.1:2323")]
    [InlineData(" 127.0.0.1:9000 ", "127.0.0.1:9000")]
    [InlineData("localhost:23", "127.0.0.1:23")]
    [InlineData("[::1]:2323", "[::1]:2323")]
    [InlineData("0.0.0.0:2323", "0.0.0.0:2323")]
    public void EndPoints_AreParsed(string address, string expected) =>
        Assert.Equal(expected, TerminalServerOptions.ParseEndPoint(address).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("70000")]
    [InlineData("host:2323")]
    [InlineData("127.0.0.1:")]
    public void BadEndPoints_AreRefused(string address) =>
        Assert.Throws<FormatException>(() => TerminalServerOptions.ParseEndPoint(address));

    [Fact]
    public async Task TheCertificateSetting_LoadsAPfx()
    {
        var path = Path.Combine(Path.GetTempPath(), $"consolemvc-{Guid.NewGuid():N}.pfx");
        await File.WriteAllBytesAsync(path, SelfSignedPfx("secret"));
        try
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [TerminalServerOptions.ServeSetting] = "2323",
                [TerminalServerOptions.CertificateSetting] = path,
                [TerminalServerOptions.CertificatePasswordSetting] = "secret"
            }).Build();

            using var loaded = TerminalServerOptions.FromConfiguration(settings)!.Certificate;

            Assert.NotNull(loaded);
            Assert.True(loaded.HasPrivateKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A self-signed certificate for localhost, loaded from a PFX as a server certificate would be.</summary>
    private static X509Certificate2 SelfSignedCertificate() => X509CertificateLoader.LoadPkcs12(SelfSignedPfx(null), null);

    /// <summary>
    /// A new self-signed certificate as PFX bytes. Exported straight from the created certificate: a certificate loaded
    /// from a PFX is not exportable by default, and macOS and Windows refuse to export it again.
    /// </summary>
    private static byte[] SelfSignedPfx(string? password)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
        return created.Export(X509ContentType.Pfx, password);
    }

    private static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
