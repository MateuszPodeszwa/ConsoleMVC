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

using var app = builder.Build();

// Middleware runs around every action: here it shows the current page in the terminal's title bar
app.Use(async (context, next) =>
{
    if (!Console.IsOutputRedirected)
        Console.Title = $"ConsoleMVC — {context.Route}";

    await next(context);
});

await app.RunAsync();
