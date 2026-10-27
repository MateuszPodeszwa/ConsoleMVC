using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;
using ConsoleMVC.Tests.Mvc.KioskApp;
using ConsoleMVC.Tests.Mvc.KioskApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Tests.Testing;

/// <summary>
/// Tests for <see cref="MvcTestHost"/>, <see cref="ScriptedInput"/> and <see cref="ScreenRecorder"/>
/// (docs/design/testing-package.md).
/// </summary>
[Collection(TestAppCollection.Name)]
public class MvcTestHostTests
{
    /// <summary>Greets loudly, to show that tests can replace services.</summary>
    private sealed class LoudGreeter : IKioskGreeter
    {
        public string Greet(string name) => $"HELLO, {name.ToUpperInvariant()}";
    }

    private static MvcTestHost Host(Action<ConsoleMVC.Mvc.MvcApplicationBuilder>? configure = null) =>
        MvcTestHost.For<KioskController>(builder =>
        {
            builder.DefaultController = "Kiosk";
            builder.Services.AddSingleton<IKioskGreeter, KioskGreeter>();
            configure?.Invoke(builder);
        });

    [Fact]
    public void Run_RecordsEachScreenAndRoute()
    {
        var run = Host().Run("1", "Ada", "2");

        Assert.Equal(["Kiosk/Index", "Kiosk/Form", "Kiosk/Done"], run.Routes);
        Assert.Equal(3, run.Screens.Count);
        Assert.StartsWith("Welcome\n[1] Form\n[l] Loop\n[q] Quit\n", run.Screens[0]);
        Assert.Equal("Hello, Ada (green)\n", run.LastScreen);
        Assert.Contains("Welcome", run.Output);
        Assert.Contains("Hello, Ada (green)", run.Output);
        Assert.Equal(0, run.RemainingInput);
    }

    [Fact]
    public void Run_UsesTheServicesTheTestRegisters()
    {
        var run = Host(builder => builder.Services.AddSingleton<IKioskGreeter, LoudGreeter>()).Run("1", "Ada", "3");

        Assert.Equal("HELLO, ADA (blue)\n", run.LastScreen);
    }

    [Fact]
    public void Run_DrivesArrowKeyFieldsFromTheScript()
    {
        var input = new ScriptedInput()
            .Line("1")
            .Line("Grace")
            .Keys(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);

        var run = Host().Run(input);

        Assert.Equal("Hello, Grace (blue)\n", run.LastScreen);
        Assert.Contains("Colour: blue", run.Screens[1]); // the select collapsed to its answer
    }

    [Fact]
    public void Run_EndsWhenTheScriptEnds_AndReportsWhatWasLeft()
    {
        Assert.Equal(["Kiosk/Index"], Host().Run().Routes);

        var run = Host().Run("q", "unused", "also unused");
        Assert.Equal(2, run.RemainingInput);
    }

    [Fact]
    public void Run_StopsAnApplicationThatNeverWaits()
    {
        var host = Host();
        host.MaxScreens = 10;

        var error = Assert.Throws<InvalidOperationException>(() => host.Run("l"));

        Assert.Contains("10 screens", error.Message);
    }

    [Fact]
    public void Run_RendersAtTheHostWidth()
    {
        var host = Host();
        host.Width = 20;

        var run = host.Run("q");

        Assert.All(run.Output.Split('\n'), line => Assert.True(line.Length <= 20, line));
    }

    [Fact]
    public async Task RunAsync_Works()
    {
        var run = await Host().RunAsync(new ScriptedInput("q"));

        Assert.Equal(["Kiosk/Index"], run.Routes);
    }

    [Fact]
    public void Run_UsesTheTestingEnvironment()
    {
        string? environment = null;
        Host(builder => environment = builder.Environment.EnvironmentName).Run("q");

        Assert.Equal("Testing", environment);
    }

    // ------------------------------------------------------------------ ScriptedInput

    [Fact]
    public void ScriptedInput_ReportsKeysOnlyWhenTheNextStepIsAKey()
    {
        var input = new ScriptedInput("a").Key(ScriptedInput.Char('x')).Line("b");
        ITextInput text = input;

        Assert.False(text.CanReadKeys);
        Assert.Equal("a", text.ReadLine());
        Assert.True(text.CanReadKeys);
        Assert.Equal('x', text.ReadKey()!.Value.KeyChar);
        Assert.Equal("b", text.ReadMasked('*'));
        Assert.Null(text.ReadLine());
        Assert.Null(text.ReadKey());
        Assert.Equal(0, input.Remaining);
    }

    [Fact]
    public void ScriptedInput_ExplainsAStepOfTheWrongKind()
    {
        ITextInput input = new ScriptedInput().Keys(ConsoleKey.Enter);

        var error = Assert.Throws<InvalidOperationException>(() => input.ReadLine());
        Assert.Contains("key", error.Message);
        Assert.Throws<InvalidOperationException>(() => ((ITextInput)new ScriptedInput("a")).ReadKey());
    }

    // ------------------------------------------------------------------ ScreenRecorder

    [Fact]
    public void ScreenRecorder_SplitsScreensAtClear_AndCanEraseLines()
    {
        var recorder = new ScreenRecorder();
        var renderer = new MarkupRenderer(recorder, () => 40);
        var first = new TextElement();
        first.AppendText("one");
        var second = new TextElement();
        second.AppendText("two");

        renderer.Render(first);
        renderer.Clear();
        renderer.Render(first);
        renderer.Rewrite(second);
        renderer.WritePrompt("> ", TextStyle.Plain);

        Assert.Equal(["one\n", "two\n> "], recorder.Screens);
        Assert.Equal("one\ntwo\n> ", recorder.Output);
    }
}
