# Changelog

All notable changes to ConsoleMVC are documented here. The project follows [Semantic Versioning](https://semver.org/).

## [1.30.0]

### Added

- **Areas** ([design](docs/design/areas-and-modules.md), [guide](docs/guides/docs/areas-and-modules.md)): controllers in
  `*.Areas.{Area}.*` namespaces or with `[Area("…")]` (`AreaAttribute`) have routes such as `Admin/Users/Index`, and
  their views live in `Areas/{Area}/Views/{Controller}` (namespace `*.Areas.{Area}.Views.{Controller}`). Inside an area,
  a controller named without one is the area's own when it has one. `RouteContext.Area` and `ControllerName`,
  `Router.ControllerKey` and `Router.Qualify`. The build checks (CVW119 to CVW121) and the Create view fix know areas.
- **Modules**: `builder.AddModule<TModule>()` (`IMvcModule.Configure`) and `builder.AddModule(assembly)` add the
  controllers and views of other assemblies; `builder.Modules`.
- The template's settings page is in an Admin area.

### Changed

- Navigation targets may have three parts (`Area/Controller/Action`); four or more are still invalid.
- The package's build props include `Areas/**/*.cvw` as views.

## [1.29.0]

### Added

- **Localisation** ([design](docs/design/localisation.md), [guide](docs/guides/docs/localisation.md)):
  `IStringLocalizer<T>` is registered for views and controllers. `builder.Localization` (`MvcLocalizationOptions`)
  sets the run's `Culture` (or the `ConsoleMVC:Culture` setting), the framework's own messages (`FrameworkText`,
  translated with `FrameworkTextResource` or set in code) and `LocalizeValidation`: DataAnnotations `ErrorMessage`
  and `[Display(Name)]` are translated and formatted with the attribute's own arguments.
- The template translates the greeting form, the framework's prompts and the validation messages into Polish.

### Changed

- Every built-in message and prompt (menus, fields, `<scroll>`, binding errors) now comes from `FrameworkText.Current`;
  the English defaults are unchanged.

## [1.28.0]

### Added

- **Screen-reader mode** ([design](docs/design/accessibility.md), [guide](docs/guides/docs/accessibility.md)):
  `builder.ScreenReader = true` or the `ConsoleMVC:ScreenReader` setting wraps the renderer in `ScreenReaderRenderer`.
  Elements are described in plain text by `ScreenReaderText` (boxes without borders, tables as `Header: value` lines,
  progress, spinners and charts in words), fields take typed answers, screens are separated rather than erased, and
  full-screen mode is ignored. `MarkupRenderer.CreateForScreenReader()`, `MarkdownElement.UseBoxDrawing`.
- **High contrast**: `Theme.CreateHighContrast()`, chosen with `builder.HighContrast = true` or the
  `ConsoleMVC:HighContrast` setting.
- The template's `Program.cs` explains both settings.

## [1.27.0]

### Added

- **Full-screen mode** ([design](docs/design/full-screen.md), [guide](docs/guides/docs/configuration.md)):
  `builder.FullScreen = true` runs the application on the terminal's alternate screen and restores the normal screen
  when it ends, fails or is stopped with Ctrl+C. `IRenderTarget.TrySetAlternateScreen` (implemented by
  `AnsiRenderTarget`), `IMarkupRenderer.SetFullScreen` and `IMarkupRenderer.Height`.
- **`<scroll>`** (`ScrollElement`, `ConsoleViewBase.ShowScroll`): long content shown a page at a time, scrolled with
  the arrow keys, J and K, Page Up and Page Down, Space, Home and End, and left with Enter, Esc or Q. Everything is drawn
  at once when it fits or input or output is piped.
- The template runs full screen and pages the feature tour's table.

## [1.26.0]

### Added

- **`<markdown text="…" />`** ([design](docs/design/markdown.md), [guide](docs/guides/docs/c-view-markup.md#markdown)):
  `MarkdownElement` draws headings, paragraphs (with hard line breaks), bulleted and numbered lists (nested), block
  quotes, fenced code (clipped, not wrapped), rules, `**bold**`, `*italic*`, `` `code` ``, links (`ShowLinkUrls`) and
  escapes. Output is sanitised, and parsing is bounded so unusual input cannot slow a screen down.
- The template's about page is written in Markdown.

## [1.25.0]

### Added

- **Charts** ([design](docs/design/charts.md), [guide](docs/guides/docs/c-view-markup.md#bar-chart-and-bar)):
  `<bar-chart>` with `<bar label value colour />` (`BarChartElement`, `BarElement`, `BarChartStyle`: `block` with
  eighths of a cell, `shade`, `ascii`; `max`, `format`, `values="false"`), and `<sparkline values min max show-last />`
  (`SparklineElement`) showing the most recent values that fit.
- `ChartValues.From` turns sequences of `int`, `long`, `float`, `double` or `decimal` into chart values;
  `values="3, 5, 2"` literals are checked at build time.
- The template's status page draws a sparkline of the load and a bar chart of response times.

## [1.24.0]

### Added

- **Live regions** ([design](docs/design/live-elements.md), [guide](docs/guides/docs/live-screens.md)): `ILiveDisplay`
  (registered as `LiveDisplay`) `Start(element, interval?)` draws an element and keeps it up to date while an action
  works; `LiveRegion.Update(change)`, `Refresh()` and disposal draw it in place, and a timer turns spinners
  (`IAnimatedElement`). On a pipe or a file the element is drawn once, in its final state.
- **`<progress />`** (`ProgressElement`, `ProgressStyle`: `block`, `line`, `ascii`) and **`<spinner />`**
  (`SpinnerElement`, `SpinnerStyle`: `dots`, `line`, `circle`; `done` shows a tick).
- **Timed refresh**: `@refresh 5s` in a view, or `View(model).RefreshEvery(TimeSpan)` and `NoRefresh()` in the action,
  draws the screen again with fresh data until the user starts typing. CVW010 reports an invalid interval and
  `@refresh` in a partial or a layout.
- `ITextInput.WaitForInput(timeout)` (implemented by `ConsoleTextInput`), and `ScriptedInput.Wait()` for testing
  refreshing screens.
- The template's feature tour has a live status option: a progress bar while the services are checked, then a page
  that refreshes every second, with a test for it in `--tests`.

## [1.23.0]

### Added

- **Keyboard shortcuts** ([design](docs/design/shortcuts.md), [guide](docs/guides/docs/controllers-and-forms.md#keyboard-shortcuts)):
  `app.MapKey(ConsoleKey.F1, "Help/Index", "Help")`, `app.MapKey(KeyGesture, NavigationResult | Func<NavigationResult>, description)`
  work while any screen waits for input: menus, text fields and arrow-key fields. `KeyGesture` (`Parse("Ctrl+X")`),
  `KeyShortcut`, `MvcApplication.Shortcuts`. Keys needed for typing and terminal control keys are refused.
- **`<shortcuts />`** (`ShortcutsElement`, `ShortcutHint`, `ConsoleViewBase.ShortcutHints`) lists the described shortcuts.
- `MvcTestHost.ConfigureApplication` configures the built application (shortcuts, middleware) in tests.
- The template maps Esc to Back and Ctrl+X to Quit and lists them in the layout.

### Changed

- With shortcuts mapped, lines are read key by key (typing, Backspace, Delete, Left, Right, Home, End), so shortcuts
  work in menus and text fields; without shortcuts, input is unchanged.

## [1.22.0]

### Added

- **File logging** ([design](docs/design/file-logging.md), [guide](docs/guides/docs/hosting.md#logging)):
  `builder.Logging.AddFile("logs/app.log")` (or `AddFile(options => …)`, or the `Logging:File` section) writes
  entries on a background task, rolls the file over by size, keeps `RetainedFileCount` old files, indents
  continuation lines and drops control characters so logged values cannot forge entries, and never blocks a screen.
  `FileLoggerProvider` (alias `File`) and `FileLoggerOptions`.
- The template's `Program.cs` logs to `logs/app.log`.

## [1.21.0]

### Added

- **Navigation checks in C#** ([design](docs/design/route-checks.md#5-navigation-in-c-121)): `NavigationAnalyzer`
  reports `RedirectToAction`, `NavigationResult.To` and `NavigationResult.ToAction` with a constant controller or
  action that does not exist (CVW120), and `View("Name")` or `View()` in an action without a view (CVW121). Code in
  `.cvw` views is checked and reported at the view's line. Both are warnings.
- **Create view** code fix for CVW121 (`ConsoleMVC.CodeFixes`, bundled in the framework package): writes
  `Views/{Controller}/{Name}View.cvw` with the model's type as `@model`.

## [1.20.0]

### Added

- **Number and date fields** ([design](docs/design/richer-inputs.md), [guide](docs/guides/docs/c-view-markup.md#number-and-date)):
  `<number>` (`min`, `max`, `step`, `decimals`, Up/Down and typed digits in a terminal) and `<date>` (`min`, `max`,
  `default` as `2026-10-27`, `today` or `today+N`; Left/Right/Up/Down and `t` in a terminal; typed `today`,
  `tomorrow` or `+7`). Answers are invariant (`2.5`, `2026-10-27`). `NumberElement`, `DateElement`
  (`TryParse`, `Clock`), `NumberElement.Validate`.
- **`<select filter>`**: typing narrows the choices in a terminal, and a typed answer may be part of one name;
  **`<select size="N">`** scrolls a long list with "more above/below" lines and PageUp/PageDown.
- Build-time checks of `min`, `max`, `step` and date values (CVW101).
- The template's calculator uses `<number>` and `<radio>`.

## [1.19.0]

### Added

- **`dotnet new consolemvc --tests`** adds `MyApp.Tests`, an xUnit project using ConsoleMVC.Testing: a snapshot of the
  home menu (shipped, so continuous integration passes from the first commit), the greeting form driven with typed
  lines and arrow keys, and a validation test. The application's project excludes the test folder.
- CI generates projects from the packed template, with and without `--tests`, builds them with `-warnaserror` and
  runs the generated tests; the publish workflow checks the test project's ConsoleMVC.Testing version.

### Documentation

- Brought every guide and readme up to date with 1.10 to 1.18: Getting Started shows the current template (layout,
  partial, `appsettings.json`) and next steps; the readme's project tree, entry point, application flow and "Adding
  New Pages" example (now markup with a Back option) match the template; the template readme lists the choice inputs
  and configuration; CONTRIBUTING lists the testing package and the documentation rules; the layout footer is
  described as drawn before any field, not only `<input>` and `<menu>`.
- The API reference includes the `ConsoleMVC.Testing` namespace, and the documentation workflow builds with .NET 10.
- The "Configuration and Testing" guide is now "Builder Options"; testing has its own guide.

## [1.18.0]

### Added

- **The `ConsoleMVC.Testing` package** ([design](docs/design/testing-package.md), [guide](docs/guides/docs/testing.md)):
  `MvcTestHost` runs an application with scripted input at a fixed width in the `Testing` environment, with a
  `MaxScreens` guard; `TestRun` gives the screens, output, routes and unread input; `ScriptedInput` scripts lines and
  keys (arrow-key fields are used when the next step is a key); `ScreenRecorder` records screens and supports in-place
  redraws; `Snapshot.Match` compares text with `__snapshots__/<name>.txt`, never creates a snapshot when `CI` is set,
  saves `.received.txt` on a difference and accepts changes with `CONSOLEMVC_UPDATE_SNAPSHOTS=1`.
- The publish workflow packs and publishes `ConsoleMVC.Testing` with the other packages.

## [1.17.0]

### Added

- **Compile-time route checks** ([design](docs/design/route-checks.md)): a literal `nav` target that names a missing
  controller or action is warning CVW119, listing the controllers or actions that exist. Controllers and actions are
  found with the same rules as the runtime router; targets with `@` expressions, and compilations without
  controllers, are not checked. Raise it with `dotnet_diagnostic.CVW119.severity = error`.

## [1.16.0]

### Added

- **Configuration and logging** ([design](docs/design/hosting-integration.md), [guide](docs/guides/docs/hosting.md)):
  `builder.Configuration` reads `appsettings.json`, `appsettings.{Environment}.json`, environment variables and the
  command line (later ones win); `builder.Environment` (`IHostEnvironment`); `builder.Logging` (`ILoggingBuilder`,
  levels from the `Logging` section, no provider by default so nothing writes over the screen).
- `IConfiguration`, `IHostEnvironment`, `IOptions<T>` and `ILogger<T>` can be injected into controllers, views and
  services. Options registered with `ValidateOnStart()` are validated by `Build()`.
- `MvcApplication.CreateBuilder(MvcApplicationOptions)` (arguments, environment name, content root, application name),
  `MvcApplication.Configuration` and `MvcApplication.Environment`, and `ConsoleHostEnvironment`.
- The framework logs the start, each screen (Debug), unhandled exceptions (Error, still rethrown) and the stop under
  `ConsoleMVC.Mvc.MvcApplication`.
- The template has an `appsettings.json` with a `Greeting` section bound to `GreetingOptions`.

### Changed

- `CreateBuilder(args)` now uses `args` (as command-line configuration).

## [1.15.0]

### Added

- **Choice inputs** ([design](docs/design/choice-inputs.md), [guide](docs/guides/docs/c-view-markup.md#select-radio-and-choice)):
  `<select>` (one under another, `multiple` for several answers), `<radio>` (side by side) with `<choice value="…">`, and
  `<checkbox>` (`true`/`false`). In a terminal they are answered with the arrow keys, Space and Enter and redrawn in
  place; with piped input or in tests the choices are numbered and typed (a number, a value or a label).
- `SelectElement`, `RadioElement`, `ChoiceElement`, `CheckboxElement`, the `ChoiceFieldElement` base class and the
  `IFormField` interface (`InputElement` implements it too).
- `ITextInput.CanReadKeys` and `ReadKey`, `IMarkupRenderer.CanRewrite` and `Rewrite`, and `IRenderTarget.TryEraseLines`
  (default implementations keep existing implementations working). `MarkupRenderer` has a constructor taking the
  terminal height, and `TerminalCapabilities.GetHeight`.
- Model binding of comma-separated values to `T[]`, `List<T>` and the collection interfaces of simple types, for
  properties, parameters and record constructors.
- The template's greeting form uses a `<select>` and a `<checkbox>`.

### Changed

- `ConsoleViewBase.ReadInput` takes any `IFormField`.

## [1.14.0]

### Added

- **Fill columns**: `<column width="*">` takes the width the other columns leave (shared between fill columns), so
  content can sit at the right edge. `Length.Fill`, `LengthKind.Fill` and `Length.IsRelative`; `*` is also accepted by
  other `width` attributes, meaning all the available width.
- The template's app bar shows the breadcrumbs on the left and the app name on the right.

## [1.13.0]

### Added

- **Route values** ([design](docs/design/route-values.md), [guide](docs/guides/docs/controllers-and-forms.md#route-values)):
  `nav="Orders/Details?id=@order.Id"`, `NavigationResult.To("Orders/Details?id=42")`,
  `NavigationResult.To(controller, action, routeValues)` and `RedirectToAction(action, [controller,] routeValues)` pass
  values that bind to the action's parameters. Form data wins over route values; route values do not trigger
  validation. Expressions after `?` in `nav` are URL-escaped.
- `RouteValues.From` and `RouteValues.ParseTarget`; `RouteValues` on `NavigationResult`, `RedirectToActionResult` and
  `RouteContext`.
- Back returns to the screen with the same route values.

### Changed

- Literal `nav` values may include `?key=value` route values (checked at build time, CVW101).

## [1.12.0]

### Added

- **Navigation history** ([design](docs/design/navigation-history.md)): the screens shown are remembered (up to
  `MvcApplication.MaxHistory`, 50). `NavigationResult.Back()` and `<option back>` return to the previous screen.
- `ConsoleViewBase.NavigationHistory` and `Breadcrumbs`, and the `<breadcrumbs />` element (`BreadcrumbsElement`)
  showing `Home › Orders › Details`, shortened from the start when it does not fit.
- The template's layout shows breadcrumbs, and the About and feature tour pages have a Back option.

## [1.11.0]

### Added

- **Layout views** ([design](docs/design/layout-views.md), [guide](docs/guides/docs/c-view-markup.md#layouts-and-body)):
  a `.cvw` file in `Views/Shared` with a `<body />` placeholder frames views. `Views/Shared/_Layout.cvw` is the
  default; views choose with `@layout Name` or `@layout none`. The header is drawn before the view and the footer just
  before the view first waits for the user (`ConsoleLayoutView`, `ConsoleView.UseLayout`).
- Diagnostics CVW117 (invalid `<body />` or layout) and CVW118 (unknown layout); CVW114 reports a `<partial>` that
  names a layout.
- The template has a `_Layout.cvw` with an app bar and a separator above every menu.

## [1.10.0]

### Added

- **Responsive screens** ([design](docs/design/responsive-redraw.md),
  [guide](docs/guides/docs/c-view-markup.md#resizing-the-terminal)): when the terminal is resized, the current screen
  is laid out again at the new width, including prompts and the answers already typed. Tables, boxes and columns no
  longer break onto new lines when the window gets narrower, and use the extra room when it gets wider.
- `MarkupRenderer.RedrawIfWidthChanged`, `MarkupRenderer.MarkForeignOutput`, `MarkupRenderer.MaxRedrawHistory`,
  `IMarkupRenderer.RecordInput` (with a default implementation) and `ForeignOutputTracker`.
- `MarkupRenderer.CreateForConsole(redrawOnResize)`; `MarkupRenderer` implements `IDisposable` to stop watching.

## [1.9.0]

### Added

- **C-View Markup 1.0** ([design](docs/design/c-view-markup-1.0.md), [guide](docs/guides/docs/c-view-markup.md)):
  - `<columns>` / `<column>`: side-by-side layout with fixed, percentage and auto widths, `gap`, and `stack-below`
    to stack the columns on narrow terminals (`ColumnsElement`, `ColumnElement`).
  - Themes: `class="…"` on `<text>`, `<cell>`, `<option>`, `<box>`, `<table>`, `<separator>` and `<errors>` applies
    named styles from `MvcApplicationBuilder.Theme` (`Theme`, `ThemeStyle`; `Theme.CreateDefault()` defines `title`,
    `muted`, `success`, `warning`, `danger`, `info` and `panel`). `ConsoleViewBase.Theme` is shared with partials.
  - `min-width` and `max-width` on `<box>` and `<table>` (`MinWidth` / `MaxWidth`).
- The template's feature tour uses columns and theme classes.
- `MvcApplication` implements `IAsyncDisposable` (`await using var app = builder.Build();`).
- A release workflow publishes both packages to NuGet.org (with Trusted Publishing, so no API key is stored) and
  GitHub Packages whenever a new version reaches `main`, and creates the GitHub release with its changelog section.
- Guides for controllers and forms, validation, and configuration and testing.

### Changed

- Controllers or views with the same name in different namespaces now fail with a message naming both, instead of
  one silently replacing the other.
- `Theme.Empty` returns a new theme on each access.
- Text widths are measured per user-perceived character: emoji with `U+FE0F`, skin tones, flags and ZWJ sequences
  (👨‍👩‍👧) occupy two columns and are never split when wrapping or truncating.

### Fixed

- `<!-- comments -->` at the top level of a view are ignored instead of being compiled as C#.
- `<separator>text</separator>` and other void elements with content report CVW103 instead of C# errors.
- `@inject` rejects names that hide view members (`Theme`, `PreparePartial`, …), allows trailing comments, and maps
  the property name to its column.
- A literal `model` or `class` of the wrong type is reported at the attribute.
- `Views/shared` (any case) holds partials; clashing, misnamed or broken partials no longer cause compiler errors
  in the views that use them; a partial named `Render` reports CVW009.
- Columns inside an auto-width box no longer stack on a wide terminal.
- `Dispose` no longer throws for services that implement only `IAsyncDisposable`; controllers and views that implement
  only `IAsyncDisposable`, class middleware created per cycle, and every controller created by a retrying middleware
  are disposed.
- `RunAsync` can run again after finishing or being cancelled before it started.
- Array action parameters bind `null` again instead of throwing.
- Hot reload refreshes the cached `@inject` properties of edited views.

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
  `IActionMiddleware`: wrap, replace or short-circuit actions and handle their exceptions.
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
  DataAnnotations attributes before the action runs, on model properties (including `IValidatableObject`) and on
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
  - `<table>`, `<row>` and `<cell>`: columns sized to the content; the widest columns shrink and wrap when the
    terminal is narrow; header rows in bold with a separator.
  - `<input>`: reads a value into the view's new `Form` dictionary, with `label`, `default`, `required` and
    `masked` (password) options.
  - `<menu>` and `<option>`: shows keyed choices, re-prompts on invalid input and navigates (`nav`, `quit`, `post`).
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

- **C-View Markup v0.1**: `<text>`, `<box>`, `<separator>` and `<br>` elements in `.cvw` views, with `@expressions`,
  `@(explicit expressions)`, `@{ code blocks }`, `@if`/`@foreach`/`@for`/`@while`/`@switch`/`@do`/`@try`, comments and
  character entities. Plain-C# views are unaffected. See [the guide](docs/guides/docs/c-view-markup.md).
- **Rendering engine** (`ConsoleMVC.Rendering`), independent of the MVC layer: colours (16 console colours, hex true
  colour), borders, alignment, word wrapping, Unicode-aware widths, ANSI / plain-text / legacy Windows console targets,
  and automatic detection of `NO_COLOR`, `COLORTERM`, redirected output and terminal width.
- **Output sanitisation**: values written through markup cannot inject escape sequences or bidirectional overrides.
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
