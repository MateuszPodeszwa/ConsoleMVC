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

### Changed

- `@model` is optional and defaults to `object`.
- The template's views use C-View Markup.
