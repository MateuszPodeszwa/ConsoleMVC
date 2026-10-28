# Design: ConsoleMVC 1.23: Keyboard shortcuts

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.23.0 |
| **Scope** | `app.MapKey`, `KeyGesture`, `KeyShortcut`, `<shortcuts />`, `ShortcutHint` |

---

## 1. Goal

Keys that work on every screen, such as F1 for help or Esc for back, without each view handling them:

```csharp
app.MapKey(ConsoleKey.F1, "Help/Index", "Help");
app.MapKey(new KeyGesture(ConsoleKey.Escape), NavigationResult.Back(), "Back");
app.MapKey(KeyGesture.Parse("Ctrl+X"), NavigationResult.Quit(), "Quit");
```

```
<shortcuts class="muted" />          F1 Help  Esc Back  Ctrl+X Quit
```

## 2. The problem and the approach

Menus and text fields read whole lines (`Console.ReadLine`), so the application never sees F1. When at least one
shortcut is mapped, the application wraps its input in `ShortcutInput`:

```
ShortcutInput.ReadLine():  input can read keys? read keys with LineEditor (typing, Backspace, Delete, Left/Right,
                           Home/End, Enter; echo through ForeignOutputTracker.Suppress), checking each key
                           otherwise (piped input): the inner ReadLine, no shortcuts
ShortcutInput.ReadMasked(): the same with MaskedLineEditor
ShortcutInput.ReadKey():   the inner key, checked (so arrow-key fields notice shortcuts too)

a key matches a shortcut → throw ShortcutPressedException(navigation)
    the exception leaves the view wherever it waits (its code is not resumed)
    the application loop catches it around the view and follows the navigation (Back, Quit, a route)
```

No shortcut mapped: nothing is wrapped, and input behaves exactly as before.

## 3. Rules

```
MapKey(key | gesture, target | NavigationResult | Func<NavigationResult>, description?)
    allowed: F1 to F24, Esc, Insert, or any key with Ctrl or Alt
    refused: keys typing and fields need (letters, digits, arrows, Enter, Space, Tab, Backspace, Home, End, PageUp/Down),
             Ctrl+C (interrupt), Ctrl+D and Ctrl+Z (end of input), Ctrl+Q and Ctrl+S (terminal flow control)
    refused: a gesture mapped twice; mapping after the application first ran
    target strings are checked like nav (Controller/Action?values)
views: ShortcutHints = the shortcuts with a description, as "F1" / "Help"; <shortcuts /> lays them out on one line,
       leaving out from the end what does not fit
Esc takes precedence over a field's own Esc (clearing a <select filter>) when it is mapped
```

## 4. Testing

`ScriptedInput` reports keys when its next step is a key, so a script can press a shortcut where a line is read:
`new ScriptedInput().Keys(ConsoleKey.F1)`. `MvcTestHost.ConfigureApplication` maps shortcuts (and middleware) on the
built application, as `Program.cs` does.
