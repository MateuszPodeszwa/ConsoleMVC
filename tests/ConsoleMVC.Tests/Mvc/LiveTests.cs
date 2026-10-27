using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;
using ConsoleMVC.Tests.Mvc.LiveApp;
using ConsoleMVC.Tests.Mvc.LiveApp.Controllers;
using ConsoleMVC.Tests.Mvc.TestApp;
using Microsoft.Extensions.DependencyInjection;
using ScriptedInput = ConsoleMVC.Testing.ScriptedInput;

namespace ConsoleMVC.Tests.Mvc;

/// <summary>Tests for timed refresh and live regions in an application (docs/design/live-elements.md).</summary>
[Collection(TestAppCollection.Name)]
public class LiveTests
{
    private static MvcTestHost Host() =>
        MvcTestHost.For<DashboardController>(builder =>
        {
            builder.DefaultController = "Dashboard";
            builder.Services.AddSingleton<VisitCounter>();
        });

    [Fact]
    public void View_RefreshesUntilTheUserAnswers()
    {
        var run = Host().Run(new ScriptedInput().Wait().Wait().Line("q"));

        Assert.Equal(["Dashboard/Index", "Dashboard/Index", "Dashboard/Index"], run.Routes);
        Assert.Equal(3, run.Screens.Count);
        Assert.Contains("Visits: 1", run.Screens[0]);
        Assert.Contains("Visits: 3", run.LastScreen);
        Assert.DoesNotContain("Visits: 2", run.LastScreen); // the screen was cleared before each refresh
    }

    [Fact]
    public void View_ChoosesItsOwnRefresh_AndBackSkipsTheRefreshes()
    {
        var run = Host().Run(new ScriptedInput("c").Wait().Wait().Line("").Line("q"));

        Assert.Equal(["Dashboard/Index", "Dashboard/Clock", "Dashboard/Clock", "Dashboard/Clock", "Dashboard/Index"], run.Routes);
        Assert.Contains("Tick 4", run.Screens[3]);
    }

    [Fact]
    public void NoRefresh_TurnsTheViewsRefreshOff()
    {
        var host = Host();

        var error = Assert.Throws<InvalidOperationException>(() => host.Run(new ScriptedInput("s").Wait()));

        Assert.Contains("refresh", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refresh_IsIgnored_WhenTheInputCannotWait()
    {
        var builder = MvcApplication.CreateBuilder([]);
        builder.ApplicationAssembly = typeof(DashboardController).Assembly;
        builder.DefaultController = "Dashboard";
        builder.Services.AddSingleton<VisitCounter>();
        var output = new StringWriter { NewLine = "\n" };
        builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(output), () => 40);
        builder.Input = new Rendering.ScriptedInput("q"); // has no WaitForInput: the default says input is there

        using var app = builder.Build();
        app.Run();

        Assert.Contains("Visits: 1", output.ToString());
        Assert.DoesNotContain("Visits: 2", output.ToString());
    }

    [Fact]
    public void LiveRegion_ShowsItsFinalState_BeforeTheNextView()
    {
        var run = Host().Run("i");

        Assert.Contains("Import ██████ 100%", run.Output);
        Assert.Contains("Imported", run.LastScreen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(-1000)]
    public void RefreshEvery_RefusesIntervalsShorterThanTheMinimum(int milliseconds)
    {
        var result = new ViewResult();

        Assert.Throws<ArgumentOutOfRangeException>(() => result.RefreshEvery(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void ScriptedInput_Wait_IsOnlyForRefreshingScreens()
    {
        var input = new ScriptedInput().Wait().Line("x");

        Assert.Throws<InvalidOperationException>(() => input.ReadLine());
        Assert.False(input.WaitForInput(TimeSpan.FromSeconds(1)));
        Assert.True(input.WaitForInput(TimeSpan.FromSeconds(1)));
        Assert.Equal("x", input.ReadLine());
        Assert.Equal(0, input.Remaining);
    }
}
