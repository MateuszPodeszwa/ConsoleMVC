using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Tests.Mvc.KioskApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Remote;

/// <summary>Tests for <see cref="RemoteTerminal"/> and <see cref="MvcApplication.RunSessionAsync(RemoteTerminal, CancellationToken)"/>.</summary>
public class RemoteSessionTests
{
    private static RemoteTerminal Terminal(DuplexTestStream stream, int columns = 80, string? type = null, TimeSpan? idle = null) =>
        new(stream, new RemoteTerminalOptions
        {
            Columns = columns,
            Rows = 24,
            TerminalType = type,
            IdleTimeout = idle ?? RemoteTerminalOptions.DefaultIdleTimeout
        });

    [Fact]
    public async Task ASession_ShowsTheScreen_AndQuitsWhenAViewQuits()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        await stream.WaitForAsync("visit 1 remote=True width=80");
        stream.Type("q\r");
        await session.WaitAsync(RemoteTestSupport.Patience);

        Assert.Contains("Again", stream.Output);
        Assert.Contains("\u001b[1m[a]", stream.Output); // the key is bold
        Assert.Contains("\r\n", stream.Output);
        Assert.DoesNotContain("visit 2", stream.Output);
    }

    [Fact]
    public async Task TypedLines_AreEchoed_AndEditable()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        stream.Type("n\r");
        await stream.WaitForAsync("Name: ");
        stream.Type("Adx\u007fa\r"); // Backspace removes the x
        await stream.WaitForAsync("Hello, Ada!");
        stream.Type("q\r");
        await session.WaitAsync(RemoteTestSupport.Patience);

        Assert.Contains("Adx", stream.Output); // echoed as typed
        Assert.Contains("visit 2", stream.Output); // Hello goes back to the index
    }

    [Fact]
    public async Task SessionItems_LastForTheSession()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        stream.Type("a\ra\r");
        await stream.WaitForAsync("visit 3");
        stream.Type("q\r");
        await session.WaitAsync(RemoteTestSupport.Patience);
    }

    [Fact]
    public async Task TheClientDisconnecting_EndsTheSession_WithoutAnError()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        await stream.WaitForAsync("visit 1");
        stream.EndInput();

        await session.WaitAsync(RemoteTestSupport.Patience);
        Assert.True(terminal.Disconnected.IsCancellationRequested);
    }

    [Fact]
    public async Task CtrlC_EndsTheSession()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        await stream.WaitForAsync("visit 1");
        stream.Type("\u0003");

        await session.WaitAsync(RemoteTestSupport.Patience);
        Assert.True(terminal.Disconnected.IsCancellationRequested);
    }

    [Fact]
    public async Task AnIdleClient_IsDisconnected()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream, idle: TimeSpan.FromMilliseconds(200));

        await app.RunSessionAsync(terminal).WaitAsync(RemoteTestSupport.Patience);

        Assert.True(terminal.Disconnected.IsCancellationRequested);
    }

    [Fact]
    public async Task CancellingTheToken_EndsASessionWaitingForAKey()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);
        using var cancellation = new CancellationTokenSource();

        var session = app.RunSessionAsync(terminal, cancellation.Token);
        await stream.WaitForAsync("visit 1");
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.WaitAsync(RemoteTestSupport.Patience));
        Assert.True(terminal.Disconnected.IsCancellationRequested);
    }

    [Fact]
    public async Task Resizing_RedrawsAtTheNewWidth_WithinTheLimits()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        await stream.WaitForAsync("width=80");
        terminal.Resize(5000, 0);

        Assert.Equal(RemoteTerminal.MaxColumns, terminal.Width);
        Assert.Equal(1, terminal.Height);
        stream.Type("a\r");
        await stream.WaitForAsync($"width={RemoteTerminal.MaxColumns}");
        stream.Type("q\r");
        await session.WaitAsync(RemoteTestSupport.Patience);
    }

    [Fact]
    public async Task UnicodeInput_IsDecoded_EvenWhenSplitBetweenPackets()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        stream.Type("n\r");
        await stream.WaitForAsync("Name: ");
        var bytes = System.Text.Encoding.UTF8.GetBytes("Zażółć\r");
        stream.Send(bytes[..3]); // splits the two bytes of "ż"
        stream.Send(bytes[3..]);
        await stream.WaitForAsync("Hello, Zażółć!");
        stream.Type("q\r");
        await session.WaitAsync(RemoteTestSupport.Patience);
    }

    [Fact]
    public async Task ArrowKeyFields_WorkRemotely()
    {
        var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.ApplicationAssembly = typeof(KioskGreeter).Assembly;
        builder.DefaultController = "Kiosk";
        builder.Services.AddSingleton<IKioskGreeter, KioskGreeter>();
        builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(TextWriter.Null), () => 80);
        await using var app = builder.Build();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        stream.Type("1\rAda\r");
        await stream.WaitForAsync("Colour:");
        stream.Type("\u001b[B\u001b[B\r"); // Down twice: blue
        await stream.WaitForAsync("Hello, Ada (blue)");
        await session.WaitAsync(RemoteTestSupport.Patience);
    }

    [Fact]
    public async Task ColoursFollowTheTerminalType()
    {
        Assert.Equal(ColourSupport.None, RemoteTerminal.ColoursFor("dumb"));
        Assert.Equal(ColourSupport.Basic, RemoteTerminal.ColoursFor("XTERM-256COLOR"));
        Assert.Equal(ColourSupport.Basic, RemoteTerminal.ColoursFor(null));
        Assert.Equal(ColourSupport.TrueColour, RemoteTerminal.ColoursFor("xterm-kitty"));
        Assert.Equal(ColourSupport.TrueColour, RemoteTerminal.ColoursFor("xterm-direct"));

        await using var terminal = Terminal(new DuplexTestStream(), type: "dumb");
        Assert.Equal(ColourSupport.None, terminal.Colours);
    }

    [Fact]
    public void TheLocalSession_IsResolvedOutsideARemoteSession()
    {
        using var app = RemoteTestSupport.BuildApp();

        var session = app.Services.GetRequiredService<ITerminalSession>();

        Assert.Equal("local", session.Id);
        Assert.False(session.IsRemote);
        Assert.Null(session.RemoteEndPoint);
        session.Disconnect(); // nothing to disconnect
    }

    [Fact]
    public async Task LiveElements_DrawOnTheTerminalOfTheirSession()
    {
        await using var terminal = Terminal(new DuplexTestStream());
        var drawn = new RecordingDisplay();
        var local = new RecordingDisplay();
        var display = new SessionLiveDisplay(local);

        using (TerminalSession.Use(TerminalSession.ForRemote(terminal, drawn)))
            display.Start(new SpinnerElement()).Dispose();
        display.Start(new SpinnerElement()).Dispose();

        Assert.Equal(1, drawn.Started);
        Assert.Equal(1, local.Started);
    }

    [Fact]
    public async Task ScreenReaderMode_IsPlainTextRemotely()
    {
        await using var app = RemoteTestSupport.BuildApp(builder => builder.ScreenReader = true);
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        await stream.WaitForAsync("visit 1");
        stream.Type("q\r");
        await session.WaitAsync(RemoteTestSupport.Patience);

        Assert.DoesNotContain('\u001b', stream.Output); // no colours, no cursor movement
    }

    [Fact]
    public async Task ABrokenConnection_EndsTheSession_WithoutAnError()
    {
        await using var app = RemoteTestSupport.BuildApp();
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);

        var session = app.RunSessionAsync(terminal);
        await stream.WaitForAsync("visit 1");
        stream.FailWrites = true;
        stream.Type("a\r");

        await session.WaitAsync(RemoteTestSupport.Patience); // completes, does not throw
        Assert.True(terminal.Disconnected.IsCancellationRequested);
    }

    [Fact]
    public async Task ALiveElement_SurvivesItsTerminalBreaking()
    {
        var stream = new DuplexTestStream();
        await using var terminal = Terminal(stream);
        var region = new LiveDisplay(terminal.Renderer).Start(new SpinnerElement(), TimeSpan.FromMinutes(1));

        stream.FailWrites = true;
        region.Tick(); // on the timer thread this would have brought the process down

        region.Dispose();
        Assert.True(terminal.Disconnected.IsCancellationRequested);
    }

    [Fact]
    public void IdleTimeout_MustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RemoteTerminal(new DuplexTestStream(), new RemoteTerminalOptions { IdleTimeout = TimeSpan.Zero }));
    }

    private sealed class RecordingDisplay : ILiveDisplay
    {
        public int Started;

        public LiveRegion Start(Element element, TimeSpan? interval = null)
        {
            Started++;
            return new LiveDisplay(new MarkupRenderer(new PlainTextRenderTarget(TextWriter.Null), () => 80)).Start(element, interval);
        }
    }
}
