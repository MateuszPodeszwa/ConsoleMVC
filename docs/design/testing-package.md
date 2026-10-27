# Design: ConsoleMVC 1.18: The ConsoleMVC.Testing package

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.18.0 |
| **Scope** | `MvcTestHost`, `TestRun`, `ScriptedInput`, `ScreenRecorder`, `Snapshot` (NuGet: `ConsoleMVC.Testing`) |

---

## 1. Goal

Test a whole console application the way a user drives it, with any test framework, and keep the screens it draws
under version control:

```csharp
[Fact]
public void Greeting_ShowsTheName()
{
    var host = MvcTestHost.For<HomeController>(builder =>
        builder.Services.AddSingleton<IGreetingService, FakeGreetings>());

    var run = host.Run(new ScriptedInput()
        .Line("2")                                        // Greeting form
        .Line("Ada")
        .Keys(ConsoleKey.DownArrow, ConsoleKey.Enter)     // <select> with the arrow keys
        .Line("n")
        .Line("q"));

    Assert.Equal(["Home/Index", "Greet/Index", "Greet/Result"], run.Routes);
    Snapshot.Match(run.Screens[2], "greeting-result");     // __snapshots__/greeting-result.txt
}
```

## 2. Classes (UML)

```
MvcTestHost                                         TestRun
  + For<TMarker>(configure?) : MvcTestHost            + Screens : IReadOnlyList<string>   one per cleared screen
  + For(assembly, configure?) : MvcTestHost           + LastScreen : string
  + Width : int = 80                                  + Output : string                   everything drawn
  + MaxScreens : int = 100                            + Routes : IReadOnlyList<string>    "Controller/Action" per cycle
  + EnvironmentName : string = "Testing"              + RemainingInput : int              script steps not read
  + Run(params string[] lines) : TestRun
  + Run(input : ScriptedInput) : TestRun
  + RunAsync(input, cancellationToken) : Task<TestRun>

ScriptedInput : ITextInput                          ScreenRecorder : IRenderTarget
  + ScriptedInput(params string[] lines)              + Screens, Output
  + Line(text) / Lines(...) / Keys(...) / Key(info)   + Clear() starts a new screen
  + CanReadKeys ⇐ the next step is a key              + TryEraseLines ⇒ true (arrow-key fields work)
  + Remaining : int

Snapshot (static)                                   SnapshotMismatchException : Exception
  + Match(actual, name, [CallerFilePath])             + Expected, Actual, SnapshotPath, ReceivedPath
```

## 3. Rules (pseudocode)

```
ScriptedInput: a queue of steps, each a line or a key
    CanReadKeys = next step is a key       (so <select> picks arrow-key or typed mode from the script itself)
    ReadLine: next is a line → it; next is a key → InvalidOperationException naming both; empty → null
    ReadKey:  the same the other way round
    end of script → null, which makes menus quit, so a run always finishes

MvcTestHost.Run(input):
    builder ← CreateBuilder(environment "Testing", the application assembly)
    builder.Renderer ← MarkupRenderer(ScreenRecorder, width), builder.Input ← input, then configure(builder)
    middleware records each route; more than MaxScreens cycles → InvalidOperationException (a loop that never asks)
    run; return TestRun(screens, output, routes, input.Remaining)

Snapshot.Match(actual, name):
    name must be letters, digits, '.', '-', '_' (no paths)
    file ← <folder of the calling test file>/__snapshots__/<name>.txt; line endings are compared as \n
    file missing:  CI set → write <name>.received.txt, throw; otherwise create it and pass
    different:     CONSOLEMVC_UPDATE_SNAPSHOTS=1 → overwrite, pass; otherwise write .received.txt and throw
                   SnapshotMismatchException with the first differing line
    equal:         delete a stale .received.txt
```

New snapshots are never accepted silently on a build server, and a failed comparison leaves the new output next to
the old one for a diff tool.
