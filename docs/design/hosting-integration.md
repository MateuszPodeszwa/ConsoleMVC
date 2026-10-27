# Design: ConsoleMVC 1.16: Configuration and logging

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.16.0 |
| **Scope** | `builder.Configuration`, `builder.Environment`, `builder.Logging`, `MvcApplicationOptions`, options binding |

---

## 1. Goal

Settings and logs the way every other .NET application has them, without the generic host taking over the console:

```csharp
var builder = MvcApplication.CreateBuilder(args);

builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop"));
if (builder.Environment.IsDevelopment())
    builder.Logging.AddDebug();

using var app = builder.Build();
await app.RunAsync();
```

```csharp
public class OrdersController(IOptions<ShopOptions> shop, ILogger<OrdersController> logger) : Controller { … }
```

## 2. Classes (UML)

```
MvcApplication
  + CreateBuilder(args : string[]) : MvcApplicationBuilder
  + CreateBuilder(options : MvcApplicationOptions) : MvcApplicationBuilder
  + Configuration : IConfiguration
  + Environment : IHostEnvironment

MvcApplicationOptions                  MvcApplicationBuilder
  + Args : string[]                      + Configuration : ConfigurationManager
  + EnvironmentName : string?            + Environment : IHostEnvironment   ──▶ ConsoleHostEnvironment
  + ContentRootPath : string?            + Logging : ILoggingBuilder        ──▶ MvcLoggingBuilder (internal)
  + ApplicationName : string?            + Services : IServiceCollection

MvcApplicationLog (internal, [LoggerMessage] source-generated)
  Starting (Information), Running (Debug), UnhandledException (Error), Stopped (Information)
```

`IHostEnvironment`, `IConfiguration`, `ILoggingBuilder` and the options types are the standard
`Microsoft.Extensions.*` abstractions, so libraries written for the generic host plug in unchanged.

## 3. Rules (pseudocode)

```
new MvcApplicationBuilder(options):
    environment ← options.EnvironmentName ?? $DOTNET_ENVIRONMENT ?? "Production"
    contentRoot ← options.ContentRootPath ?? AppContext.BaseDirectory     (the build copies appsettings*.json there)
    Configuration ← appsettings.json (optional)
                  + appsettings.{environment}.json (optional)
                  + environment variables   (Shop__Name → Shop:Name)
                  + command line            (--Shop:Name=Corner)            later sources win
    Services += IConfiguration, IHostEnvironment, options, logging (levels from the "Logging" section)

Logging: no provider is added. The console is the user interface; a console logger would write over the screen.
    Add one on purpose: AddDebug(), a file logger, OpenTelemetry…

Build(): framework services are added again if Services was cleared (TryAdd), the provider is built with
    ValidateOnBuild, then IStartupValidator runs, so ValidateOnStart() options fail at start-up

RunAsync(): log Starting; each cycle log Running {route}; an exception escaping the loop is logged as
    UnhandledException {route} and rethrown unchanged; log Stopped when a view quits
```

## 4. Security

- Configuration files are read with `reloadOnChange: false`: no file watchers, nothing changes under a running screen.
- Secrets belong in environment variables or a secret store, not in `appsettings.json`; the guide says so.
- Log messages use structured templates (`{Route}`), never string concatenation, so values cannot inject format items.
