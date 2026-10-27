using ConsoleMVC.App.Services;
using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = MvcApplication.CreateBuilder(args);

// Services are passed to controller constructors and to views that declare @inject
builder.Services.AddSingleton<IGreetingService, GreetingService>();

using var app = builder.Build();
app.Run();
