using ConsoleMVC.App.Services;
using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = MvcApplication.CreateBuilder(args);

// Services are passed to controller constructors and to views that declare @inject
builder.Services.AddSingleton<IGreetingService, GreetingService>();

// Settings from appsettings.json, appsettings.{Environment}.json, environment variables and the command line
builder.Services.Configure<GreetingOptions>(builder.Configuration.GetSection(GreetingOptions.SectionName));

using var app = builder.Build();

// Middleware runs around every action: here it shows the current page in the terminal's title bar
app.Use(async (context, next) =>
{
    if (!Console.IsOutputRedirected)
        Console.Title = $"ConsoleMVC — {context.Route}";

    await next(context);
});

await app.RunAsync();
