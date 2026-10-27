# Changelog

All notable changes to ConsoleMVC are documented here. The project follows [Semantic Versioning](https://semver.org/).

## [1.8.0]

### Added

- **Live reload** ([guide](docs/guides/docs/getting-started.md#live-reload)): `.cvw` files are watched by
  `dotnet watch`, and after each hot reload update the application rediscovers controllers and views before the next
  screen, so new pages are routable without a restart.

## [1.7.0]

### Added

- **Partial views** ([design](docs/design/partial-views.md), [guide](docs/guides/docs/c-view-markup.md)):
  `.cvw` files in `Views/Shared` compile to `ConsolePartialView<TModel>` and are rendered with
  `<partial name="…" model="@…" />`. The name and model type are checked at build time; a partial shares the view's
  `Form`, `ModelState`, `ViewData`, renderer and input, and receives its `@inject` services.
- `ConsoleViewBase` (shared by views and partials) and `ConsoleViewBase.PreparePartial`.
- Diagnostics CVW114 (unknown partial), CVW115 (partial needs a model) and CVW116 (partial renders itself);
  CVW103 now also reports a `<menu>` in a partial view.
- The template's home and about pages share a `PageHeader` partial; the feature tour lists `<errors>` and `<partial>`.

### Changed

- `.cvw` files in `Views/Shared` are partial views rather than routable views, and need no `View` suffix.
- `ConsoleView`'s view data, model state, renderer, input and form members moved to the new base class
  `ConsoleViewBase` (source compatible).

## [1.6.0]

### Added

- **Asynchronous actions** ([design](docs/design/async-and-middleware.md),
  [guide](docs/guides/docs/async-and-middleware.md)): actions may return `Task<T>` or `ValueTask<T>` of an
  `ActionResult` type; `CancellationToken` parameters receive the run's token.
- `MvcApplication.RunAsync(CancellationToken)`; `Run()` waits for it.
- **Middleware**: `MvcApplication.Use(...)` and `UseMiddleware<T>()` with `ActionContext`, `ActionDelegate` and
  `IActionMiddleware` — wrap, replace or short-circuit actions and handle their exceptions.
- `RouteContext.ToString()` returns `Controller/Action`.
- The template's `Program.cs` uses `RunAsync` and a middleware that shows the current page in the terminal title.

## [1.5.0]

### Added

- **Dependency injection** ([design](docs/design/dependency-injection.md),
  [guide](docs/guides/docs/dependency-injection.md)) with `Microsoft.Extensions.DependencyInjection`:
  - `MvcApplicationBuilder.Services` to register services and `MvcApplication.Services` for the root provider.
  - Controllers receive services through their constructors.
  - `@inject Type Name` in `.cvw` files declares a view property filled from the services (`InjectAttribute`).
  - Every navigation cycle (an action and the view it renders) runs in its own scope; controllers and views that
    implement `IDisposable` are disposed at the end of their cycle.
  - `IMarkupRenderer` and `ITextInput` are registered as services.
  - `Router.ResolveController(name, services)` and `Router.ResolveView(controller, action, services)`.
- Diagnostics CVW007 (invalid `@inject`) and CVW008 (duplicate `@inject` property).
- The template's greeting example uses an injected `IGreetingService`.

### Changed

- `MvcApplication` implements `IDisposable`; write `using var app = builder.Build();` to dispose singleton services.
- Service registrations are validated when the application is built (`ValidateOnBuild`, `ValidateScopes`).

## [1.4.0]

### Added

- **Form validation** ([design](docs/design/form-validation.md)): posted form data is validated with the standard
  DataAnnotations attributes before the action runs — on model properties (including `IValidatableObject`) and on
  simple action parameters.
- `ModelStateDictionary` (`Controller.ModelState`, `ConsoleView.ModelState`) with `IsValid`, `AddModelError`,
  `ErrorsFor` and `Clear`.
- Values that cannot be converted (e.g. `"abc"` for an `int`) are reported in `ModelState` instead of being ignored.
- `Controller.View(viewName, model)` renders another action's view (e.g. the form again) and
  `Controller.TryValidateModel(model)` validates objects built in code.
- `<errors />` / `<errors for="Field" />` markup element and the `ValidationErrorsElement` runtime class.

- `Controller.View(viewName)` renders another action's view without a model, as in ASP.NET Core MVC.
- Positional records (and other models without a parameterless constructor) are bound through their public
  constructor.

### Changed

- The template's greeting and calculator forms are validated and show their errors.
- `View("text")` now selects the view named `text`; pass a string model with `View((object)text)`.
- Binding a model that cannot be created (no usable constructor) throws a descriptive `InvalidOperationException`
  instead of silently passing `null`.

### Fixed

- `IValidatableObject` no longer runs after a conversion error, so cross-field rules do not report errors about
  fallback values.
- `TryValidateModel` no longer adds the same message twice, and returns whether that model is valid.
- Conversion errors use the field's `[Display(Name = "…")]`.
- `<errors>` measured multi-line messages and trailing spaces too wide inside a box sized to its content.

## [1.3.0]

### Added

- **C-View Markup v0.2** ([design](docs/design/c-view-markup-0.2.md), [guide](docs/guides/docs/c-view-markup.md)):
  - `<table>`, `<row>` and `<cell>` — columns sized to the content; the widest columns shrink and wrap when the
    terminal is narrow; header rows in bold with a separator.
  - `<input>` — reads a value into the view's new `Form` dictionary, with `label`, `default`, `required` and
    `masked` (password) options.
  - `<menu>` and `<option>` — shows keyed choices, re-prompts on invalid input and navigates (`nav`, `quit`, `post`).
    A menu ends the view, so no `return` is needed; at end of input it quits instead of waiting forever.
- `ConsoleView.Form`, `ConsoleView.Input`, `ReadInput()` and `RunMenu()`.
- `ITextInput` and `ConsoleTextInput` (masked reading never echoes input); `MvcApplicationBuilder.Input` for
  scripted input in tests.
- `IMarkupRenderer.WritePrompt` and `IRenderTarget.WriteInline` for prompts.
- `InlineElement` base class shared by `TextElement`, `TableCellElement` and `MenuOptionElement`.
- `BorderStyle` table junction characters (`TeeDown`, `TeeUp`, `TeeRight`, `TeeLeft`, `Cross`).
- Diagnostics CVW109 (element rule), CVW110 (text in an element-only container) and CVW111 (missing attribute).

### Changed

- CVW103 now also reports placement rules (`<row>` outside `<table>`, `<menu>` inside `<box>`, …) with a message
  explaining the rule. Placement is checked inside `@foreach`/`@if` as well.
- The template uses `<menu>` and `<input>`, and adds a feature tour page with a `<table>`.
  `HomeViewModel.MenuOptions` was removed from the template.

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
