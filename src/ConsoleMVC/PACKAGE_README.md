# ConsoleMVC

An MVC framework for console applications, inspired by ASP.NET Core MVC.

ConsoleMVC brings the familiar **Controller-ViewModel** pattern to console apps, with convention-based routing, automatic discovery through a generated route table, and Razor-like `.cvw` view templates powered by a C# source generator.

## Quick Start

```bash
# Install the template
dotnet new install ConsoleMVC.Template

# Create a new project
dotnet new consolemvc -n MyApp
cd MyApp
dotnet run
```

## How It Works

**Program.cs**: familiar ASP.NET-style builder:
```csharp
var builder = MvcApplication.CreateBuilder(args);
builder.Services.AddSingleton<IGreetingService, GreetingService>(); // optional
using var app = builder.Build();
await app.RunAsync();
```

**Controllers**: inherit from `Controller`, return `ActionResult`:
```csharp
public class HomeController : Controller
{
    public ActionResult Index()
    {
        var model = new HomeViewModel { Title = "Hello!" };
        return View(model);
    }
}
```

**Views**: `.cvw` files with an `@model` directive and C-View Markup, no class boilerplate:
```
@model MyApp.Models.HomeViewModel

<box title="@Model.Title" border="rounded" color="cyan">
    <text>@Model.Message</text>
</box>

<input name="Name" label="Your name: " required />

<menu>
    <option key="1" nav="Greet/Result" post>Greet me</option>
    <option key="q" quit>Exit</option>
</menu>
```

**Models**: simple DTOs:
```csharp
public class HomeViewModel
{
    public string Title { get; set; } = "";
}
```

## Features

- **Convention-based routing**: Controllers and Views are found by name and namespace conventions, listed in a route table generated at build time
- **Razor-like view engine**: `.cvw` files are compiled into `ConsoleView<TModel>` classes at build time by a source generator
- **No boilerplate**: Views are plain C# code with an `@model` directive, no class inheritance required
- **C-View Markup**: `<box>`, `<text>`, `<separator>`, `<br>` and `<table>` elements with colours, borders, alignment and word wrapping, plus `@` expressions and `@if`/`@foreach`; validated at compile time with errors pointing at the `.cvw` file, including `nav` targets that name a missing controller or action (CVW119), the same mistakes in `RedirectToAction` and `NavigationResult` calls (CVW120), and missing views (CVW121, with a fix that creates the view)
- **Responsive screens**: resizing the terminal lays the current screen out again at the new width
- **Full screen and scrolling**: `builder.FullScreen = true` uses the terminal's alternate screen, and `<scroll>` pages long content with the arrow keys
- **Layout and themes**: `<columns>` (stacking on narrow terminals), `min-width`/`max-width`, and theme classes with `class="title"`
- **Layouts**: `Views/Shared/_Layout.cvw` frames every screen, with `<body />` where the view goes
- **Partial views**: reusable markup in `Views/Shared/{Name}.cvw`, rendered with `<partial name="Header" model="@Model.Title" />` and checked at build time
- **Declarative input and navigation**: `<input>` fields (required, default, masked), `<number>` and `<date>` with ranges, `<select>` (with type-to-filter) and `<radio>` lists and `<checkbox>` (answered with the arrow keys in a terminal) collect a `Form`, and `<menu>` reads the choice and navigates
- **Mouse**: `builder.Mouse = true` makes menu options, choices and checkboxes clickable and the wheel scroll
- **Keyboard shortcuts**: `app.MapKey(ConsoleKey.F1, "Help/Index", "Help")`, `NavigationResult.Back()` on Esc, and `<shortcuts />` to list them
- **Markdown**: `<markdown text="@Model.Notes" />` (`MarkdownElement`) draws headings, lists, quotes, code, emphasis and links, sanitised
- **Charts**: `<bar-chart>` with `<bar />` children and `<sparkline values="@Model.History" />` (`BarChartElement`, `SparklineElement`)
- **Live screens**: `<progress>` and `<spinner>`, kept up to date while an async action works with `ILiveDisplay.Start(element)`, and views that refresh on a timer with `@refresh 5s` or `View(model).RefreshEvery(...)` until the user types
- **Areas and modules**: `Areas/{Area}/Views` with `[Area]` or `*.Areas.{Area}` controllers and `Area/Controller/Action` routes, and `builder.AddModule<TModule>()` (`IMvcModule`) for controllers and views in other assemblies
- **Localisation**: `IStringLocalizer<T>` in views and controllers, `builder.Localization` (culture, `FrameworkText` translated from resources, localised DataAnnotations messages), and the `ConsoleMVC:Culture` setting
- **Accessibility**: `builder.ScreenReader` (`ScreenReaderRenderer`: plain text, no borders, typed answers) and `builder.HighContrast` (`Theme.CreateHighContrast()`), also chosen by users with the `ConsoleMVC:ScreenReader` and `ConsoleMVC:HighContrast` settings
- **Secure output**: model values are sanitised so they cannot inject terminal escape sequences
- **Reusable rendering engine**: the `ConsoleMVC.Rendering` namespace can draw elements in any console app
- **Familiar API**: `View()`, `RedirectToAction()`, `ViewData`, all the patterns you know from ASP.NET Core MVC
- **Navigation**: Views return `NavigationResult` to control app flow (`NavigationResult.To()`, `Back()`, `Quit()`); menus offer `back` options and `<breadcrumbs />` shows the trail; route values (`nav="Orders/Details?id=@order.Id"`) bind to action parameters
- **Testing**: the companion package [ConsoleMVC.Testing](https://www.nuget.org/packages/ConsoleMVC.Testing) runs the application with scripted input and compares screens with snapshot files
- **Configuration and logging**: `builder.Configuration` (appsettings.json, environment variables, command line), options with `Configure<T>`, `builder.Environment` and `ILogger<T>` through `builder.Logging`, with a built-in rolling file logger (`builder.Logging.AddFile("logs/app.log")`)
- **Validation**: posted forms are checked with DataAnnotations (`[Required]`, `[Range]`, `[EmailAddress]`, …) before the action runs; use `ModelState.IsValid`, `View("Form", model)` and `<errors />` as in ASP.NET Core MVC
- **Dependency injection**: `builder.Services` (Microsoft.Extensions.DependencyInjection); constructor injection into controllers, `@inject Type Name` in views, one scope per screen
- **Async actions and middleware**: `Task<ActionResult>` actions, `RunAsync(token)`, and `app.Use(...)` / `UseMiddleware<T>()` around every action
- **Native AOT and trimming**: a generated route table (`IRouteTable`) replaces reflection, so `dotnet publish -p:PublishAot=true` works with no warnings
- **Remote sessions**: `app.ServeAsync(new TerminalServerOptions())` or the `ConsoleMVC:Serve` setting serves the application over Telnet (optionally TLS) with one session per client; `ITerminalSession` tells users apart, and `RemoteTerminal` plus `RunSessionAsync` run a session over any stream
- **Live reload**: `dotnet watch` applies `.cvw` and C# edits to the running app from the next screen; new pages are discovered automatically
- **Form data posting**: Views collect user input via `Console.ReadLine()` and post it to controller actions using `NavigationResult.To("Controller", "Action", formData)`. The framework automatically binds the data to action method parameters, either as individual simple types (`int`, `string`, etc.) or as a complex model class with matching property names

## IDE Support

For `.cvw` file recognition, syntax highlighting, and icon support in JetBrains Rider, install the **ConsoleMVC CVWSupport** plugin:

- [JetBrains Marketplace](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport)
- [Plugin Source Code](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport)

## Documentation

For full documentation and source code, visit the [GitHub repository](https://github.com/MateuszPodeszwa/ConsoleMVC).
