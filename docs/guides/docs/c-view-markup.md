# C-View Markup

C-View Markup is a small markup language for describing console screens inside `.cvw` views: text, boxes, tables, form inputs, choice lists, checkboxes and menus. It compiles to plain C# at build time, so there is no runtime template parsing, and it mixes freely with ordinary C# code.

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
    Views written in plain C# keep working unchanged. Markup is only recognised where a C# statement could begin, at the start of the body or after `;`, `{`, `}` or `:`, when `<` is directly followed by a known tag name. C# can never start a statement with `<`, so comparisons and generics such as `a < b` or `List<string>` are never mistaken for markup.

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
| `width` | `auto`, a column count (`40`), a percentage (`50%`), or `*` (all the width) | `auto` (fit the content) |
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
| `<column>` | `width` | `auto`, a column count, a percentage of the width after the gaps, or `*` for what the other columns leave | `auto` |

* Fixed and percentage columns take their width first; `auto` columns use their content's natural width when it fits; `*` (fill) columns share what is left, so `<column width="*">` followed by an auto column puts the second one at the right edge. Otherwise narrow columns keep their width and wide ones share the rest equally, wrapping their text.
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
| `name` | **required**: the key in `Form` (matched case-insensitively when binding) | |
| `label` | the prompt written before reading | none |
| `default` | used when the user enters nothing | empty |
| `required` | flag: ask again until a value is entered | off |
| `masked` | flag: hide typed characters (shows one `*` per character); for passwords and other secrets. Backspace works, and Ctrl+D (or Ctrl+Z on Windows) on an empty line ends the input | off |
| `color` / `colour` | label colour | terminal default |

Values are trimmed. `Form` is also available to ordinary C# in the view, for example `int.Parse(Form["Age"])`.

### `<select>`, `<radio>` and `<choice>`

Offer a fixed list of answers. The answer is stored in `Form` like an `<input>`, so it posts and binds the same way.
`<select>` lists the choices one under another (and may allow several answers); `<radio>` shows a short list side by
side.

```
<select name="Colour" label="Favourite colour:" default="green">
    <choice value="red">Red</choice>
    <choice value="green">Green</choice>
    <choice value="blue">Blue</choice>
</select>

<select name="Toppings" label="Toppings:" multiple>
    @foreach (var topping in Model.Toppings)
    {
        <choice value="@topping.Id">@topping.Name</choice>
    }
</select>

<radio name="Size" label="Size:" default="M"><choice>S</choice><choice>M</choice><choice>L</choice></radio>
```

In a terminal the user answers with the keys; the field is redrawn in place and collapses to its answer:

```
Favourite colour: (Up/Down, Enter)        Size: ( ) S  (*) M  ( ) L  (Left/Right, Enter)
   Red
 > Green                                  Toppings: (Up/Down, Space, Enter)
   Blue                                    > [x] Cheese
                                             [ ] Ham
after Enter:  Favourite colour: Green
```

| Key | Does |
|---|---|
| Up/Down (Left/Right, Tab, Home, End) | move the highlight; it wraps around |
| 1 to 9 | jump to that choice |
| Space | tick or untick the highlighted choice (`multiple` only) |
| Enter | confirm; a single choice is the highlighted one |
| Ctrl+D (Ctrl+Z on Windows) | end the input |

When input is piped, in tests, or when the console cannot move the cursor, the choices are numbered instead and the
user types a number, a value or a label (case-insensitive). Several answers are separated by commas (`1,3`). An
empty answer takes the default:

```
Favourite colour:
  1) Red
  2) Green
  3) Blue
Choose 1-3 [Green]: _
```

| Element | Attribute | Values | Default |
|---|---|---|---|
| `<select>`, `<radio>` | `name` | **required**: the key in `Form` | |
| | `label` | the text before the choices; leading spaces indent the whole field | none |
| | `default` | the value chosen for an empty answer and highlighted first; for `multiple`, values separated by commas | none |
| | `required` | flag: an empty answer (no tick, for `multiple`) is asked again | off |
| | `color` / `colour` | label colour | terminal default |
| | `highlight-color` / `highlight-colour` | colour of the `>` marker | cyan |
| `<select>` | `multiple` | flag: several choices may be ticked; the answer is their values separated by commas, in the order of the choices | off |
| | `filter` | flag: typing narrows the list in a terminal (Backspace and Esc edit the filter); a typed answer may be part of a name that matches one choice | off |
| | `size` | how many choices a terminal shows at once; the list scrolls with the highlight and PageUp/PageDown move a page | all |
| `<choice>` | `value` | what the form receives | the label text |
| | style attributes | as for `<text>` | |

A `multiple` answer binds to `string[]`, `List<int>`, `IReadOnlyList<MyEnum>` and other collections (see
[Model binding](controllers-and-forms.md#model-binding)), so its values cannot contain commas. A field without choices
(for example built from an empty list) throws an `InvalidOperationException` when it is shown; two choices with the
same value are an `ArgumentException`.

### `<number />` and `<date />`

Ask for a number or a date, checked against a range. The answers are stored in the invariant culture (`2.5`,
`2026-10-27`), so they bind to `int`, `decimal`, `double`, `DateOnly` and `DateTime`.

```
<number name="Qty" label="Quantity:" min="1" max="99" decimals="0" default="1" />
<number name="Price" label="Price:" min="0" decimals="2" step="0.5" />
<date name="Due" label="Due:" min="today" max="today+90" default="today+7" />
```

```
Terminal:  Quantity: 1  (Up/Down, type a number, Enter)
           Due: 2026-11-03 Tue  (Left/Right, Up/Down, t today, Enter)
Typed:     Quantity: _      (a number with a dot for decimals; Enter a number from 1 to 99. if it is out of range)
           Due: _           (2026-11-03, today, tomorrow, yesterday, +7 or -1)
```

| Element | Attribute | Values | Default |
|---|---|---|---|
| both | `name` | **required**: the key in `Form` | |
| | `label` | the text before the value (a space is added if it has none) | none |
| | `required` | flag: an empty answer is asked again | off |
| | `color` / `colour`, `highlight-color` / `highlight-colour` | label colour; colour of the value being edited | terminal default; cyan |
| `<number>` | `default` | the answer to an empty line and the starting value | none |
| | `min`, `max` | the range, e.g. `min="0" max="130"` or `max="@Model.Limit"` | none |
| | `step` | how much Up/Down change the value (PageUp/PageDown change it by ten steps) | 1 |
| | `decimals` | the most decimal places; `0` for whole numbers | any |
| `<date>` | `default`, `min`, `max` | `2026-10-27`, `today`, `today+N` or `today-N` (checked at build time) | none; the terminal starts today |

In a terminal, digits replace the number (the first one replaces the starting value), Backspace edits it and Esc
restores the default. A date's Up/Down change the part chosen with Left/Right, and the date never leaves its range.
`DateElement.Clock` decides what *today* is, so tests can fix it.

### `<checkbox />`

Asks a yes/no question. The answer is `"true"` or `"false"` and binds to a `bool`.

```
<checkbox name="Newsletter" label="Send me the newsletter" />
<checkbox name="Terms" label="I accept the terms" required />
```

```
Terminal:   [ ] Send me the newsletter  (Space, Enter)       Space ticks, Enter confirms, y or n answer at once
Typed:      Send me the newsletter? [y/N]: _                 y, yes, true, 1 or n, no, false, 0
```

| Attribute | Values | Default |
|---|---|---|
| `name` | **required**: the key in `Form` | |
| `label` | the text beside the box (a `?` is added to the typed question unless it ends in punctuation) | none |
| `checked` | flag: ticked at first, and the answer to an empty line | off |
| `required` | flag: the box must be ticked to continue (`This box must be ticked.`) | off |
| `color` / `colour` | label colour | terminal default |

### `<menu>` and `<option>`

Shows keyed choices, reads the user's choice, repeats until it is valid, and navigates. A menu replaces the usual "write the options, `Console.ReadLine()`, `switch`" code, so it **ends the view**: no `return` is needed.

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
| `<option>` | `key` | **required**: what the user types (case-insensitive) | |
| | `nav` | `Controller/Action`, or `Action` on the current controller, optionally with route values: `Orders/Details?id=@order.Id` (values from expressions are URL-escaped) | |
| | `quit` | flag: quit the application | |
| | `back` | flag: return to the previous screen (on the first screen, it is shown again) | |
| | `post` | flag: post the view's `Form` with the navigation | off |
| | style attributes | as for `<text>` | |

Literal `nav` targets are checked when you build: `nav="Orders/Detials"` gives warning CVW119 naming the actions
`Orders` has. Targets with an `@` expression are only known at run time and are not checked. Make the warning an error
with `dotnet_diagnostic.CVW119.severity = error` in `.editorconfig`.

Each option needs exactly one of `nav`, `quit` or `back`. Options can be generated in a loop, e.g. `<option key="@(i + 1)" nav="Users/Details">@user.Name</option>`. If the input ends (for example when piping input to the application), the menu quits instead of waiting forever.

!!! note "Top level only"
    `<input>`, `<select>`, `<radio>`, `<checkbox>` and `<menu>` run at the point they appear in the view's code, so they must be written at the top level of the view. They may be inside a C# `if` or loop there, but not inside a `<box>` or another element.

### `<breadcrumbs />`

Shows where the user is, from the navigation history: `Home › Orders › Details`. Each screen is labelled with its controller for an `Index` action, else its action; the current one is bold. When the trail is too wide the oldest items become `…`, so the current screen always shows. It renders nothing outside an application (for example in a unit test).

```
<breadcrumbs class="muted" />
<breadcrumbs separator=" / " color="darkgrey" />
```

| Attribute | Values | Default |
|---|---|---|
| `separator` | text between items | ` › ` |
| `color` / `colour` | a [colour](#colours) | terminal default |
| `class` | [theme](#themes-and-class) classes | none |

Views can also read the history in C#: `NavigationHistory` (the routes, oldest first) and `Breadcrumbs` (the labels).

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

* A file in `Views/Shared` that contains `<body />` is a [layout](#layouts-and-body), not a partial.
* The partial is checked when you build: an unknown name (CVW114) lists the available partials, a partial with `@model` needs a `model` attribute (CVW115), and a model of the wrong type is a compiler error in the view that passes it.
* A partial shares the view's `Form`, `ModelState`, `ViewData`, renderer and input: an `<input>` in a partial fills the view's form, and `<errors />` shows the view's errors.
* Partials may use `@inject`, C# code and other partials (a partial cannot render itself: CVW116).
* Like `<input>`, `<partial>` is written at the top level of the view (it may be inside a C# `if` or loop), not inside another element.

### Layouts and `<body />`

A layout frames every screen. It is a `.cvw` file in `Views/Shared` with a `<body />` placeholder where the view goes:

```
@* Views/Shared/_Layout.cvw *@
<text class="muted" align="right">My App</text>
<body />
<separator class="muted" />
```

* `Views/Shared/_Layout.cvw` is used by every view. A view picks another with `@layout Plain`, or none with `@layout none`.
* The part before `<body />` is drawn before the view. The part after it is drawn **just before the view first waits for the user** (its first `<input>`, `<select>`, `<radio>`, `<checkbox>` or `<menu>`), or when the view returns if it never waits. A console screen ends where it waits, so this is where a footer is seen: between the content and the menu.
* A layout shares the view's `ViewData`, `ModelState`, `Form`, theme and services, and may use C# and `@inject`. It runs before the view's own code, so it sees `ViewData` set by the controller. It has no `@model`.
* `<body />` appears once, at the top level of the layout, not inside an element or a C# block. Partials have no layout, and layouts cannot nest.

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
| CVW009 | A partial view file is named `Render`, the name of its own method |
| CVW100 | Unknown attribute |
| CVW101 | Invalid attribute value |
| CVW102 | Missing or mismatched closing tag |
| CVW103 | Element not allowed here (e.g. `<box>` inside `<text>`, `<row>` outside `<table>`, `<choice>` outside `<select>` or `<radio>`, `<menu>` inside `<box>` or in a partial view) |
| CVW104 | Unterminated expression, string, comment or block |
| CVW105 | Unknown element (write `&lt;` for a literal `<`) |
| CVW106 | Control flow or a code block inside `<text>` |
| CVW107 | Attribute specified twice |
| CVW108 | Malformed tag |
| CVW109 | Element rule broken (an `<option>` needs exactly one of `nav` or `quit`) |
| CVW110 | Text or an expression directly inside `<table>`, `<row>` or `<menu>` |
| CVW111 | Required attribute missing (`<option key>`, `<input name>`, `<select name>`, `<checkbox name>`) |
| CVW112 | `@()` contains no expression |
| CVW113 | Markup nested more than 100 levels deep |
| CVW114 | `<partial>` names no partial view in `Views/Shared`, or its name is not written literally |
| CVW115 | A partial that declares `@model` is used without a `model` attribute |
| CVW116 | A partial view renders itself |
| CVW117 | `<body />` outside a layout in `Views/Shared`, used twice or inside a C# block; or a layout with `@model` or `@layout` |
| CVW118 | `@layout` names no layout (lists the available ones), or a partial uses `@layout` |
| CVW119 | **Warning**: a literal `nav` target names a controller or action that does not exist (lists the ones that do) |

## Resizing the terminal

Elements are laid out to the terminal's width when they are drawn. When the window is resized, the framework clears the screen and lays the current screen out again at the new width, so a table re-wraps inside its borders instead of breaking onto new lines, and a wider window gives boxes and columns more room. Prompts and the answers already typed are shown again, and the cursor returns to the prompt that is waiting.

- This works on macOS, Linux and Windows terminals, a moment after the window stops changing size.
- A screen that also writes with `Console.WriteLine` is not redrawn, because that output cannot be laid out again; it stays as the terminal shows it.
- Characters typed at a prompt but not yet confirmed with Enter are kept, but appear again only after Enter.
- A `<select>`, `<radio>` or `<checkbox>` being answered with the arrow keys is redrawn with its current highlight and ticks.
- To turn it off: `builder.Renderer = MarkupRenderer.CreateForConsole(redrawOnResize: false);`

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

To change how an application renders (for example to write plain text at a fixed width in tests) set `MvcApplicationBuilder.Renderer`. To drive it with scripted input, set `MvcApplicationBuilder.Input` to your own `ITextInput`:

```csharp
builder.Renderer = new MarkupRenderer(new PlainTextRenderTarget(writer), () => 60);
builder.Input = new ScriptedInput("1", "Ada", "q"); // your ITextInput implementation
```

Interactive elements work outside views too:

```csharp
var name = new InputElement { Name = "Name", Label = "Name: ", Required = true };
string? value = name.Read(renderer, ConsoleTextInput.Instance);

var size = new RadioElement { Name = "Size", Label = "Size:" };
foreach (var label in new[] { "S", "M", "L" })
{
    var choice = new ChoiceElement();
    choice.AppendText(label);
    size.Add(choice);
}
string? chosen = size.Read(renderer, ConsoleTextInput.Instance);
```

Arrow keys are used when `ITextInput.CanReadKeys` and `IMarkupRenderer.CanRewrite` are both true; a scripted
`ITextInput` can return keys from `ReadKey` to test that mode, and `IRenderTarget.TryEraseLines` lets your own render
target support it.
