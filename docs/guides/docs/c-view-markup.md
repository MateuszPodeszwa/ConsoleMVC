# C-View Markup

C-View Markup is a small markup language for describing console screens inside `.cvw` views — text, boxes, tables, form inputs and menus. It compiles to plain C# at build time — there is no runtime template parsing — and it mixes freely with ordinary C# code.

```
@model MyApp.Models.DashboardViewModel

<box title="Dashboard" border="double" color="cyan" width="60">
    <text align="centre" bold>Welcome, @Model.UserName!</text>
    <separator />
    @foreach (var notification in Model.Notifications)
    {
        <text>• @notification</text>
    }
</box>

Console.Write("Press Enter to continue...");
Console.ReadLine();
return NavigationResult.ToAction("Index");
```

Output (colours omitted):

```
╔═ Dashboard ══════════════════════════════════════════════╗
║                      Welcome, Ada!                       ║
║ ──────────────────────────────────────────────────────── ║
║ • Your report is ready                                   ║
║ • 3 new messages                                         ║
╚══════════════════════════════════════════════════════════╝
Press Enter to continue...
```

!!! note "Backwards compatible"
    Views written in plain C# keep working unchanged. Markup is only recognised where a C# statement could begin — at the start of the body or after `;`, `{`, `}` or `:` — when `<` is directly followed by a known tag name. C# can never start a statement with `<`, so comparisons and generics such as `a < b` or `List<string>` are never mistaken for markup.

## How markup and code interact

* Top-level elements are rendered **immediately, in source order**. A `<box>` written before `Console.ReadLine()` appears before the prompt.
* The view still returns a `NavigationResult` from C# code.
* Inside an element, `@` switches to C#:

| Syntax | Meaning |
|---|---|
| `@Model.Name`, `@items[0]`, `@user?.GetName()` | Implicit expression |
| `@(a + b)` | Explicit expression (use for anything with spaces or operators) |
| `@if (…) { … } else { … }` | Conditional content (also `@foreach`, `@for`, `@while`, `@switch`, `@do`, `@try`, `@lock`) |
| `@{ var total = 0; }` | Code block |
| `@@` | A literal `@` |
| `name@example.com` | `@` after a letter or digit is literal, so e-mail addresses just work |
| `@* comment *@`, `<!-- comment -->` | Comments, removed at compile time |
| `&lt; &gt; &amp; &quot; &apos; &nbsp; &at; &#64;` | Character entities |

Control flow and code blocks are allowed in containers such as `<box>` but not inside `<text>`; use an expression there instead, for example `@(Model.IsAdmin ? "admin" : "user")`.

### Preprocessor directives

`#region`, `#if`/`#else`/`#endif`, `#pragma` and other directive lines may be used in view code. Markup on the line after a directive is recognised as usual, so `#if DEBUG` around a `<text>` element works.

### Reserved names

Generated code declares local variables starting with `__cvw_`; do not use that prefix for your own variables in views.

### Whitespace

Text inside `<text>` follows HTML rules: runs of spaces and new lines collapse into a single space, and whitespace at the start and end is removed. Use `<br />` for a line break. Values inserted with `@expressions` keep their own spaces and line breaks.

### Security

Every value inserted into the screen is sanitised. Control characters, ANSI escape sequences and Unicode bidirectional overrides are removed, so model data typed by users cannot clear the screen, recolour the terminal or disguise text.

## Elements

### `<text>`

A block of text that wraps to the available width.

| Attribute | Values | Default |
|---|---|---|
| `color` / `colour` | a [colour](#colours) | terminal default |
| `background` | a [colour](#colours) | terminal default |
| `align` | `left`, `centre` (or `center`), `right` | `left` |
| `bold`, `dim`, `italic`, `underline` | flag, or `true`/`false` | off |
| `wrap` | `true` to word-wrap, `false` to truncate with `…` | `true` |
| `class` | [theme](#themes-and-class) classes, separated by spaces | none |

```
<text color="yellow" bold>Warning:</text>
<text align="right" dim>Page @Model.Page of @Model.PageCount</text>
```

### `<box>`

A bordered panel. Its children are stacked vertically; text written directly inside a box becomes a `<text>` element automatically.

| Attribute | Values | Default |
|---|---|---|
| `title` | text, may contain `@expressions` | none |
| `width` | `auto`, a column count (`40`), or a percentage (`50%`) | `auto` (fit the content) |
| `border` | `single`, `double`, `rounded`, `heavy`, `ascii`, `none` | `single` |
| `padding` | columns between border and content (0–20) | `1` |
| `color` / `colour` | border colour | terminal default |
| `title-color` / `title-colour` | title colour | border colour |
| `min-width` / `max-width` | column counts (0–1000) limiting the width | none |
| `class` | [theme](#themes-and-class) classes | none |

Boxes never grow wider than the terminal; content wraps to fit. `min-width` widens a small box and `max-width` stops a `width="100%"` box growing on a wide terminal; the terminal width always wins.

### `<separator />`

A horizontal rule that fills its container (or the terminal at top level).

| Attribute | Values | Default |
|---|---|---|
| `style` | `single`, `double`, `heavy`, `dashed`, `ascii` | `single` |
| `color` / `colour` | a [colour](#colours) | terminal default |
| `width` | `auto` (fill), a column count, or a percentage | `auto` |
| `class` | [theme](#themes-and-class) classes (the text colour is used) | none |

### `<br />`

One or more empty lines between blocks, or a line break inside `<text>`.

| Attribute | Values | Default |
|---|---|---|
| `count` | number of line breaks (0–100) | `1` |

### `<table>`, `<row>` and `<cell>`

A grid whose columns size themselves to the content. When the table is wider than the terminal, the widest columns shrink and their cells wrap. Rows are usually generated with `@foreach`.

```
<table border="rounded">
    <row header>
        <cell>Name</cell>
        <cell align="right">Age</cell>
    </row>
    @foreach (var user in Model.Users)
    {
        <row>
            <cell>@user.Name</cell>
            <cell align="right">@user.Age</cell>
        </row>
    }
</table>
```

```
╭───────┬─────╮
│ Name  │ Age │
├───────┼─────┤
│ Ada   │  36 │
│ Linus │  54 │
╰───────┴─────╯
```

| Element | Attribute | Values | Default |
|---|---|---|---|
| `<table>` | `border` | `single`, `double`, `rounded`, `heavy`, `ascii`, `none` | `single` |
| | `padding` | blank columns on each side of every cell (0–20) | `1` |
| | `width` | `auto`, a column count, or a percentage; a larger width widens the last column | `auto` |
| | `color` / `colour` | grid colour | terminal default |
| | `min-width` / `max-width` | column counts limiting the width | none |
| | `class` | [theme](#themes-and-class) classes | none |
| `<row>` | `header` | flag: bold cells (unless a cell sets its own decoration) followed by a separator line | off |
| `<cell>` | same as `<text>` | `align`, `color`, `background`, `bold`, `dim`, `italic`, `underline`, `wrap`, `class` | |

A `<table>` may only contain `<row>` elements, and a `<row>` only `<cell>` elements; loops and conditions around them are fine. Rows with fewer cells are padded with empty cells.

### `<columns>` and `<column>`

Places content side by side. Each `<column>` holds block content, like a box without a border.

```
<columns gap="2" stack-below="60">
    <column width="30%">
        <box class="panel" title="Menu"><text>[1] Orders</text></box>
    </column>
    <column>
        <text class="title">Orders</text>
        <text>@Model.Summary</text>
    </column>
</columns>
```

| Element | Attribute | Values | Default |
|---|---|---|---|
| `<columns>` | `gap` | spaces between columns (0–20) | `2` |
| | `stack-below` | a width in columns: narrower terminals stack the columns vertically | never stack |
| `<column>` | `width` | `auto`, a column count, or a percentage of the width after the gaps | `auto` |

* Fixed and percentage columns take their width first; `auto` columns use their content's natural width when it fits. Otherwise narrow columns keep their width and wide ones share the rest equally, wrapping their text.
* Columns can be generated with `@foreach`, nested in a `<box>`, and contain any block elements, including other columns.
* `<columns>` may only contain `<column>` elements, and `<column>` only appears inside `<columns>`.

### `<input>`

Prompts for a value and stores it in the view's `Form` dictionary under its `name`. Post the form to a controller action, where it is bound to parameters or a model just like before:

```
<input name="Name" label="Name: " required />
<input name="Password" label="Password: " masked />
<input name="Colour" label="Favourite colour: " default="blue" />

return NavigationResult.ToAction("Result", Form);
```

| Attribute | Values | Default |
|---|---|---|
| `name` | **required** — the key in `Form` (matched case-insensitively when binding) | |
| `label` | the prompt written before reading | none |
| `default` | used when the user enters nothing | empty |
| `required` | flag: ask again until a value is entered | off |
| `masked` | flag: hide typed characters (shows one `*` per character); for passwords and other secrets. Backspace works, and Ctrl+D (or Ctrl+Z on Windows) on an empty line ends the input | off |
| `color` / `colour` | label colour | terminal default |

Values are trimmed. `Form` is also available to ordinary C# in the view, for example `int.Parse(Form["Age"])`.

### `<menu>` and `<option>`

Shows keyed choices, reads the user's choice, repeats until it is valid, and navigates. A menu replaces the usual "write the options, `Console.ReadLine()`, `switch`" code, so it **ends the view** — no `return` is needed.

```
<menu key-color="cyan">
    <option key="1" nav="Home/About">About</option>
    <option key="2" nav="Search" post>Search with the form above</option>
    <option key="q" quit>Exit</option>
</menu>
```

```
[1] About
[2] Search with the form above
[q] Exit
Select an option: _
```

| Element | Attribute | Values | Default |
|---|---|---|---|
| `<menu>` | `prompt` | the prompt text | `Select an option: ` |
| | `error` | message after an invalid choice | `Invalid choice, please try again.` |
| | `key-color` / `key-colour` | colour of the `[key]` labels | terminal default |
| `<option>` | `key` | **required** — what the user types (case-insensitive) | |
| | `nav` | `Controller/Action`, or `Action` on the current controller | |
| | `quit` | flag: quit the application | |
| | `post` | flag: post the view's `Form` with the navigation | off |
| | style attributes | as for `<text>` | |

Each option needs exactly one of `nav` or `quit`. Options can be generated in a loop, e.g. `<option key="@(i + 1)" nav="Users/Details">@user.Name</option>`. If the input ends (for example when piping input to the application), the menu quits instead of waiting forever.

!!! note "Top level only"
    `<input>` and `<menu>` run at the point they appear in the view's code, so they must be written at the top level of the view — they may be inside a C# `if` or loop there, but not inside a `<box>` or another element.

### `<errors />`

Shows validation messages from the view's `ModelState` (see [Validation](https://github.com/MateuszPodeszwa/ConsoleMVC#validation)): every error, or only one field's errors with `for`. Renders nothing when there are no errors.

```
<errors />
<errors for="Email" color="yellow" />
```

```
• The Name field is required.
• The value 'twelve' is not valid for Age.
```

| Attribute | Values | Default |
|---|---|---|
| `for` | a field name (case-insensitive) | all fields |
| `color` / `colour` | a [colour](#colours) | `red` |

### `<partial />`

Renders a **partial view**: a reusable piece of markup in `Views/Shared/{Name}.cvw`. Partials are compiled like views but cannot navigate, so they contain no `<menu>` and need no `return`.

```
@* Views/Shared/Header.cvw *@
@model string
<box border="double" align="centre">@Model</box>
<br />
```

```
@* Views/Home/IndexView.cvw *@
<partial name="Header" model="@Model.Title" />
<partial name="Footer" />
```

| Attribute | Values | Default |
|---|---|---|
| `name` | the partial's file name, written literally (required) | |
| `model` | an expression (`@Model.User`) or literal text (passed as a `string`) | none |

* The partial is checked when you build: an unknown name (CVW114) lists the available partials, a partial with `@model` needs a `model` attribute (CVW115), and a model of the wrong type is a compiler error in the view that passes it.
* A partial shares the view's `Form`, `ModelState`, `ViewData`, renderer and input: an `<input>` in a partial fills the view's form, and `<errors />` shows the view's errors.
* Partials may use `@inject`, C# code and other partials (a partial cannot render itself: CVW116).
* Like `<input>`, `<partial>` is written at the top level of the view (it may be inside a C# `if` or loop), not inside another element.

## Themes and `class`

A theme defines named styles once for the whole application; elements use them with `class`, like CSS classes:

```
<text class="title">Orders</text>
<box class="panel" title="Summary">…</box>
<text class="muted danger">Payment overdue</text>
```

| Built-in class | Style |
|---|---|
| `title` | bold cyan text |
| `muted` | dim text |
| `success` / `warning` / `info` | green / yellow / blue text |
| `danger` | bold red text |
| `panel` | rounded cyan border, padding 2 |

Classes are applied in order, then the element's own attributes, so `<box class="panel" padding="0">` keeps the panel's border with no padding. Unknown class names are ignored. Text properties apply to `<text>`, `<cell>` and `<option>`; the text colour also colours `<separator>` and `<errors>`; border, padding and width properties apply to `<box>` and `<table>`.

Define your own classes, or replace the built-in ones, in `Program.cs`:

```csharp
builder.Theme
    .Set("brand", new ThemeStyle { Foreground = Colour.Magenta, Bold = true })
    .Set("panel", new ThemeStyle { Border = BorderStyle.Double, BorderColour = Colour.Magenta, Padding = 1 });

builder.Theme = Theme.Empty; // or start with no classes at all
```

`class` accepts expressions: `<text class="@(order.Late ? "danger" : "success")">`. Partials use the same theme as the view that renders them.

## Attribute values

* **Literal values** (`width="40"`, `border="double"`) are validated when you build. A typo such as `colour="purpel"` is a compile error pointing at the attribute.
* **Expressions** (`width=@Model.Width`, `title="@Model.Name's account"`) are type-checked by the C# compiler. Expressions can be mixed with text only in text attributes such as `title`.

### Colours

The 16 console colours by name: `black`, `darkblue`, `darkgreen`, `darkcyan`, `darkred`, `darkmagenta`, `darkyellow`, `grey`, `darkgrey`, `blue`, `green`, `cyan`, `red`, `magenta`, `yellow`, `white`. Both `grey` and `gray` are accepted, and dashes are ignored (`dark-grey`). Hex values such as `#f80` and `#ff8800` use 24-bit colour where the terminal supports it and the nearest console colour otherwise.

Colour output is automatically disabled when output is redirected, when the `NO_COLOR` environment variable is set, or when `TERM=dumb`.

## Compile-time diagnostics

Mistakes in markup are reported as build errors located in the `.cvw` file. C# errors inside views are also mapped back to the right `.cvw` line and column.

| Id | Meaning |
|---|---|
| CVW001 | The `.cvw` file is not at `Views/{Controller}/{Action}View.cvw` (warning; the file is skipped) |
| CVW002 | A folder or file name is not a valid C# identifier |
| CVW003 | The file name does not end with `View`, so the router cannot find it (warning) |
| CVW004 | Two views generate the same class (for example `Views/Home` and `Views/home`, or an `Areas/X/Views/Home` copy); the later file is skipped |
| CVW005 | The project's `RootNamespace` is not a valid C# namespace (set `<RootNamespace>` in the project file) |
| CVW006 | `@model` is declared more than once; the last one is used (warning) |
| CVW007 | `@inject` without a type and a property name, or an invalid or reserved name (see [Dependency Injection](dependency-injection.md)) |
| CVW008 | Two `@inject` directives declare the same property name |
| CVW100 | Unknown attribute |
| CVW101 | Invalid attribute value |
| CVW102 | Missing or mismatched closing tag |
| CVW103 | Element not allowed here (e.g. `<box>` inside `<text>`, `<row>` outside `<table>`, `<menu>` inside `<box>` or in a partial view) |
| CVW104 | Unterminated expression, string, comment or block |
| CVW105 | Unknown element (write `&lt;` for a literal `<`) |
| CVW106 | Control flow or a code block inside `<text>` |
| CVW107 | Attribute specified twice |
| CVW108 | Malformed tag |
| CVW109 | Element rule broken (an `<option>` needs exactly one of `nav` or `quit`) |
| CVW110 | Text or an expression directly inside `<table>`, `<row>` or `<menu>` |
| CVW111 | Required attribute missing (`<option key>`, `<input name>`) |
| CVW112 | `@()` contains no expression |
| CVW113 | Markup nested more than 100 levels deep |
| CVW114 | `<partial>` names no partial view in `Views/Shared`, or its name is not written literally |
| CVW115 | A partial that declares `@model` is used without a `model` attribute |
| CVW116 | A partial view renders itself |

## Rendering outside views

The elements are ordinary classes in the `ConsoleMVC.Rendering` namespace, which has no dependency on the MVC framework. You can use them directly:

```csharp
using ConsoleMVC.Rendering;

var box = new BoxElement { Title = "Status", Border = BorderStyle.Rounded };
var text = new TextElement { Foreground = Colour.Green };
text.AppendText("All systems operational");
box.Add(text);

MarkupRenderer.CreateForConsole().Render(box);
```

To change how an application renders — for example to write plain text at a fixed width in tests — set `MvcApplicationBuilder.Renderer`. To drive it with scripted input, set `MvcApplicationBuilder.Input` to your own `ITextInput`:

```csharp
builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(writer), () => 60);
builder.Input = new ScriptedInput("1", "Ada", "q"); // your ITextInput implementation
```

Interactive elements work outside views too:

```csharp
var name = new InputElement { Name = "Name", Label = "Name: ", Required = true };
string? value = name.Read(renderer, ConsoleTextInput.Instance);
```
