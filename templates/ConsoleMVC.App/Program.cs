using ConsoleMVC.App;
using ConsoleMVC.App.Services;
using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = MvcApplication.CreateBuilder(args);

// Services are passed to controller constructors and to views that declare @inject
builder.Services.AddSingleton<IGreetingService, GreetingService>();

// Settings from appsettings.json, appsettings.{Environment}.json, environment variables and the command line
builder.Services.Configure<GreetingOptions>(builder.Configuration.GetSection(GreetingOptions.SectionName));

// Logs go to a file, because the console shows the application (levels: the "Logging" section of appsettings.json)
builder.Logging.AddFile("logs/app.log");

// Run on the terminal's alternate screen, like a full-screen editor: your scrollback comes back when the app ends
builder.FullScreen = true;

// Click menu options, choices and checkboxes, and scroll with the wheel, in terminals that report the mouse
builder.Mouse = true;

// Localisation: views use IStringLocalizer<SharedResource> (Resources/SharedResource*.resx), and the framework's own
// messages and validation messages are translated too. Try: dotnet run -- --ConsoleMVC:Culture=pl-PL
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Localization.FrameworkTextResource = typeof(SharedResource);
builder.Localization.LocalizeValidation = true;
builder.Localization.ValidationResource = typeof(SharedResource);

// Accessibility: users choose without a new build, e.g. dotnet run -- --ConsoleMVC:ScreenReader=true
// (plain text for screen readers) or --ConsoleMVC:HighContrast=true (bold, bright colours)

using var app = builder.Build();

// Keyboard shortcuts that work on every screen; the layout lists them with <shortcuts />
app.MapKey(new KeyGesture(ConsoleKey.Escape), NavigationResult.Back(), "Back");
app.MapKey(KeyGesture.Parse("Ctrl+X"), NavigationResult.Quit(), "Quit");

// Middleware runs around every action: here it shows the current page in the terminal's title bar
app.Use(async (context, next) =>
{
    if (!Console.IsOutputRedirected)
        Console.Title = $"ConsoleMVC — {context.Route}";

    await next(context);
});

await app.RunAsync();
