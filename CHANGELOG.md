# Changelog

All notable changes to ConsoleMVC are documented here. The project follows [Semantic Versioning](https://semver.org/).

## [1.34.0]

### Added

- **Editor support** ([design](docs/design/editor-support.md), [guide](docs/guides/docs/editor-support.md)): the
  `ConsoleMVC.Preview` .NET tool (`dotnet tool install -g ConsoleMVC.Preview`, command `cvw-preview`) draws a `.cvw`
  view with the real rendering engine without building or running the application, as text, ANSI or HTML
  (`--width`, `--format`, `--stdin` for an editor's unsaved text, `--high-contrast`). Expressions show as
  placeholders, code is skipped, partial views and the layout are read from `Views/Shared`, and the markup's
  diagnostics are reported with their line and column. It runs no project code and reads only shared views by plain
  name.
- `schema/cvw-schema.json` describes every element, attribute (kind, accepted values, range), directive, construct and
  diagnostic of C-View Markup with documentation, for editor plugins. It is written from the generator's schema
  (`MarkupSchemaExport`, `MarkupDocumentation`), and a test fails when it is out of date
  (`UPDATE_SCHEMA=1 dotnet test` rewrites it).
- CI installs the packed tool and previews the template's views; the publish workflow releases `ConsoleMVC.Preview`
  with the other packages.
- **Acceptance tests** (`tests/ConsoleMVC.AcceptanceTests`, run with `tests/ConsoleMVC.AcceptanceTests/run.sh`): an
  application with real `.cvw` views that uses the packed `ConsoleMVC.Framework` and `ConsoleMVC.Testing` packages,
  as a user's application would, and checks the documented behaviour through typed input. CI runs them.

- CVW120 also checks the route of `app.MapKey(key, "Help/Index")`. CVW119, CVW120 and CVW121 suggest the closest name
  (`Did you mean 'Details'?`) and list the others closest first, saying how many more there are.

- New build diagnostics: CVW011 (`@model` or `@using` without a type or namespace), CVW012 (a page view with neither a
  `<menu>` nor a `return`, which was the C# error CS0161 at an unhelpful place) and CVW122 (`@if`, `@foreach`, `@{` or
  `@(` written in the view's C#, where `@` is not needed). A bare `@refresh` is CVW010 and a bare `@layout` CVW117.
- `MarkupValue.ToDecimal` and `ToDouble`: number attributes (`<number min max step>`, `<progress>`, `<bar>`,
  `<bar-chart max>`, `<sparkline>`) accept an expression of any number type, nullable ones included (`@(3.5)` was a
  CS0266 error for `<number>`).
- Every attribute in `schema/cvw-schema.json` says whether it accepts an `expression`.
- CVW123: two actions of a controller with the same name (overloads, or names differing only in case) are a build
  error; they used to build and then fail when navigated to.
- A route starting with `/` names the application's controller from inside an area: `nav="/Home/Index"` leaves an
  area that has its own Home for the application's (it was CVW101, and no route could reach it).
- The template has a table booking form (`BookingController`): `<date>`, a `<number>` checked by `[Range]`,
  `<select filter size>`, `<select multiple>` and a masked `<input>` checked by `[RegularExpression]`, logged with
  `ILogger<T>`, in a box with a theme class of the application's own. F1 opens the help on every screen. It has an
  `appsettings.Development.json`, a `.gitignore`, a quote and a link in the about page's Markdown, and its tests set the
  application up as `Program.cs` does (theme, mouse, shortcuts) and cover the booking form, F1 and a mouse click.
- An `<input>` with a default (or an answer kept from a form shown again) shows it after the label, `Name: [Ada]`, so
  the user knows Enter keeps it. A masked field never shows one.
- `ClickTarget.FromColumn` and `ToColumn` let several click targets share a line, and `IMarkupRenderer.KeysAt(row,
  column)` finds the one clicked (the default implementation calls `KeysAt(row)`, so existing renderers keep working).

### Changed

- Ctrl+C ends a local run cleanly: the screen waiting for the user is left at once (it used to end only after the next
  key, even with a cancellation token), services are disposed, `Stopped` is logged and `Run` returns. A second Ctrl+C
  ends the process at once. A token passed to `RunAsync` also stops a screen that is waiting for the user.

### Fixed

- When the input ended (a pipe or file running out, or Ctrl+D) while a form field was waiting, the form was posted
  with empty answers, ignoring `default` and `required`; a form shown again after failing validation then repeated for
  ever, writing millions of lines. The application now ends, as a `<menu>` does at the end of input.
- An empty value bound to a `string` gave `""`, so an optional `[EmailAddress] string? Email` left blank failed
  validation. An empty value is now not bound, as for every other type: the field keeps its default (`null`).
- A screen in the navigation history is its route with its route values: `Orders/Details?id=2` and
  `Orders/Details?id=42` were one entry, so Back skipped a screen. Going to a screen already in the history (a menu's
  Home option) now returns the history to it, so breadcrumbs no longer grow without end.
- `MapKey` refuses Ctrl+H, Ctrl+I, Ctrl+J and Ctrl+M, which terminals send as Backspace, Tab and Enter (the shortcut
  could never run), and the refused keys with Shift.
- CVW120 quoted `NavigationResult.To("Orders", "Detials")` as `NavigationResult.To("Detials")`.
- The markup diagnostics' severity is documented as set with `WarningsAsErrors` or a `.globalconfig`: `.editorconfig`
  sections do not cover `.cvw` files, so `dotnet_diagnostic.CVW119.severity` there had no effect.
- Screen-reader mode wrote colour and style codes when the application set its own renderer
  (`builder.Renderer = MarkupRenderer.CreateForConsole()`, as the configuration guide shows); it now writes plain text
  whatever the renderer.
- With Esc mapped as a shortcut (the template maps it to Back), Esc in a changed `<number>` left the screen instead
  of restoring the default. A field now keeps a key it has a use for at that moment (Esc in a changed `<number>` or a
  `<select filter>` with a filter typed); otherwise the shortcut runs.
- A `<checkbox>` answered key by key ignored the translated yes and no letters (`t` in Polish) and accepted only `y`
  and `n`; it now accepts the translated letters, and `y` and `n` when they do not mean the opposite.
- `ScriptedInput.Click` and `Wheel` with the mouse off failed with "the next step of the script is the key Escape";
  the message now says to turn on `builder.Mouse`. The testing guide's samples compile as written (their `using`
  directives, a mouse host for `Click`, `Router.ResolveController` on a router instance, `RemainingInput` as a count),
  and it explains that with shortcuts or the mouse on, key steps are typed into text fields.
- Resizing the terminal during a `<scroll>` garbled the screen: the page kept the old width (the terminal wrapped it),
  the status counted the old lines, and the page left on the screen afterwards stayed at the old width. The page is now
  laid out again at the new width.
- A `Raw` remote session drew its first screen at 80 by 24 although the client reported its size: the answer was only
  read with the first key. The session now waits up to half a second for it before the first screen.
- Serving (`ConsoleMVC:Serve`) said nothing on the console, so with the log in a file a server looked stuck; it now
  says where it listens and how to connect. The refusal to listen on a network without TLS names the
  `ConsoleMVC:ServeAllowUnencrypted` setting, and a missing `ConsoleMVC:ServeCertificate` file is reported as missing
  rather than as "Error occurred during a cryptographic operation".
- A missing service in a view's `@inject` was named as the runtime writes it (`IOptions`1`); it is now named as in C#
  (`IOptions<GreetingOptions>`). The configuration guide no longer claims that `Build()` checks the services of
  controllers and views: they are checked when their screen opens.
- The keys shown beside a field in a terminal (`(Space, Enter)`, `(Up/Down, Enter)`, …), `Filter: `, `(no matches)`
  and the "more above/below" lines of a `<select size>` were always in English; they are now `FrameworkText`
  properties (`CheckboxKeys`, `SelectKeys`, `SelectManyKeys`, `FilterKeys`, `FilterManyKeys`, `RadioKeys`, `NumberKeys`,
  `DateKeys`, `FilterLabel`, `NoMatches`, `MoreAbove`, `MoreBelow`), and the template translates them, and the number,
  date, choice and binding messages it lacked, into Polish. Its Polish number hint said `2,5`, which is refused.
- A whole-number `<number decimals="0">` answered with something that is not a number said "Enter a number, e.g. 42 or
  2.5", inviting an answer it refuses; it now says "Enter a whole number."
- In screen-reader mode a screen with `@refresh` was written out again at every refresh; screens no longer refresh in
  that mode.
- Snapshots failed on differences nobody can see: a space at the end of a line (the prompt `Select an option: `) or a
  final new line added by an editor. `Snapshot.Match` now ignores spaces at the ends of lines and blank lines at the
  end, saves snapshots with a final line ending, and quotes the differing lines in its message so leading spaces show.
- An on/off `ConsoleMVC:` setting with a value other than `true` or `false` (`--ConsoleMVC:ScreenReader=yes`) was
  ignored, leaving the feature off; `Build()` now stops with a message naming the setting. The new
  `ConsoleMVC:FullScreen` setting matches `builder.FullScreen`, and the configuration guide lists every `ConsoleMVC:`
  setting.
- Clicking a `<radio>` choice did nothing: a radio's choices are side by side, and only whole lines could be clicked.
  The mouse guide spoke of a "vertical `<radio>`", which does not exist. A long radio choice on a narrow terminal was
  cut off at the edge (`Large and extra wide choi`); it now wraps under its own text.
- The build checks warned about navigation between an application and its modules: `nav="Billing/Invoices/Index"`
  in the application (CVW119), and `nav="Home/Index"` or `RedirectToAction("Index", "Home")` in a module (CVW119,
  CVW120), although both work. The application's checks now know the controllers of the modules it references, and a
  module (a library with an `IMvcModule` class, or `<ConsoleMVCModule>true</ConsoleMVCModule>`) does not report the
  application's controllers as unknown. The modules guide says where a module's area views live.
- Literal values that the runtime refuses now fail the build (CVW101, or CVW109 for a `<choice>`), as the guide
  promised: a `<number>` `step` of 0 or less, `min` above `max`, or a `default` that is not a number, out of range or
  with too many decimals (an empty answer then asked again for ever); a `<select>` or `<radio>` without choices, with
  duplicate values or with a `default` that names no choice; duplicate `<menu>` keys (`q` and `Q`); a `<date>` whose
  `min` is after its `max`; a `<box>` narrower than its border. The runtime also refuses such a `<number>` or choice
  default, and an empty answer stores the choice's own value (`default="m"` stores `M`). Date literals accept exactly
  what a typed date does (`tomorrow`, `+7`).
- An attribute written with `=` and no value (`<box title=>`) was accepted silently for some attributes; it is CVW101
  for every one.
- `@inject` with a name of a view's own member (`Breadcrumbs`, `NavigationHistory`, `ShortcutHints`, `ShowScroll`,
  `InjectServices`, `RequireService`, …) or of the view's class gave C# errors at a line that does not exist; every
  such name is now CVW007, and the dependency injection guide lists them. `@inject System.String Foo Bar` is CVW007 at
  the type.
- The schema said a `<partial name>` could be an expression; it must be written literally.
- `cvw-preview`:
  - reports the build's own checks of partials, layouts and directives (CVW114 to CVW118, CVW103, CVW010 in partials,
    CVW001 to CVW003, CVW009, …) at the build's line and column; route checks still need a build, as the guide says;
  - draws the layout's part after `<body />` before the view's first prompt, as the application does, and draws
    fields as a terminal first shows them (`checked`, `default` values, the select and radio frames);
  - refuses a `--width` outside 20 to 500 with a clear message (it clamped silently), accepts `--option=value`, and
    says a shared view over 1 MB is too large rather than not found.
- With piped input `<shortcuts />` still listed shortcuts, which do nothing there; it now lists none.
- A route typed in another case (`admin/users/index`) kept that case in `RouteContext` and the breadcrumbs
  (`Area` was `admin`); routes now read as the controller is declared.
- Validation messages without an `ErrorMessage` can be translated once for every field: a resource named after the
  attribute's English template (`The {0} field is required.`) is formatted like the original. A resource with the
  whole message still wins.
- `ViewResult.RefreshEvery` refuses an interval over 24 hours, like `@refresh` (`ViewResult.MaximumRefreshInterval`),
  and says the range it accepts. The file log says "1 entry was dropped".
- The sanitiser removes whole escape sequences: `Bob\u001b[2J` became `Bob[2J`, and now becomes `Bob`, as the guide
  says.
- Guides corrected where their samples did not work as written: the hosting guide's development logging (the
  `Logging` section overrode `SetMinimumLevel`; it now uses `AddFilter`, with its `using` directives), `@inject
  IOptions<…>` (needs `@using Microsoft.Extensions.Options`), `@Model.Total` formatting in Polish, the sparkline
  sample's bars, the Native AOT executable size, and where relative log paths go (the application's folder, also in
  the template's readme).
- A Markdown image (`![a chart](chart.png)`) was drawn as `!a chart (chart.png)`; it is now `[image: a chart]`.
- A form shown again after validation failed lost every answer (the greeting form went back to its first colour and an
  empty name). Fields without an error now start from the answer posted, as ASP.NET Core forms do, so Enter keeps it;
  fields with an error, and masked fields, are asked afresh.
- The template: the greeting counter is thread-safe (remote sessions share the singleton), the calculator's numbers
  are required (an empty answer was taken as 0), and `GreetController.Result`'s comment says what it does.
- Numbers are parsed as documented: invariant culture with decimal digits only. Whole numbers no longer accept
  hexadecimal (`0x10`, `#10`, `&H10`), `Half` no longer accepts thousands separators, and `float`, `double` and `Half`
  reject values that are not finite (`1e400`, `Infinity`, `NaN`).

## [1.33.0]

### Added

- **Remote sessions** ([design](docs/design/remote-sessions.md), [guide](docs/guides/docs/remote-sessions.md)): one
  process serves the application to many users, each with their own screen, input, current page and history.
  `TerminalServer` and `TerminalServerOptions` listen over TCP with Telnet (window size and terminal type negotiated) or
  raw bytes, optionally over TLS 1.2 or 1.3; `app.ServeAsync(options, token)`, or the `ConsoleMVC:Serve` settings
  (`ServeProtocol`, `ServeCertificate`, `ServeCertificatePassword`, `ServeMaxSessions`, `ServeAllowUnencrypted`) make
  `Run` and `RunAsync` serve until Ctrl+C.
- `RemoteTerminal` and `RemoteTerminalOptions` turn any stream into a terminal (keys and escape sequences decoded,
  output written as ANSI with CR LF), and `MvcApplication.RunSessionAsync(terminal, token)` runs a session on it, for
  other transports such as WebSockets.
- `ITerminalSession`, resolved from services: the session's `Id`, `IsRemote`, `RemoteEndPoint`, `TerminalType`, `Width`,
  `Height`, per-session `Items` and `Disconnect()`.
- Secure defaults: loopback only, a network address needs TLS (or `AllowUnencryptedNetworkAccess`), at most 16 sessions,
  bounded input and window sizes, idle, handshake and write timeouts (`WriteTimeout`, 30 seconds), and random session
  identifiers. Stopping the server closes connections whose sessions have not ended after 5 seconds.
- The template's status page shows its session, and `Program.cs` explains how to serve the app.

### Changed

- `ILiveDisplay` draws on the terminal of the session that started the live element.
- A run's current page and history are kept per session; local runs behave as before.
- The alternate screen and mouse reporting are put back with every kind of terminal error, not only `IOException`.

## [1.32.0]

### Added

- **Native AOT and trimming** ([design](docs/design/native-aot.md), [guide](docs/guides/docs/native-aot.md)): the
  source generator writes a route table per assembly (`IRouteTable`, `ControllerRoute`, `ActionRoute`, `ViewRoute`,
  `RouteTableAttribute`; `.cvw` views register with `GeneratedViewAttribute`) with typed controller and view factories,
  typed action calls and `DynamicDependency` attributes for model types. `Router.Use(table)`; the router falls back to
  scanning without a table and after a hot reload. Generated views fill `@inject` properties in `InjectServices`
  (`ConsoleViewBase.RequireService`). The framework is analysed for trimming and AOT, and the template publishes as
  Native AOT with no warnings (checked in CI).
- Generic overloads `RedirectToAction<TRouteValues>`, `NavigationResult.To<TRouteValues>` and
  `RouteValues.From<TValues>` keep route values' properties when trimming; the `object` overloads are
  `[RequiresUnreferencedCode]`.
- The template enables the configuration binding generator.

### Changed

- Controllers and views are found through the generated route table, and actions are called without reflection, which
  also makes start-up faster.

## [1.31.0]

### Added

- **Mouse support** ([design](docs/design/mouse.md), [guide](docs/guides/docs/controllers-and-forms.md#mouse)):
  `builder.Mouse = true` (or the `ConsoleMVC:Mouse` setting) turns on the terminal's mouse reports. Clicking a menu
  option, a `<select>` or vertical `<radio>` choice or a `<checkbox>` answers it, and the wheel moves like the arrow
  keys. Elements register `ClickTarget`s with `IMarkupRenderer.MapClicks`; `KeysAt`, `SetMouseReporting` and
  `IRenderTarget.TrySetMouseReporting` (implemented by `AnsiRenderTarget`).
- `ScriptedInput.Click(column, row)` and `Wheel(down)` for tests.
- The template turns the mouse on.

### Changed

- Line editing key by key (shared by shortcuts and the mouse) understands `ConsoleKey.Clear`, which erases the line.

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
