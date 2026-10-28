using System.Globalization;
using ConsoleMVC.App;
using ConsoleMVC.App.Controllers;
using ConsoleMVC.App.Services;
using ConsoleMVC.Mvc;
using ConsoleMVC.Rendering;
using ConsoleMVC.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.App.Tests;

/// <summary>
/// Runs the application with scripted input, the way a user would, and checks the screens it draws.
/// Run with <c>dotnet test ConsoleMVC.App.Tests</c>. See https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/guides/docs/testing.md
/// </summary>
public class AppTests
{
    /// <summary>
    /// The application as Program.cs sets it up (services, theme, mouse, shortcuts), in a fixed culture so the tests read
    /// the same everywhere; replace any service with a fake here.
    /// </summary>
    private static MvcTestHost Host(string? culture = null)
    {
        var host = MvcTestHost.For<HomeController>(builder =>
        {
            builder.Services.AddSingleton<IGreetingService, GreetingService>();
            builder.Services.Configure<GreetingOptions>(options => options.Salutation = "Hello");

            // The localisation set up in Program.cs, in a fixed culture so the tests read the same everywhere
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Localization.Culture = CultureInfo.GetCultureInfo(culture ?? "en-GB");
            builder.Localization.FrameworkTextResource = typeof(SharedResource);
            builder.Localization.LocalizeValidation = true;
            builder.Localization.ValidationResource = typeof(SharedResource);

            builder.Theme.Set("brand", new ThemeStyle { BorderColour = Colour.Magenta, TitleColour = Colour.Magenta, Bold = true });
            builder.Mouse = true;
        });

        // The shortcuts Program.cs maps after Build()
        host.ConfigureApplication = app =>
        {
            app.MapKey(ConsoleKey.F1, "Home/About", "Help");
            app.MapKey(new KeyGesture(ConsoleKey.Escape), NavigationResult.Back(), "Back");
            app.MapKey(KeyGesture.Parse("Ctrl+X"), NavigationResult.Quit(), "Quit");
        };
        return host;
    }

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
        Assert.Contains("Session: this console", run.Screens[3]);   // not served remotely
    }

    [Fact]
    public void AboutPage_DrawsItsMarkdown()
    {
        var run = Host().Run("1", "q");

        Assert.Contains("Each page has its own Controller, View and Model", run.Screens[1]); // **bold** without the stars
        Assert.Contains("• Navigation with <menu>", run.Screens[1]);
    }

    [Fact]
    public void GreetingForm_SpeaksPolish()
    {
        var run = Host("pl-PL").Run("2", "", "Ada", "3", "n", "q");

        Assert.Contains("Formularz powitania", run.Screens[1]);
        Assert.Contains("Imię:", run.Screens[1]);
        Assert.Contains("To pole jest wymagane.", run.Screens[1]);      // the framework's own message, translated
        Assert.Contains("Wybierz opcję:", run.Screens[0]);
    }

    [Fact]
    public void SettingsPage_IsInTheAdminArea()
    {
        var run = Host().Run("4", "3", "1", "q");

        Assert.Equal(["Home/Index", "Home/Features", "Admin/Settings/Index", "Home/Index"], run.Routes);
        Assert.Contains("Admin: settings", run.Screens[2]);
        Assert.Contains("en-GB", run.Screens[2]);
    }

    [Fact]
    public void Booking_ConfirmsATable()
    {
        // Typed answers, as with piped input: the day as +3, the fourth table, two extras, a PIN
        var run = Host().Run("5", "Ada", "+3", "4", "4", "1,2", "4821", "q");

        Assert.Equal(["Home/Index", "Booking/Index", "Booking/Confirm"], run.Routes);
        Assert.Contains("Booked", run.LastScreen);
        Assert.Contains("garden-2 for 4", run.LastScreen);
        Assert.Contains("Extras: cake, flowers", run.LastScreen);
        Assert.DoesNotContain("4821", run.Output);                     // the PIN is masked
    }

    [Fact]
    public void Booking_AsksAgainForAWrongPin_AndKeepsTheOtherAnswers()
    {
        var run = Host().Run("5", "Ada", "+3", "4", "4", "", "12",      // a PIN of two digits
                             "", "", "", "", "", "4821", "q");          // Enter keeps each valid answer

        Assert.Contains("The PIN must be four digits.", run.Screens[2]);
        Assert.Contains("Booked", run.LastScreen);
        Assert.Contains("Ada", run.LastScreen);
        Assert.Contains("garden-2 for 4", run.LastScreen);
    }

    [Fact]
    public void F1_OpensTheHelp_FromAnyScreen()
    {
        var run = Host().Run(new ScriptedInput("2").Keys(ConsoleKey.F1).Line("q"));

        Assert.Equal(["Home/Index", "Greet/Index", "Home/About"], run.Routes);
    }

    [Fact]
    public void ClickingAMenuOption_ChoosesIt()
    {
        // The home screen: the app bar, the header box (three rows), a blank line, the separator, the shortcuts, then
        // the options; row 7 is "[1] About"
        var run = Host().Run(new ScriptedInput().Click(2, 7).Line("q"));

        Assert.Equal("Home/About", run.Routes[1]);
    }
}
