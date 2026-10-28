using ConsoleMVC.Mvc;

namespace ConsoleMVC.Tests.Remote;

/// <summary>Tests for <see cref="TelnetStream"/> (docs/design/remote-sessions.md, section 6).</summary>
public class TelnetStreamTests
{
    private const byte Iac = 255, Sb = 250, Se = 240, Will = 251, Wont = 252, Do = 253, Dont = 254;
    private const byte Naws = 31, Ttype = 24;

    private static (TelnetStream Telnet, MemoryStream Sent) Create()
    {
        var sent = new MemoryStream();
        return (new TelnetStream(sent), sent);
    }

    /// <summary>The data parsed so far; nothing is read from the inner stream while there is some.</summary>
    private static byte[] DataOf(TelnetStream telnet)
    {
        var buffer = new byte[256];
        var count = telnet.Read(buffer);
        return buffer[..count];
    }

    [Fact]
    public void Data_PassesThrough_AndCommandsAreTakenOut()
    {
        var (telnet, _) = Create();

        telnet.Parse([(byte)'h', Iac, 241 /* NOP */, (byte)'i', Iac, Wont, 1, (byte)'!']);

        Assert.Equal("hi!"u8.ToArray(), DataOf(telnet));
    }

    [Fact]
    public void IacIac_IsTheByte255()
    {
        var (telnet, _) = Create();

        telnet.Parse([Iac, Iac]);

        Assert.Equal([255], DataOf(telnet));
    }

    [Fact]
    public void WindowSize_IsReported_EvenWhenSplitAcrossReads()
    {
        var (telnet, _) = Create();
        (int, int)? size = null;
        telnet.WindowSizeChanged += (columns, rows) => size = (columns, rows);

        telnet.Parse([Iac, Sb, Naws, 0]);
        telnet.Parse([200, 0, 50, Iac]);
        telnet.Parse([Se, (byte)'x']);

        Assert.Equal((200, 50), size);
        Assert.Equal((200, 50), telnet.WindowSize);
        Assert.Equal("x"u8.ToArray(), DataOf(telnet));
    }

    [Fact]
    public void WindowSize_WithIacDoubled_IsRead()
    {
        var (telnet, _) = Create();

        telnet.Parse([Iac, Sb, Naws, 1, Iac, Iac, 0, 24, Iac, Se]);

        Assert.Equal((511, 24), telnet.WindowSize);
    }

    [Fact]
    public void TerminalType_IsAskedFor_AndRead()
    {
        var (telnet, sent) = Create();

        telnet.Parse([Iac, Will, Ttype]);
        Assert.Equal([Iac, Sb, Ttype, 1, Iac, Se], sent.ToArray());

        telnet.Parse([Iac, Sb, Ttype, 0, .. "XTERM-256COLOR"u8, Iac, Se]);
        Assert.Equal("XTERM-256COLOR", telnet.TerminalType);
    }

    [Fact]
    public void OptionsNotOffered_AreRefused_AndRefusalsAreNotAnswered()
    {
        var (telnet, sent) = Create();

        telnet.Parse([Iac, Do, 5, Iac, Will, 32, Iac, Wont, 5, Iac, Dont, 1, Iac, Will, Naws, Iac, Do, 1]);

        Assert.Equal([Iac, Wont, 5, Iac, Dont, 32], sent.ToArray());
    }

    [Fact]
    public void ALongSubnegotiation_IsDropped()
    {
        var (telnet, _) = Create();
        var junk = Enumerable.Repeat((byte)'a', TelnetStream.MaxSubnegotiationLength * 2).ToArray();

        telnet.Parse([Iac, Sb, Ttype, 0, .. junk, Iac, Se, (byte)'k']);

        Assert.Null(telnet.TerminalType);
        Assert.Equal("k"u8.ToArray(), DataOf(telnet));
    }

    [Fact]
    public async Task Reads_ReturnOnlyData()
    {
        var inner = new MemoryStream([Iac, Do, 1, (byte)'o', (byte)'k', Iac, Sb, Naws, 0, 90, 0, 30, Iac, Se]);
        var telnet = new TelnetStream(inner);
        var buffer = new byte[16];

        var count = await telnet.ReadAsync(buffer);

        Assert.Equal("ok", System.Text.Encoding.ASCII.GetString(buffer, 0, count));
        Assert.Equal((90, 30), telnet.WindowSize);
        Assert.Equal(0, await telnet.ReadAsync(buffer)); // the end of the connection
    }

    [Fact]
    public async Task Negotiate_AsksForEverything_AndStopsWaitingAfterTheTimeout()
    {
        var silent = new DuplexTestStream();
        var telnet = new TelnetStream(silent);

        var started = DateTime.UtcNow;
        await telnet.NegotiateAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);

        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(5));
        Assert.Null(telnet.WindowSize);
        Assert.Contains("ÿý\u001f", System.Text.Encoding.Latin1.GetString(System.Text.Encoding.UTF8.GetBytes(silent.Output)) + Latin1Output(silent));

        // What the client types after the timeout is still read
        silent.Type("y");
        var buffer = new byte[4];
        Assert.Equal(1, await telnet.ReadAsync(buffer));
        Assert.Equal((byte)'y', buffer[0]);
    }

    [Fact]
    public async Task Negotiate_StopsBufferingAClientThatFloodsData()
    {
        var flood = new DuplexTestStream();
        var telnet = new TelnetStream(flood);
        for (var i = 0; i < 256; i++)
            flood.Send(new byte[4096]); // 1 MB of data, and no answers

        await telnet.NegotiateAsync(TimeSpan.FromSeconds(5), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(4));

        Assert.InRange(telnet.BufferedDataCount, 1, TelnetStream.MaxNegotiationBuffer + 4096);
    }

    private static string Latin1Output(DuplexTestStream stream) =>
        System.Text.Encoding.Latin1.GetString(stream.OutputBytes);
}
