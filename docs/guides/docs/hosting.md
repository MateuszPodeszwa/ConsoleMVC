# Configuration Files and Logging

ConsoleMVC reads settings and writes logs with the standard .NET libraries (`Microsoft.Extensions.Configuration`,
`Microsoft.Extensions.Options` and `Microsoft.Extensions.Logging`), the same ones ASP.NET Core uses.

## Configuration

`builder.Configuration` is filled when the builder is created, from these sources. Later ones win:

| Source | Example |
|---|---|
| `appsettings.json` in the application's folder (optional) | `{ "Shop": { "Name": "Corner" } }` |
| `appsettings.{Environment}.json` (optional) | `appsettings.Development.json` |
| Environment variables (`__` separates sections) | `Shop__Name=Corner` |
| Command-line arguments | `dotnet run -- --Shop:Name=Corner` |

The template's project copies `appsettings*.json` next to the application when it builds:

```xml
<ItemGroup>
    <None Update="appsettings*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Read values directly, or bind a section to a class and inject it:

```csharp
var builder = MvcApplication.CreateBuilder(args);

var pageSize = builder.Configuration.GetValue("Shop:PageSize", 20);
builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop"));
builder.Configuration.AddJsonFile("local.json", optional: true);   // add your own sources
```

```csharp
public sealed class ShopOptions
{
    public string Name { get; set; } = "";
    public int PageSize { get; set; } = 20;
}

public class OrdersController(IOptions<ShopOptions> shop) : Controller
{
    public ActionResult Index() => View(new OrdersModel { Title = shop.Value.Name });
}
```

Views can inject configuration too:

```
@inject Microsoft.Extensions.Configuration.IConfiguration Config

<text>@Config["Shop:Name"]</text>
```

`IConfiguration` is registered as a service and is also `app.Configuration`.

### Checking settings at start-up

Options validated with `ValidateOnStart()` are checked by `Build()`, so a bad setting fails before the first screen:

```csharp
builder.Services.AddOptions<ShopOptions>()
    .Bind(builder.Configuration.GetSection("Shop"))
    .Validate(o => o.PageSize is > 0 and <= 100, "Shop:PageSize must be between 1 and 100.")
    .ValidateOnStart();
```

!!! warning "Secrets"
    Keep passwords and API keys out of `appsettings.json`, which is usually committed: use environment variables or a
    secret store.

## Environment

`builder.Environment` (and `app.Environment`, or an injected `IHostEnvironment`) tells where the application runs:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

| Property | Value |
|---|---|
| `EnvironmentName` | `DOTNET_ENVIRONMENT`, or `Production` when it is not set |
| `ContentRootPath` | where configuration files are read from: the application's folder |
| `ApplicationName` | the entry assembly's name |

Choose them in code with `MvcApplicationOptions`, for example in tests:

```csharp
var builder = MvcApplication.CreateBuilder(new MvcApplicationOptions
{
    Args = args,
    EnvironmentName = "Development",
    ContentRootPath = testDirectory
});
```

## Logging

Inject `ILogger<T>` into controllers, services and views
(`@inject Microsoft.Extensions.Logging.ILogger<MyApp.Views.Orders.IndexView> Log`):

```csharp
public class OrdersController(ILogger<OrdersController> logger) : Controller
{
    public ActionResult Cancel(int id)
    {
        logger.LogInformation("Cancelling order {OrderId}", id);
        …
    }
}
```

**No log provider is added by default**: the console shows your application, and a console logger would write over
it. Write logs to a file instead, with the built-in file logger:

```csharp
builder.Logging.AddFile("logs/app.log");           // relative to the application's folder
```

```
2026-10-27T21:04:05.123Z INFO  ConsoleMVC.Mvc.MvcApplication: Starting at Home/Index (Production)
2026-10-27T21:04:09.871Z WARN  Shop.Controllers.OrdersController: Order 42 has no address
```

Entries are written on a background task, so logging never makes a screen wait for the disk. The file rolls over by
size (`app.log` → `app.1.log` → `app.2.log`), continuation lines (exceptions, line breaks in a message) are indented
so a logged value cannot forge an entry, and the file can be followed with `tail -f`. The settings can also come from
the configuration, which `AddFile()` reads from `Logging:File`:

```json
{
  "Logging": {
    "File": {
      "Path": "logs/shop.log",
      "MaxFileSizeBytes": 1048576,
      "RetainedFileCount": 3,
      "LogLevel": { "Default": "Debug" }
    }
  }
}
```

| Option | Default | Purpose |
|---|---|---|
| `Path` | `logs/app.log` | The file, relative to the content root |
| `MaxFileSizeBytes` | 10 MB | Roll over at this size; 0 never rolls over |
| `RetainedFileCount` | 5 | Rolled-over files kept besides the current one |
| `QueueLength` | 10 000 | Entries waiting to be written; when the disk cannot keep up, newer ones are dropped and counted |

Any other provider works too, for example `builder.Logging.AddDebug()` (Microsoft.Extensions.Logging.Debug) for the
debugger's output window.

Levels follow the `Logging` section, as in ASP.NET Core:

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information", "ConsoleMVC": "Debug" }
  }
}
```

The framework logs under the `ConsoleMVC.Mvc.MvcApplication` category:

| Level | Message |
|---|---|
| Information | `Starting at Home/Index (Production)` and `Stopped` |
| Debug | `Running Orders/Details` for every screen or redirect |
| Error | `Unhandled exception on Orders/Save`, with the exception (it is still thrown to the caller) |
