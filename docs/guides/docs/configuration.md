# Builder Options

## The builder

```csharp
var builder = MvcApplication.CreateBuilder(args);

builder.DefaultController = "Dashboard";                         // default "Home"
builder.DefaultAction = "Overview";                              // default "Index"
builder.ClearScreenBetweenViews = false;                         // default true
builder.ApplicationAssembly = typeof(Program).Assembly;          // default: the entry assembly
builder.Renderer = MarkupRenderer.CreateForConsole();            // default: detected automatically
builder.Input = ConsoleTextInput.Instance;                       // default: the console
builder.Theme.Set("brand", new ThemeStyle { Bold = true });      // default: Theme.CreateDefault()
builder.Services.AddSingleton<IClock, SystemClock>();            // your services
builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop"));
builder.Logging.AddDebug();                                       // logs, not on the screen

using var app = builder.Build();
app.Use(async (context, next) => await next(context));           // middleware
await app.RunAsync();
```

| Option | Purpose |
|---|---|
| `DefaultController` / `DefaultAction` | The first route |
| `Services` | Services for controller constructors, `@inject` and middleware ([Dependency Injection](dependency-injection.md)) |
| `Configuration` / `Environment` / `Logging` | Settings, the environment and log providers ([Configuration Files and Logging](hosting.md)) |
| `Theme` | Named styles for `class="…"` ([themes](c-view-markup.md#themes-and-class)) |
| `ClearScreenBetweenViews` | Clear the console before each view |
| `ApplicationAssembly` | Where controllers and views are discovered |
| `Renderer` | How markup is drawn: colour support (`NO_COLOR`, `COLORTERM`, Windows VT mode), terminal width and redirected output are detected, and the screen is redrawn when the terminal is resized ([details](c-view-markup.md#resizing-the-terminal)) |
| `Input` | Where `<input>`, `<select>`, `<radio>`, `<checkbox>` and `<menu>` read from |

`Build()` validates the service registrations, so a missing dependency fails at start-up rather than on the screen
that needs it.

## Testing

The `ConsoleMVC.Testing` package runs the application with scripted lines and keys, records each screen and compares
screens with snapshot files:

```csharp
var run = MvcTestHost.For<HomeController>(b => b.Services.AddSingleton<IGreetingService, FakeGreetings>())
    .Run("2", "Ada", "3", "n", "q");

Assert.Contains("Hello, Ada!", run.LastScreen);
Snapshot.Match(run.LastScreen, "greeting");
```

See [Testing](testing.md). `builder.Input` takes any `ITextInput` and `builder.Renderer` any `IMarkupRenderer` if you
want to drive the application yourself.

## Live reload

Run `dotnet watch` during development; see [Getting Started](getting-started.md#live-reload).
