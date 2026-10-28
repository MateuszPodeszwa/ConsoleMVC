# Design: ConsoleMVC 1.12: Navigation history

| | |
|---|---|
| **Author** | Mateusz Podeszwa |
| **Status** | Implemented in 1.12.0 |
| **Scope** | `NavigationResult.Back()`, `<option back>`, `ConsoleViewBase.NavigationHistory` and `Breadcrumbs`, `<breadcrumbs />` |

---

## 1. Goals

1. A "Back" option that returns to the previous screen, without every view hard-coding where it came from.
2. Show the user where they are: `Home › Orders › Details`.

## 2. Rules (pseudocode)

```
history: List<route>, oldest first; the last entry is the screen on display (at most 50)

before a view renders for route R (the controller's declared action name):
    same(a, b) = same controller and action (case-insensitive) and the same route values   # 1.34: values count
    i ← last index where same(history[i], R)
    if i found: history ← history[0 .. i-1]   # a form shown again, a refresh, or a screen already in the trail
    history.add(R)

after the view returns navigation N:
    N.IsBack → if history.Count > 1: history.removeLast()
               next route ← history.last (without form data; it is then "the same screen" and replaces itself)
    otherwise next route ← N as before

redirects never render, so they are never screens.
```

`Breadcrumbs` labels each entry with its controller for an `Index` action, else the action (`Home`, `Orders`,
`Details`). `<breadcrumbs />` joins them with `separator` (default ` › `), draws the current one in bold, and when the
trail is too wide replaces the oldest items with `…`, so the current screen always shows.

Back on the first screen shows it again rather than quitting, so a stray "back" never closes the application.
