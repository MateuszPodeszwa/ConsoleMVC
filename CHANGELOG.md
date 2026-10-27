# Changelog

All notable changes to ConsoleMVC are documented here. The project follows [Semantic Versioning](https://semver.org/).

## [1.2.0]

### Added

- **C-View Markup v0.1** — `<text>`, `<box>`, `<separator>` and `<br>` elements in `.cvw` views, with `@expressions`,
  `@(explicit expressions)`, `@{ code blocks }`, `@if`/`@foreach`/`@for`/`@while`/`@switch`/`@do`/`@try`, comments and
  character entities. Plain-C# views are unaffected. See [the guide](docs/guides/docs/c-view-markup.md).
- **Rendering engine** (`ConsoleMVC.Rendering`) — independent of the MVC layer: colours (16 console colours, hex true
  colour), borders, alignment, word wrapping, Unicode-aware widths, ANSI / plain-text / legacy Windows console targets,
  and automatic detection of `NO_COLOR`, `COLORTERM`, redirected output and terminal width.
- **Output sanitisation** — values written through markup cannot inject escape sequences or bidirectional overrides.
- **Compile-time diagnostics** CVW001–CVW108 for markup and view placement mistakes, reported in the `.cvw` file.
- `#line` mapping: C# errors inside views point at the `.cvw` line and column.
- `MvcApplicationBuilder.ApplicationAssembly`, `Renderer` and `ClearScreenBetweenViews`.
- `[NonAction]` attribute for public controller helpers.
- `ConsoleView.Renderer` and partial generated view classes.

### Fixed

- View lookup was case-sensitive although controller lookup was not, so `NavigationResult.To("home", "index")` failed.
- Property accessors (`get_ViewData`), `object` methods and non-`ActionResult` methods were routable as actions;
  overloaded actions threw `AmbiguousMatchException`.
- A custom or `null` action result made the application loop forever; endless redirects are now detected.
- Exceptions thrown inside actions surfaced as `TargetInvocationException`.
- `Console.Clear()` threw `IOException` when output was redirected.
- A missing entry assembly or partially loadable assembly crashed discovery.
- Missing or invalid form values overwrote model property initialisers; invalid simple values ignored the parameter's
  declared default; form keys differing only in case threw.
- A `null` or mismatched model produced an unhelpful cast exception.
- Views outside `Views/{Controller}/` were skipped silently, and a project inside a folder named `Views` produced the
  wrong namespace.
- Generated views relied on `ImplicitUsings` and produced CS1591 warnings with XML documentation enabled.
- Two views whose generated file names collided (`Views/A_B/CView` and `Views/A/B_CView`, or `Views/Home` and
  `Views/home`) made the generator fail and drop every view; genuine duplicates now report CVW004.
- `#line` paths were escaped, so errors in projects under non-ASCII or Windows paths did not link to the `.cvw` file.
- Generated code had no `#nullable` directive (CS8669) and ignored the project's nullable setting.
- A comment between `}` and `else`/`catch`/`finally` rendered the rest of the statement as text.
- Preprocessor directives (`#region Customer's data`, `#if DEBUG`) were lexed as code, causing false errors or hiding
  the markup on the next line.
- E-mail addresses in attribute values were parsed as expressions; `@a@b` printed the second expression literally;
  unquoted attribute values did not decode entities; whitespace did not collapse across comments in text.
- Keyword folder and file names (`Views/class`) and invalid `RootNamespace` values produced broken generated code
  instead of CVW002/CVW005.
- `@using System.Text;` (with a semicolon), `@model` followed by a tab, and `@* comments *@` before the directives
  were misread; a duplicate `@model` is now reported (CVW006); `@()` is reported (CVW112); `@using` errors point at
  the namespace.
- Extremely deep nesting could overflow the compiler's stack (now CVW113).
- Generated variable names could clash with user variables such as `__cvw1`.
- Boxes, text and wrapped words could draw lines wider than the space given (large padding, tiny widths, wide
  characters); padding now shrinks to fit and lines are clipped.
- A percentage-width box inside an auto-width box was sized twice.
- Trailing spaces in values made auto-width boxes too wide.
- `Length` percentages overflowed for very large widths.
- `DateOnly`, `TimeOnly`, `Half`, `Int128`, `UInt128`, `Uri` and `Version` were never bound; undefined enum numbers
  (e.g. `99`) were accepted.

### Changed

- `@model` is optional and defaults to `object`.
- The template's views use C-View Markup.
