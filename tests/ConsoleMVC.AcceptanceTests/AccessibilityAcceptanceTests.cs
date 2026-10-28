using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;

namespace Acceptance;

/// <summary>Screen-reader mode, as docs/guides/docs/accessibility.md describes it: "No colours and no cursor movement."</summary>
public class AccessibilityAcceptanceTests
{
    /// <summary>Runs the application on an ANSI renderer writing to a string, as a terminal would receive it.</summary>
    private static string RunOnAnsiTerminal(bool screenReader, params string[] lines)
    {
        var terminal = new StringWriter();
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(App).Assembly;
        builder.Renderer = new MarkupRenderer(new AnsiRenderTarget(terminal, ColourSupport.TrueColour), () => 80);
        builder.Input = new ScriptedInput(lines);
        builder.ScreenReader = screenReader;
        using var app = builder.Build();
        app.Run();
        return terminal.ToString();
    }

    [Fact]
    public void WithItsOwnAnsiRenderer_ScreenReaderModeWritesNoEscapeCodes()
    {
        var output = RunOnAnsiTerminal(screenReader: true, "x", "q");   // an invalid choice shows a red error too

        Assert.Contains("Styled warning", output);
        Assert.Contains("Invalid choice", output);
        Assert.True(!output.Contains('\u001b'), output.Replace("\u001b", "<ESC>"));
    }

    [Fact]
    public void WithoutScreenReaderMode_TheSameRendererWritesColours()
    {
        var output = RunOnAnsiTerminal(screenReader: false, "q");

        Assert.Contains("\u001b[", output);   // the test would prove nothing if the renderer never wrote codes
    }

    [Fact]
    public void ARefreshingPage_Refreshes()
    {
        var run = App.Host().Run(new ScriptedInput("7").Wait().Line("q"));

        Assert.Equal(["Home/Index", "Clock/Index", "Clock/Index"], run.Routes);
    }

    [Fact]
    public void InScreenReaderMode_APageDoesNotRefresh()
    {
        // Wait() is only used up by a screen that refreshes: here it is not, so the run says so
        var host = MvcTestHost.For<Controllers.HomeController>(builder => builder.ScreenReader = true);

        var error = Assert.Throws<InvalidOperationException>(() => host.Run(new ScriptedInput("7").Wait().Line("q")));

        Assert.Contains("Wait()", error.Message);
    }
}
