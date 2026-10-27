# Configuration and Testing

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

using var app = builder.Build();
app.Use(async (context, next) => await next(context));           // middleware
await app.RunAsync();
```

| Option | Purpose |
|---|---|
| `DefaultController` / `DefaultAction` | The first route |
| `Services` | Services for controller constructors, `@inject` and middleware ([Dependency Injection](dependency-injection.md)) |
| `Theme` | Named styles for `class="…"` ([themes](c-view-markup.md#themes-and-class)) |
| `ClearScreenBetweenViews` | Clear the console before each view |
| `ApplicationAssembly` | Where controllers and views are discovered |
| `Renderer` | How markup is drawn: colour support (`NO_COLOR`, `COLORTERM`, Windows VT mode), terminal width and redirected output are detected |
| `Input` | Where `<input>` and `<menu>` read from |

`Build()` validates the service registrations, so a missing dependency fails at start-up rather than on the screen
that needs it.

## Testing

Drive a whole application with scripted input and capture plain-text output:

```csharp
sealed class ScriptedInput(params string[] lines) : ITextInput
{
    private readonly Queue<string> _lines = new(lines);
    public string? ReadLine() => _lines.Count > 0 ? _lines.Dequeue() : null;   // null = end of input
    public string? ReadMasked(char mask) => ReadLine();
}

var output = new StringWriter();
var builder = MvcApplication.CreateBuilder([]);
builder.ApplicationAssembly = typeof(HomeController).Assembly;
builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(output), () => 60);
builder.Input = new ScriptedInput("2", "Ada", "teal", "s", "q");
builder.Services.AddSingleton<IGreetingService, FakeGreetings>();

using var app = builder.Build();
app.Run();

Assert.Contains("Hello, Ada!", output.ToString());
```

At the end of the scripted input, menus quit instead of waiting, so the run always finishes. Controllers can also be
tested on their own: create one with `Router.ResolveController(name, services)`, or construct it directly and check
the `ViewResult` it returns.

## Live reload

Run `dotnet watch` during development; see [Getting Started](getting-started.md#live-reload).
