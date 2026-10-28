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
}
