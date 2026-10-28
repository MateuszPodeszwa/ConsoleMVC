# Accessibility

ConsoleMVC has two modes that users can turn on without a new build of your application.

## Screen readers

```bash
dotnet run -- --ConsoleMVC:ScreenReader=true
# or
export ConsoleMVC__ScreenReader=true
```

Or always, in `Program.cs`:

```csharp
builder.ScreenReader = true;
```

Every screen is then written as plain lines of text that a screen reader reads well:

| Element | Read as |
|---|---|
| `<box title="Account">` | `Account`, then the box's content |
| `<table>` | one line per row: `Name: Ada, Age: 36` |
| `<columns>` | each column, one after another |
| `<separator />` | nothing |
| `<progress>` | `Import: 75%` |
| `<spinner>` | its label, then `Connected: done` |
| `<bar-chart>` | `Mon: 12` for each bar |
| `<sparkline>` | `Load: 30 values from 10 to 89, latest 14` |
| `<shortcuts />` | `Shortcuts: Esc Back, Ctrl+X Quit` |
| `<breadcrumbs />` | `You are here: Home, Orders` |
| `<markdown>` | without rules; quotes start with `Quote:` |

Also:

- No colours, no styles and no cursor movement, even with your own `builder.Renderer`. `<select>`, `<radio>`, `<checkbox>`, `<number>` and `<date>` list numbered
  choices and read a typed answer instead of the arrow keys.
- Screens are not erased: a blank line separates them, so what was read stays in the reader's history.
- Live regions draw their final state once, `<scroll>` draws everything, and `FullScreen` is ignored.
- Screens do not refresh (`@refresh`, `RefreshEvery`): the reader would read the whole page out again each time.
  Leave the page and come back for fresh data.

Your own elements are drawn as they are. To describe one in words, wrap the renderer yourself, or check
`Renderer is ScreenReaderRenderer` in a view and draw something simpler.

## High contrast

```bash
dotnet run -- --ConsoleMVC:HighContrast=true
```

```csharp
builder.HighContrast = true;            // or: builder.Theme = Theme.CreateHighContrast();
```

The built-in classes (`title`, `muted`, `success`, `warning`, `danger`, `info`, `panel`) use bold, bright colours with
no dim text, and panels get heavy white borders. High contrast replaces `builder.Theme`, so add your own classes to
`Theme.CreateHighContrast()` if you need them in both.

`NO_COLOR` (https://no-color.org) is respected as before: with it set, nothing is coloured.
