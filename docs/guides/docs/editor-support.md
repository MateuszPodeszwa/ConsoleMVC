# Editor Support

ConsoleMVC gives editors two things to build on, both made from the framework's own code so they always agree with
the build:

- **`cvw-preview`**, a .NET tool that draws a `.cvw` view with the real rendering engine, without building or running
  the application.
- **`schema/cvw-schema.json`**, a description of every element, attribute, value, directive and diagnostic of C-View
  Markup, with documentation.

The [ConsoleMVC-CVWSupport](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport) plugins for Rider and VS Code use
both for highlighting, completion, hover documentation and a live preview panel.

## Previewing a view

Install the tool once:

```bash
dotnet tool install -g ConsoleMVC.Preview
```

Then draw any view of a project:

```bash
cvw-preview Views/Home/IndexView.cvw
```

```text
Home › Page                                                 ConsoleMVC
╭─ {Model.Title} ───╮
│  {Model.Message}  │
╰───────────────────╯

──────────────────────────────────────────────────────────────────────
Esc Back  Ctrl+X Quit
[1] About
[2] Greeting form
[3] Calculator
[4] Feature tour (tables)
[q] Exit
Select an option:
```

The layout's separator and shortcuts come before the menu, as in the application: the part of a layout after
`<body />` is drawn just before the view first waits for the user.

| Option | Meaning |
|---|---|
| `--width N` | The width in columns: a whole number from 20 to 500 (default 80). Any other value is an error |
| `--format text` | Plain text (the default) |
| `--format ansi` | Text with colours, for a terminal |
| `--format html` | An HTML fragment: a `<pre class="cvw-screen">` with a styled `<span>` per run of one style, followed by a `<ul class="cvw-problems">` when there are problems |
| `--stdin` | Reads the view's text from standard input; the file name only says where the view lives, so its layout and partial views can be found. Editors send their unsaved text this way |
| `--high-contrast` | Draws with the [high-contrast theme](accessibility.md) |
| `--help` | Shows the usage |

A value can follow its option or be joined to it with `=`: `--width 100` and `--width=100` are the same, and so are
`--format html` and `--format=html`. Options are written in lower case.

The exit code is 0 when the view was drawn (even with diagnostics), 1 for wrong arguments and 2 when the file cannot
be read. Wrong arguments are explained on standard error, followed by the usage:

```text
cvw-preview: --width needs a whole number from 20 to 500, not '5000'.
```

### What the preview shows

The tool reads markup, not C#, so a preview is a sketch of the screen rather than a real run:

| In the view | In the preview |
|---|---|
| `@Model.Title`, `@(a + b)` | A placeholder: `{Model.Title}`, `{a + b}` |
| `title="Order @Model.Id"` | The text with a placeholder: `Order {Model.Id}` |
| `@foreach`, `@if`, `@{ … }` | The code is skipped; elements inside loops are drawn once, and both branches of an `if` are drawn |
| `values="@Model.Load"` on a chart, `value="@…"` on `<progress>` or `<bar>` | Sample numbers, so the chart or bar is visible |
| `<partial name="Header" />` | The partial view from `Views/Shared` (an area's own `Shared` folder first), drawn in place |
| The view's layout (`_Layout`, or `@layout Name`) | The view is drawn inside it, as in the application: the part before `<body />` first, the part after it just before the view's first `<input>`, `<number>`, `<date>`, `<select>`, `<radio>`, `<checkbox>` or `<menu>` (or at the end when there is none). `@layout none` draws the view alone |
| A view in `Views/Shared` | Drawn on its own; `<body />` shows as `[the page's content]` |
| `<menu>`, `<input>`, `<number>`, `<date>`, `<select>`, `<radio>`, `<checkbox>` | Exactly what the application draws in a terminal when the field first waits for its answer (see below) |
| `<errors />` | A sample message, `{validation message}` |
| `<breadcrumbs />`, `<shortcuts />` | Sample items |
| `class="…"` | The [default theme](c-view-markup.md#themes-and-class) (or the high-contrast one) |

Each field and menu is drawn by the framework's own code, as a terminal first shows it, before the user presses a key:

```text
Colour: (Up/Down, Enter)
   Red
 > Green
   Blue
[x] Send me news  (Space, Enter)
Count: 1  (Up/Down, type a number, Enter)
Due: 2026-11-04 Wed  (Left/Right, Up/Down, t today, Enter)
Name:
```

- A `<select>` or `<radio>` highlights its `default` (ticks for `<select multiple>`), a `<checkbox checked>` is ticked,
  and a `<number>` or `<date>` shows its `default`. A `<date>` without one, or whose `default` is an expression, shows
  today.
- An `<input>` shows its label and a `<menu>` its options and prompt, as in a terminal. The line ends after the prompt,
  where the user would press Enter.
- The application waits at its first field; the preview shows every field of the view, each as it is first drawn.
- When the input is piped, or the console cannot move the cursor, the application asks differently (numbered choices,
  `Choose 1-3 [Green]:`, `Send me news? [Y/n]:`); the preview always shows the terminal.

Something the preview cannot draw, such as a partial view whose name is an expression, shows as a dim note in brackets.
A partial view or layout that was not found, or an attribute value an element refuses, is also listed with the
problems.

### Diagnostics

The view is parsed by the generator's own parser, so the preview reports exactly the markup errors the build would,
with their line and column:

```text
3:3: error CVW105: Unknown element <blink>. Known elements: <text>, <box>, …
```

In text and ANSI output they go to standard error; in HTML they follow the screen as
`<li class="cvw-error" data-line="3" data-column="3">`. C# errors are not checked, since no C# is compiled.

### Safety

The tool runs no code from the project and loads none of its assemblies. It reads the view, and only the shared views
it names in `Views/Shared` (and the area's `Views/Shared`). Partial and layout names must be plain names (letters,
digits and underscores), so a view cannot make the tool read another file, and a shared view larger than 1 MB is not
read: the preview draws a note in its place and says that the file is too large. All text in the HTML output is encoded, so a view cannot add markup to an editor's preview panel.

## The markup schema

[`schema/cvw-schema.json`](https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/schema/cvw-schema.json) is written
from the generator's schema, and a test fails when the file and the generator disagree. It contains:

- `directives`: `@model`, `@using`, `@inject`, `@layout` and `@refresh`, with their syntax and description.
- `constructs`: `@expression`, `@( … )`, `@{ … }`, `@if`, `@foreach`, comments and escapes.
- `elements`: every element with its description, its content (`void`, `inline`, `block` or `elements`), where it may
  appear (`parents`, `children`, `topLevelOnly`) and its attributes. Each attribute has a description, whether it is
  `required`, a `flag` or allows `@expressions`, its `kind` (`text`, `integer`, `number`, `number-list`, `date`,
  `boolean`, `colour`, `length`, `choice`, `route` or `model`) and, where it makes sense, the `values` to offer and a
  `minimum` and `maximum`.
- `diagnostics`: every `CVWxxx` diagnostic with its severity and title.

`formatVersion` changes only when the meaning of a field changes; new fields may be added at any time, so a reader
should ignore fields it does not know.

To regenerate the file after changing the generator's schema (maintainers):

```bash
UPDATE_SCHEMA=1 dotnet test tests/ConsoleMVC.Generators.Tests
```

## Editors

- **JetBrains Rider**: the [ConsoleMVC CVWSupport](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport)
  plugin.
- **VS Code**: the extension in [ConsoleMVC-CVWSupport](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport).

Both look for `cvw-preview` on the `PATH` for their preview; install it as above. Without a plugin, associating
`*.cvw` with C# gives basic highlighting (see [IDE support](https://github.com/MateuszPodeszwa/ConsoleMVC#IDESupport)
in the readme).

See the [design](https://github.com/MateuszPodeszwa/ConsoleMVC/blob/main/docs/design/editor-support.md) for how the
tool and the schema work.
