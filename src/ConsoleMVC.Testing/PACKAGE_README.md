# ConsoleMVC.Testing

End-to-end tests for [ConsoleMVC](https://github.com/MateuszPodeszwa/ConsoleMVC) applications. Run the application
with scripted input, capture every screen as plain text, check the routes it visited, and compare screens with
snapshot files. Works with xUnit, NUnit and MSTest.

```
dotnet add package ConsoleMVC.Testing
```

```csharp
using ConsoleMVC.Testing;

[Fact]
public void Greeting_ShowsTheName()
{
    var host = MvcTestHost.For<HomeController>(builder =>
        builder.Services.AddSingleton<IGreetingService, FakeGreetings>());

    var run = host.Run(new ScriptedInput("2", "Ada")       // menu choice, then a name
        .Keys(ConsoleKey.DownArrow, ConsoleKey.Enter)      // a <select> answered with the arrow keys
        .Line("n")
        .Line("q"));

    Assert.Equal(["Home/Index", "Greet/Index", "Greet/Result"], run.Routes);
    Snapshot.Match(run.Screens[2], "greeting-result");     // __snapshots__/greeting-result.txt beside this file
}
```

| Type | Purpose |
|---|---|
| `MvcTestHost` | Builds a fresh application per run with a fixed `Width` (80), the `Testing` environment and a `MaxScreens` guard (100) against applications that never wait |
| `TestRun` | `Screens`, `LastScreen`, `Output`, `Routes` and `RemainingInput` |
| `ScriptedInput` | Lines (`Line`, `Lines`) and keys (`Keys`, `Key`, `Char`); fields use the arrow keys exactly when the next step is a key |
| `ScreenRecorder` | The render target behind the host, one entry per screen; usable with your own `MarkupRenderer` |
| `Snapshot` | `Snapshot.Match(text, "name")`: creates the file on the first run, fails with the first different line afterwards, never creates one when `CI` is set; `CONSOLEMVC_UPDATE_SNAPSHOTS=1` accepts changes |

The guide is at [Testing](https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/guides/docs/testing.md).
