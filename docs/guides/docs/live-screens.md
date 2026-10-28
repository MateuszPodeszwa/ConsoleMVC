# Live Screens

A screen is normally drawn once and then waits for the user. ConsoleMVC has two ways to keep it moving:

- **Live regions** redraw a `<progress>` bar or a `<spinner>` in place while an action works.
- **Timed refresh** draws a view again every few seconds, with fresh data, until the user starts typing.

## Progress while an action works

An action runs before its view is drawn, so a long one leaves the user looking at the previous screen. Show what it is
doing with `ILiveDisplay`, which the framework registers for you:

```csharp
public class ImportController(ILiveDisplay live, IImporter importer) : Controller
{
    public async Task<ActionResult> Run(CancellationToken cancellationToken)
    {
        var progress = new ProgressElement { Label = "Import ", Max = importer.Count };

        await using (var region = live.Start(progress))
        {
            await foreach (var _ in importer.ImportAsync(cancellationToken))
                region.Update(() => progress.Value++);
        }

        return View();
    }
}
```

```
Select an option: 1
Import ██████████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  40%
```

- `Start` draws the element, and `Update` changes it and redraws it in the same step.
- The region also redraws on a timer (every 100 ms; pass another interval to `Start`), which is what turns spinners.
  Changes made without `Update` show at the next tick, or straight away with `region.Refresh()`.
- Disposing the region draws the element one last time. It stays on the screen until the next view clears it.
- Draw nothing else while a region is live: the region replaces the last element drawn.
- When the output is a pipe or a file, lines cannot be redrawn, so the element is drawn once, in its final state, when
  the region ends. Logs then show `Import ████████ 100%` rather than every step.

Any element can be live, including a `<box>` with several bars in it. A spinner shows that something is happening when
there is nothing to count:

```csharp
var spinner = new SpinnerElement { Label = "Connecting" };
await using (var region = live.Start(spinner))
{
    await client.ConnectAsync(cancellationToken);
    region.Update(() => { spinner.Label = "Connected"; spinner.Done = true; }); // ✓ Connected
}
```

Spinners, and any element of your own that implements `IAnimatedElement`, move to their next frame at each tick, also
inside containers.

## Refreshing a view on a timer

A dashboard should show fresh numbers without waiting for a key. Add `@refresh` to the view:

```
@model MyApp.Models.StatusViewModel
@refresh 5s

<text>Queue length: @Model.QueueLength</text>
<progress label="Disk " value="@Model.DiskUsed" max="@Model.DiskSize" />

<menu>
    <option key="b" back>Back</option>
</menu>
```

Every 5 seconds the action runs again (so it reads fresh data) and the screen is cleared and drawn again.

- **Refresh stops while the user answers.** As soon as a key is pressed on the screen, it waits for the user like any other.
- The interval is a number with `ms`, `s` or `m` (`500ms`, `5s`, `1.5s`, `2m`), from 100 ms to 24 hours. Anything
  else, or `@refresh` with no interval, is build error CVW010.
- Only views refresh: a partial view or a layout with `@refresh` is CVW010 too.
- A refresh shows the same screen again, so it does not add to the [navigation history](controllers-and-forms.md):
  Back still goes to the screen before.
- When input is piped (not a terminal), the application cannot tell whether the user is typing, so screens do not refresh.

The action can choose instead, and it wins over the view's `@refresh`:

```csharp
public ActionResult Status() => View(LoadStatus()).RefreshEvery(TimeSpan.FromSeconds(2));

public ActionResult Report() => View(LoadReport()).NoRefresh(); // the view's @refresh is turned off
```

To make a spinner turn on a refreshing screen, give it a frame that changes, for example `frame="@DateTime.Now.Second"`.

## Testing

`ScriptedInput.Wait()` is a pause in which the user types nothing: a refreshing screen uses it up and refreshes.

```csharp
var run = host.Run(new ScriptedInput("4").Wait().Wait().Line("q"));

Assert.Equal(["Home/Index", "Status/Index", "Status/Index", "Status/Index"], run.Routes);
```

A live region in an action draws into the test's screens like it would on a terminal, so `run.Output` holds the final
state of the bar. See [Testing](testing.md).

## Your own input

The framework asks the input whether the user has started typing through `ITextInput.WaitForInput(timeout)`. The
default implementation returns `true` straight away (go ahead and read), so an input that does not implement it never
refreshes a screen. Return `false` when the time passes with nothing typed.
