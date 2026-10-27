using ConsoleMVC.App.Controllers;
using ConsoleMVC.App.Services;
using ConsoleMVC.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.App.Tests;

/// <summary>
/// Runs the application with scripted input, the way a user would, and checks the screens it draws.
/// Run with <c>dotnet test ConsoleMVC.App.Tests</c>. See https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/guides/docs/testing.md
/// </summary>
public class AppTests
{
    /// <summary>The application with the services Program.cs registers; replace any of them with a fake here.</summary>
    private static MvcTestHost Host() => MvcTestHost.For<HomeController>(builder =>
    {
        builder.Services.AddSingleton<IGreetingService, GreetingService>();
        builder.Services.Configure<GreetingOptions>(options => options.Salutation = "Hello");
    });

    [Fact]
    public void HomeMenu_MatchesTheSnapshot()
    {
        var run = Host().Run("q");

        // The first run creates __snapshots__/home-menu.txt; later runs fail if the screen changes.
        // Accept an intended change with: CONSOLEMVC_UPDATE_SNAPSHOTS=1 dotnet test
        Snapshot.Match(run.Screens[0], "home-menu");
    }

    [Fact]
    public void GreetingForm_GreetsByName()
    {
        var input = new ScriptedInput("2", "Ada")              // the greeting form, then a name
            .Keys(ConsoleKey.UpArrow, ConsoleKey.Enter)         // the colour <select>, answered with the arrow keys
            .Line("n")                                          // the <checkbox>, typed
            .Line("q");

        var run = Host().Run(input);

        Assert.Equal(["Home/Index", "Greet/Index", "Greet/Result"], run.Routes);
        Assert.Contains("Hello, Ada!", run.LastScreen);
        Assert.Contains("green", run.LastScreen);
    }

    [Fact]
    public void GreetingForm_RequiresAName()
    {
        var run = Host().Run("2", "", "Ada", "3", "n", "q");

        Assert.Contains("This field is required.", run.Screens[1]);
        Assert.Contains("Hello, Ada!", run.LastScreen);
    }

    [Fact]
    public void StatusPage_RefreshesUntilYouType()
    {
        var input = new ScriptedInput("4", "2")                 // the feature tour, then the live status
            .Wait()                                             // a second passes with nothing typed: the page refreshes
            .Line("b");                                         // back to the tour

        var run = Host().Run(input);

        Assert.Equal(["Home/Index", "Home/Features", "Home/Check", "Home/Status", "Home/Status", "Home/Features"], run.Routes);
        Assert.Contains("Checking services ████", run.Output);
        Assert.Contains("Services up ████████████████████ 100%", run.Output);
        Assert.Contains("Payments", run.Screens[3]);                // the bar chart of response times
    }

    [Fact]
    public void AboutPage_DrawsItsMarkdown()
    {
        var run = Host().Run("1", "q");

        Assert.Contains("Each page has its own Controller, View and Model", run.Screens[1]); // **bold** without the stars
        Assert.Contains("• Navigation with <menu>", run.Screens[1]);
    }
}
