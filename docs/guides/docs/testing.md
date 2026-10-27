# Testing

The `ConsoleMVC.Testing` package runs a whole application in a test: it types the user's input from a script,
records every screen as plain text, and compares screens with snapshot files kept beside your tests. It works with
xUnit, NUnit and MSTest.

```
dotnet new xunit -n MyApp.Tests
dotnet add MyApp.Tests reference MyApp
dotnet add MyApp.Tests package ConsoleMVC.Testing
```

## Running the application

```csharp
using ConsoleMVC.Testing;

public class GreetingTests
{
    private static MvcTestHost Host() => MvcTestHost.For<HomeController>(builder =>
    {
        builder.Services.AddSingleton<IGreetingService, GreetingService>();
        builder.Services.Configure<GreetingOptions>(o => o.Salutation = "Hi");
    });

    [Fact]
    public void Greeting_ShowsTheName()
    {
        var run = Host().Run("2", "Ada", "3", "n", "q");

        Assert.Equal(["Home/Index", "Greet/Index", "Greet/Result"], run.Routes);
        Assert.Contains("Hi, Ada!", run.LastScreen);
    }
}
```

`MvcTestHost.For<T>()` finds the controllers and views in the assembly of `T`. The callback configures the builder
as `Program.cs` does, but `Program.cs` itself does not run: register the services the screens need, with fakes where
you want them. Each `Run` builds a fresh application, so tests do not share state.

| Setting | Default | Purpose |
|---|---|---|
| `Width` | 80 | The terminal width screens are laid out to |
| `MaxScreens` | 100 | Fails a run that keeps navigating without waiting for input, instead of hanging |
| `EnvironmentName` | `Testing` | `builder.Environment`, so `appsettings.Testing.json` is read when it is copied to the test output |

`TestRun` holds what the run produced:

| Member | Value |
|---|---|
| `Screens` | Every screen, oldest first: the framework clears the console before each view, and each clear starts a new entry |
| `LastScreen` | The final screen |
| `Output` | All screens one after another |
| `Routes` | `Controller/Action` for every navigation cycle, redirects included |
| `RemainingInput` | Script steps the application never read |

When the script ends, menus quit, so a run always finishes.

## Scripting lines and keys

`ScriptedInput` holds typed lines and key presses in order:

```csharp
var input = new ScriptedInput("2", "Ada")               // lines: a menu choice, then a name
    .Keys(ConsoleKey.DownArrow, ConsoleKey.Enter)       // a <select> answered with the arrow keys
    .Key(ScriptedInput.Char('y'))                       // a <checkbox> answered with y
    .Line("q");

var run = Host().Run(input);
```

A `<select>`, `<radio>` or `<checkbox>` uses the arrow keys when the next step of the script is a key, and a typed
answer when it is a line, so both ways can be tested. A step of the wrong kind (a key where the application reads a
line) fails with a message naming both.

## Snapshots

`Snapshot.Match` compares text with a file in a `__snapshots__` folder beside the test file:

```csharp
Snapshot.Match(run.LastScreen, "greeting-result");     // __snapshots__/greeting-result.txt
```

| Situation | Result |
|---|---|
| The file does not exist | It is created and the test passes; commit it |
| The file does not exist and `CI` is set (build servers) | The test fails, so a snapshot is never accepted unseen |
| The text is the same | The test passes (line endings are ignored) |
| The text differs | The test fails with the first different line, and the new text is saved as `greeting-result.received.txt` beside the snapshot |
| `CONSOLEMVC_UPDATE_SNAPSHOTS=1` | Differences replace the snapshot: `CONSOLEMVC_UPDATE_SNAPSHOTS=1 dotnet test` |

Snapshot names may contain letters, digits, `.`, `-` and `_`, so a snapshot is never read or written outside its folder.
Add `*.received.txt` to `.gitignore`.

## Assembling a run yourself

`ScreenRecorder` and `ScriptedInput` also work without `MvcTestHost`, for example to keep `Program.cs`'s own builder
settings or to render one application at several widths:

```csharp
var recorder = new ScreenRecorder();
var builder = MvcApplication.CreateBuilder([]);
builder.ApplicationAssembly = typeof(HomeController).Assembly;
builder.Renderer = new MarkupRenderer(recorder, () => 60);
builder.Input = new ScriptedInput("q");

using var app = builder.Build();
app.Run();
```

Controllers can also be tested on their own: create one with `Router.ResolveController(name, services)`, or construct
it directly and check the `ViewResult` it returns.
