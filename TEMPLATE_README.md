# ConsoleMVC Application Template

A `dotnet new` project template for **ConsoleMVC**: an MVC framework for console applications inspired by ASP.NET Core MVC.

## Installation

```bash
dotnet new install ConsoleMVC.Template
```

## Usage

```bash
# Create a new ConsoleMVC project
dotnet new consolemvc -n MyApp

# Run it
cd MyApp
dotnet run

# Or create it with a test project (MyApp.Tests) and run the tests
dotnet new consolemvc -n MyApp --tests
dotnet test MyApp/MyApp.Tests
```

| Option | Default | Purpose |
|---|---|---|
| `--tests` | off | Adds `MyApp.Tests`, an xUnit project that runs the application with [ConsoleMVC.Testing](https://www.nuget.org/packages/ConsoleMVC.Testing): a snapshot of the home menu (`__snapshots__/home-menu.txt`) and the greeting form driven with typed lines and arrow keys |

## What You Get

A ready-to-run console application structured with the MVC pattern:

- **Program.cs**: entry point using the familiar `CreateBuilder()`, `Build()`, `RunAsync()` pattern, with a middleware that shows the current page in the terminal title
- **Controllers/**: controller classes that handle actions and return results
- **Views/**: `.cvw` views using C-View Markup (`<box>`, `<text>`, `<table>`, `<columns>`, `<input>`, `<select>`, `<checkbox>`, `<menu>`, `<progress>`, `<spinner>`, `<bar-chart>`, `<sparkline>`, `<markdown>`, `<scroll>`) and C#, compiled at build time; `Views/Shared` holds the layout and a partial view
- **Models/**: ViewModel DTOs passed from controllers to views
- **Services/**: an `IGreetingService` registered with `builder.Services` in `Program.cs`, configured by `GreetingOptions`
- **appsettings.json**: settings read by `builder.Configuration` (environment variables and command-line arguments override them)

The template includes working examples of:

- **Navigation**: `<menu>` elements moving between controllers and actions (`Home`, `About`), Back options, and breadcrumbs in the layout
- **Tables, columns and themes**: a feature tour page with a `<table>` generated from the model, `<columns>` that stack on narrow terminals, and theme classes (`class="panel"`)
- **Partial views and layouts**: the home and about pages share `Views/Shared/PageHeader.cvw` through `<partial>`, and `Views/Shared/_Layout.cvw` adds an app bar (breadcrumbs on the left, the app name on the right) and a separator above every menu
- **Form data posting with model binding**: a greeting form with a text `<input>`, a colour `<select>` (arrow keys in a terminal) and a `<checkbox>`, posted to a controller as a `GreetFormModel` class, validated with `[Required]` and `[StringLength]` and showing errors with `<errors />`
- **Logging**: `Program.cs` sends logs to `logs/app.log` with `builder.Logging.AddFile`, since the console shows the application
- **Configuration**: the greeting's salutation comes from the `Greeting` section of `appsettings.json` through `IOptions<GreetingOptions>`; try `dotnet run -- --Greeting:Salutation=Hi`
- **Accessibility**: run with `dotnet run -- --ConsoleMVC:ScreenReader=true` for plain text a screen reader reads well, or `--ConsoleMVC:HighContrast=true` for bold, bright colours
- **Full screen and scrolling**: `Program.cs` sets `builder.FullScreen = true`, so the app runs on the alternate screen and your scrollback comes back afterwards; the feature tour's table is paged with `<scroll height="12">`
- **Markdown**: the about page's text is Markdown in `HomeController`, drawn with `<markdown>`
- **Live screens**: the feature tour's live status option checks the services with a `<progress>` bar that updates while the action works (`ILiveDisplay`), then shows a status page that refreshes every second with `@refresh 1s`, with a `<sparkline>` of the load and a `<bar-chart>` of response times
- **Keyboard shortcuts**: Esc goes back and Ctrl+X quits on every screen (`app.MapKey` in `Program.cs`), listed by `<shortcuts />` in the layout
- **Dependency injection**: `GreetController` receives `IGreetingService` through its constructor and the result view reads it with `@inject`
- **Form data posting with simple parameters**: a calculator with two `<number>` fields and a `<radio>` for the operator, posting individual values (`int a`, `int b`, `string op`) directly to action method parameters

## IDE Support

For `.cvw` file recognition, syntax highlighting, and icon support in JetBrains Rider, install the **ConsoleMVC CVWSupport** plugin:

- [JetBrains Marketplace](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport)
- [Plugin Source Code](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport)

## Learn More

- [ConsoleMVC.Framework NuGet Package](https://www.nuget.org/packages/ConsoleMVC.Framework)
- [ConsoleMVC.Testing NuGet Package](https://www.nuget.org/packages/ConsoleMVC.Testing): test the application with scripted input and snapshots
- [Guides](https://mateuszpodeszwa.github.io/ConsoleMVC/guides/)
- [GitHub Repository](https://github.com/MateuszPodeszwa/ConsoleMVC)
