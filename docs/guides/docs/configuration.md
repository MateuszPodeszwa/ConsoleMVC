# Builder Options

## The builder

```csharp
var builder = MvcApplication.CreateBuilder(args);

builder.DefaultController = "Dashboard";                         // default "Home"
builder.DefaultAction = "Overview";                              // default "Index"
builder.ClearScreenBetweenViews = false;                         // default true
builder.FullScreen = true;                                       // default false
builder.Mouse = true;                                            // default false (or the ConsoleMVC:Mouse setting)
builder.ScreenReader = true;                                     // default false (or the ConsoleMVC:ScreenReader setting)
builder.HighContrast = true;                                     // default false (or the ConsoleMVC:HighContrast setting)
builder.ApplicationAssembly = typeof(Program).Assembly;          // default: the entry assembly
builder.Renderer = MarkupRenderer.CreateForConsole();            // default: detected automatically
builder.Input = ConsoleTextInput.Instance;                       // default: the console
builder.Theme.Set("brand", new ThemeStyle { Bold = true });      // default: Theme.CreateDefault()
builder.Services.AddSingleton<IClock, SystemClock>();            // your services
builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop"));
builder.Logging.AddFile("logs/app.log");                          // logs, not on the screen

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
| `ScreenReader` | Plain text for screen readers: no borders or bars, typed answers, no clearing; also on with the `ConsoleMVC:ScreenReader` setting (see [Accessibility](accessibility.md)) |
| `HighContrast` | Use `Theme.CreateHighContrast()`; also on with the `ConsoleMVC:HighContrast` setting |
| `Localization` | The run's culture, the framework's messages (`FrameworkText`) and translated validation messages; see [Localisation](localisation.md) |
| `Mouse` | Click menu options, choices and checkboxes, and scroll with the wheel; also on with the `ConsoleMVC:Mouse` setting (see [Mouse](controllers-and-forms.md#mouse)) |
| `FullScreen` | Run on the terminal's alternate screen, like a full-screen editor; the user's scrollback comes back when the application ends, also after an error or Ctrl+C. Ignored when the output is not a terminal that supports it |
| `ApplicationAssembly` | Where controllers and views are discovered |
| `Renderer` | How markup is drawn: colour support (`NO_COLOR`, `COLORTERM`, Windows VT mode), terminal width and redirected output are detected, and the screen is redrawn when the terminal is resized ([details](c-view-markup.md#resizing-the-terminal)) |
| `Input` | Where `<input>`, `<select>`, `<radio>`, `<checkbox>` and `<menu>` read from |

`Build()` validates the service registrations, so a missing dependency fails at start-up rather than on the screen
that needs it.

## Running and serving

| Call | What it does |
|---|---|
| `app.Run()` / `await app.RunAsync(token)` | Runs on this console until a view quits; serves instead when the `ConsoleMVC:Serve` setting is set |
| `await app.ServeAsync(new TerminalServerOptions(), token)` | Serves the application to many users at once over Telnet or TLS ([Remote Sessions](remote-sessions.md)) |
| `await app.RunSessionAsync(terminal, token)` | Runs one session on a `RemoteTerminal` over any stream |

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
