# ConsoleMVC

> **C-View Markup:** describe console screens with `<box>`, `<text>`, `<table>`, `<input>`, `<select>` and `<menu>`, embed C# with Razor-style `@` expressions, and let the build compile it all to plain C#. New in 1.33: remote sessions: `dotnet run -- --ConsoleMVC:Serve=2323` serves the application to many users at once over Telnet or TLS, each with their own screen and history. 1.32 added Native AOT and trimming, 1.31 mouse support, 1.30 areas and feature modules. All releases are in the [changelog](CHANGELOG.md). See the **[C-View Markup guide](docs/guides/docs/c-view-markup.md)** and the roadmap in **[Planned Features & Project Direction](plannedfeatures.md)**.

An MVC framework for .NET console applications, inspired by ASP.NET Core MVC.

ConsoleMVC brings the **Controller-ViewModel** pattern to console apps, with convention-based routing, reflection-based auto-discovery, and a Razor-like view engine powered by a C# source generator. If you've built ASP.NET Core MVC apps before, you already know how to use ConsoleMVC.

## Why ConsoleMVC?

Console applications often end up as unstructured, hard-to-maintain code. UI frameworks like Spectre.Console help with presentation, but they don't solve the underlying architectural problem. ConsoleMVC provides:

- **Separation of concerns**: Controllers handle logic, Views handle rendering, Models carry data
- **Convention over configuration**: Controllers and Views are auto-discovered at startup via reflection
- **Familiar API**: `View()`, `RedirectToAction()`, `ViewData`, the same patterns from ASP.NET Core MVC
- **Razor-like view engine**: `.cvw` files are plain C# with an `@model` directive, compiled at build time by a source generator
- **C-View Markup**: declarative `<box>`, `<text>`, `<separator>` and `<table>` elements with colours, borders, alignment and word wrapping, checked at compile time
- **Partial views and layouts**: reusable markup in `Views/Shared`, rendered with `<partial name="…" model="@…" />` and type-checked at build time, and a `_Layout.cvw` that frames every screen
- **Layout and themes**: `<columns>` that stack on narrow terminals, `min-width`/`max-width`, and named styles used with `class="…"`
- **Responsive screens**: resize the terminal and the current screen is laid out again at the new width; tables, boxes and columns never break onto new lines
- **Full screen and scrolling**: `builder.FullScreen = true` runs on the terminal's alternate screen and restores the user's scrollback afterwards, and `<scroll>` pages long content with the arrow keys
- **Mouse**: with `builder.Mouse = true`, clicking a menu option, a choice or a checkbox answers it and the wheel scrolls, in terminals that report the mouse
- **Keyboard shortcuts**: `app.MapKey(ConsoleKey.F1, "Help/Index", "Help")` works on every screen, in menus and fields alike, and `<shortcuts />` lists them
- **Markdown**: `<markdown text="@Model.Notes" />` draws headings, lists, quotes, code, `**bold**`, `*italic*` and links, wrapped to the terminal and sanitised
- **Charts**: `<bar-chart>` with `<bar label value />` (bars ending in eighths of a cell) and `<sparkline values="@Model.History" />` for dashboards
- **Live screens**: `<progress>` and `<spinner>` redraw in place while an async action works (`ILiveDisplay`), and `@refresh 5s` redraws a dashboard with fresh data until the user starts typing
- **Declarative input and navigation**: `<input>`, `<number>` and `<date>` collect form values, `<select>` (with type-to-filter), `<radio>` and `<checkbox>` offer choices answered with the arrow keys, and `<menu>` reads the user's choice and navigates, with no `Console.ReadLine()` or `switch` boilerplate
- **Areas and modules**: group screens in `Areas/{Area}` (routes such as `Admin/Users/Index`, checked at build time) and add controllers, views and services from class libraries with `builder.AddModule<TModule>()`
- **Localisation**: `@inject IStringLocalizer<SharedResource> L` in views, the framework's own messages and DataAnnotations messages translated from `.resx` resources, and the culture chosen in code or with `--ConsoleMVC:Culture=pl-PL`
- **Accessible**: users turn on a screen-reader mode (plain text, tables read as `Header: value`, typed answers) or a high-contrast theme with `--ConsoleMVC:ScreenReader=true` or `--ConsoleMVC:HighContrast=true`, no new build needed
- **Safe by default**: model data is sanitised before it reaches the terminal, so it cannot inject escape sequences
- **Form data posting**: Views collect user input and post it to controller actions with automatic model binding, just like HTML forms in web MVC
- **Validation**: posted forms are checked with the standard DataAnnotations attributes (`[Required]`, `[Range]`, …) before the action runs; `ModelState` and `<errors />` work as in ASP.NET Core MVC
- **Dependency injection**: register services with `builder.Services`; controllers receive them through their constructors and views through `@inject`, with one scope per screen
- **Configuration and logging**: `builder.Configuration` reads `appsettings.json`, `appsettings.{Environment}.json`, environment variables and the command line; options bind with `Configure<T>`, and `ILogger<T>` works everywhere, with a built-in rolling file logger (`builder.Logging.AddFile`) instead of one writing over the screen
- **Async actions and middleware**: actions can return `Task<ActionResult>`, and `app.Use(...)` runs logging, authorisation or error handling around every action
- **Testing**: the `ConsoleMVC.Testing` package runs the application with scripted lines and keys, records every screen and the routes visited, and compares screens with snapshot files
- **Native AOT**: `dotnet publish -p:PublishAot=true` makes a single native executable that starts in milliseconds; the source generator writes a route table so nothing depends on reflection
- **Remote sessions**: one process serves the application to many users at once (`--ConsoleMVC:Serve=2323`, then `telnet localhost 2323`), each with their own screen, history and `ITerminalSession`; loopback-only and TLS-ready by default, and SSH or a web terminal through OpenSSH or ttyd
- **Live reload**: run `dotnet watch` and edits to views and controllers appear from the next screen, without restarting

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- JetBrains Rider is recommended, for the [ConsoleMVC CVWSupport plugin](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport); a Visual Studio extension is planned
- With VS Code or another editor, [add basic support for `.cvw` files](#IDESupport)

### Installation

Install/Update the project template.
Always ensure the template stays [up-to-date](https://www.nuget.org/packages/ConsoleMVC.Template):

```bash
dotnet new install ConsoleMVC.Template
```

Create a new project:

```bash
dotnet new consolemvc -n MyApp
cd MyApp
dotnet run
```

Add `--tests` for a test project too (`MyApp/MyApp.Tests`, using [ConsoleMVC.Testing](docs/guides/docs/testing.md)): `dotnet new consolemvc -n MyApp --tests`, then `dotnet test MyApp.Tests`.

This scaffolds a ready-to-run application with the following structure:

```
MyApp/
  MyApp.csproj
  Program.cs
  SharedResource.cs             # Names Resources/SharedResource.resx (English) and .pl.resx (Polish)
  appsettings.json              # Settings read by builder.Configuration
  MyApp.Tests/                  # Only with --tests: xUnit tests using ConsoleMVC.Testing
  Areas/Admin/                  # An area: Controllers/SettingsController.cs and Views/Settings/IndexView.cvw
  Controllers/
    HomeController.cs
    GreetController.cs          # Form posting with model binding
    CalcController.cs           # Form posting with simple parameter binding
  Models/
    HomeViewModel.cs
    FeaturesViewModel.cs        # Rows for the <table> on the feature tour page
    StatusViewModel.cs          # The status page, which refreshes every second
    GreetFormModel.cs           # Bound from form data (complex model)
    GreetResultModel.cs
    CalcResultModel.cs
  Services/
    IGreetingService.cs         # Registered in Program.cs, injected into GreetController and a view
    GreetingService.cs
    GreetingOptions.cs          # Bound from the Greeting section of appsettings.json
  Views/
    Home/
      IndexView.cvw             # <menu> navigation
      AboutView.cvw             # <markdown> from the model
      FeaturesView.cvw          # <table> generated from the model, paged by <scroll>
      StatusView.cvw            # @refresh 1s, <spinner>, <progress>, <sparkline> and <bar-chart>
    Greet/
      IndexView.cvw             # <input>, <select> and <checkbox>, posts the Form
      ResultView.cvw
    Calc/
      IndexView.cvw             # <number> and <radio> fields, posts the Form
      ResultView.cvw
    Shared/
      _Layout.cvw               # Frames every view: breadcrumbs, app name, separator
      PageHeader.cvw            # A partial view, used with <partial>
```

## How It Works

### Entry Point

`Program.cs` uses the familiar ASP.NET Core builder pattern:

```csharp
using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = MvcApplication.CreateBuilder(args);
builder.Services.AddSingleton<IGreetingService, GreetingService>(); // optional: your services
builder.Services.Configure<GreetingOptions>(builder.Configuration.GetSection("Greeting")); // optional: settings
using var app = builder.Build();
await app.RunAsync();                                               // or app.Run()
```

The framework scans the assembly at startup, discovers all controllers and views, and begins the main loop at the default route (`Home/Index`).

### Controllers

Controllers inherit from `Controller` and contain action methods that return `ActionResult`:

```csharp
using MyApp.Models;
using ConsoleMVC.Mvc;

namespace MyApp.Controllers;

public class HomeController : Controller
{
    public ActionResult Index()
    {
        var model = new HomeViewModel
        {
            Title = "Welcome",
            Message = "Select an option below."
        };

        return View(model);
    }

    public ActionResult Settings()
    {
        return RedirectToAction("Index"); // Navigate without rendering
    }
}
```

Any public method that returns `ActionResult` (or `Task<ActionResult>` / `ValueTask<ActionResult>`) is an action, and constructor parameters are supplied from `builder.Services`. Mark helper methods with `[NonAction]` to keep them out of navigation. Action names are matched case-insensitively and must be unique (overloads are not supported).

**Available methods:**

| Method | Description |
|--------|-------------|
| `View(model)` | Renders the view matching the current action, passing the model |
| `View()` | Renders the view without a model |
| `View("Action")` / `View("Action", model)` | Renders another action's view, e.g. a form again after validation fails |
| `TryValidateModel(model)` | Validates an object built in code |
| `RedirectToAction("action")` | Navigates to a different action on the same controller |
| `RedirectToAction("action", "controller")` | Navigates to an action on a different controller |

### Views (`.cvw` files)

Views use the `.cvw` (Console View) file format, plain C# code with an `@model` directive at the top. No class boilerplate, no inheritance, no interface implementation. The source generator compiles them into proper `ConsoleView<TModel>` classes at build time.

```
@model MyApp.Models.HomeViewModel

Console.WriteLine($"=== {Model.Title} ===");
Console.WriteLine(Model.Message);

Console.Write("Select: ");
var input = Console.ReadLine()?.Trim();

return input switch
{
    "1" => NavigationResult.To("Home", "About"),
    "2" => NavigationResult.Quit(),
    _ => NavigationResult.To("Home", "Index")
};
```

**Directives:**

| Directive | Purpose |
|-----------|---------|
| `@model FullTypeName` | Declares the ViewModel type. The model is available as `Model` in the view code. Optional; defaults to `object`. |
| `@using Namespace` | Adds a `using` statement to the generated class. |
| `@inject Type Name` | Declares a property filled from the application's services (see [Dependency Injection](#dependency-injection)). |
| `@layout Name` | Draws the view inside `Views/Shared/Name.cvw`; `@layout none` for no layout. Defaults to `_Layout` when it exists (see [Layouts](#layouts)). |

#### Layouts

`Views/Shared/_Layout.cvw` frames every screen. `<body />` marks where the view goes:

```
<text class="muted" align="right">My App</text>
<body />
<separator class="muted" />
```

The part before `<body />` is drawn before the view; the part after it is drawn just before the view first waits for the user (its first `<input>`, `<select>`, `<radio>`, `<checkbox>` or `<menu>`, or at the end if it has none), so a footer sits between the content and what the user answers. Views choose another layout with `@layout Name` or none with `@layout none`. See [Layouts](docs/guides/docs/c-view-markup.md#layouts-and-body) and the [design](docs/design/layout-views.md).

#### Layout and Themes

`<columns>` places content side by side and stacks it on narrow terminals; `class` applies named styles from the application's theme:

```
<columns gap="2" stack-below="60">
    <column width="30%"><box class="panel" title="Menu"><text>[1] Orders</text></box></column>
    <column><text class="title">Orders</text><text class="muted">@Model.Summary</text></column>
</columns>
```

```csharp
builder.Theme.Set("brand", new ThemeStyle { Foreground = Colour.Magenta, Bold = true });
```

The default theme defines `title`, `muted`, `success`, `warning`, `danger`, `info` and `panel`. Boxes and tables also accept `min-width` and `max-width`. See [Themes](docs/guides/docs/c-view-markup.md#themes-and-class) and [columns](docs/guides/docs/c-view-markup.md#columns-and-column).

#### Partial Views

Markup used by several views (a header, a footer, a group of form fields) goes in a partial view, `Views/Shared/{Name}.cvw`, and is rendered with `<partial>`:

```
@* Views/Shared/PageHeader.cvw *@
@model MyApp.Models.HomeViewModel
<box title="@Model.Title" border="rounded" color="cyan" padding="2">
    <text>@Model.Message</text>
</box>
```

```
@* Views/Home/IndexView.cvw *@
@model MyApp.Models.HomeViewModel
<partial name="PageHeader" model="@Model" />
<menu>…</menu>
```

`<partial>` compiles to a direct call, so the name and the model's type are checked when you build (CVW114, CVW115). A partial shares the view's `Form`, `ModelState` and `ViewData`, may use `@inject`, and cannot contain a `<menu>`. See the [guide](docs/guides/docs/c-view-markup.md#partial) and the [design](docs/design/partial-views.md).

**C-View Markup:**

Instead of formatting output and reading input by hand, views can describe the screen with markup. Markup and C# mix freely: top-level elements run in source order, and `@` switches back to C# inside an element:

```
@model MyApp.Models.HomeViewModel

<box title="@Model.Title" border="rounded" color="cyan" padding="2">
    <text>@Model.Message</text>
</box>

<menu key-color="cyan">
    <option key="1" nav="Home/About">About</option>
    <option key="2" nav="Users/Index">Users</option>
    <option key="q" quit>Exit</option>
</menu>
```

```
╭─ Welcome to ConsoleMVC ────────────────────────────────╮
│  A structured MVC framework for console applications.  │
╰────────────────────────────────────────────────────────╯
[1] About
[2] Users
[q] Exit
Select an option:
```

The `<menu>` shows the options, re-prompts on an invalid choice and returns the navigation, so the view needs no `return`. Tables are just as short:

```
<table border="rounded">
    <row header><cell>Name</cell><cell align="right">Age</cell></row>
    @foreach (var user in Model.Users)
    {
        <row><cell>@user.Name</cell><cell align="right">@user.Age</cell></row>
    }
</table>
```

Mistakes such as an unknown attribute or a missing closing tag are build errors pointing at the `.cvw` line, and C# errors inside views are mapped back to the `.cvw` file too. A `nav` target naming a controller or action that does not exist is a warning (CVW119) listing the ones that do; so are `RedirectToAction`, `NavigationResult.To` and `ToAction` in C# (CVW120), and `View("Name")` or `View()` without a view (CVW121, with an IDE fix that creates it). The full element and attribute reference is in the **[C-View Markup guide](docs/guides/docs/c-view-markup.md)**.

**Navigation from views** (a menu option marked `back` returns to the previous screen, and `<breadcrumbs />` shows the trail, e.g. `Home › Orders › Details`):

Every view must return a `NavigationResult` to tell the framework what to do next, either from C#, or by ending with a `<menu>`:

| Method | Description |
|--------|-------------|
| `NavigationResult.To("Controller", "Action")` | Navigate to a specific controller and action |
| `NavigationResult.To("Controller", "Action", formData)` | Navigate and post form data to the target action |
| `NavigationResult.ToAction("Action")` | Navigate to a different action on the current controller |
| `NavigationResult.ToAction("Action", formData)` | Navigate within the current controller and post form data |
| `NavigationResult.To("Orders/Details?id=42")` | Navigate with route values, which bind to the action's parameters (also `nav="Orders/Details?id=@order.Id"` and `RedirectToAction("Details", new { id })`) |
| `NavigationResult.Back()` | Return to the previous screen (the same as a menu option marked `back`) |
| `NavigationResult.Quit()` | Exit the application |

### Form Data Posting

Views can collect user input and post it to a controller action, just like HTML forms in web MVC. The framework binds the posted values to the target action's method parameters automatically.

**Collecting input with `<input>`, `<select>` and `<checkbox>`**: `Views/Greet/IndexView.cvw`:
```
@model object

<input name="Name" label="Name: " required />
<select name="Color" label="Favourite colour:" default="blue">
    <choice value="red">Red</choice>
    <choice value="green">Green</choice>
    <choice value="blue">Blue</choice>
</select>
<checkbox name="Shout" label="Shout the greeting" />

return NavigationResult.ToAction("Result", Form);
```

Each field stores its value in the view's `Form` dictionary under its `name`. Use `required` to re-prompt until a value is entered, `default` for a fallback value and `masked` for passwords. In a terminal, `<select>` and `<radio>` are answered with the arrow keys and Enter (Space ticks the choices of `<select multiple>`), and `<checkbox>` with Space; with piped input the choices are numbered and typed. `<checkbox>` posts `true` or `false` for a `bool`, and `<select multiple>` posts `red,blue` for a `List<string>` or an array. A menu option marked `post` sends the `Form` too: `<option key="s" nav="Result" post>Submit</option>`. See [choice inputs](docs/guides/docs/c-view-markup.md#select-radio-and-choice) and the [design](docs/design/choice-inputs.md).

**Collecting input in C#**: any `Dictionary<string, string>` can be posted, so plain C# views keep working:
```
@model object

Console.Write("Name: ");
var name = Console.ReadLine()?.Trim() ?? "";

Console.Write("Favourite colour: ");
var color = Console.ReadLine()?.Trim() ?? "";

var formData = new Dictionary<string, string>
{
    ["Name"] = name,
    ["Color"] = color
};

return NavigationResult.ToAction("Result", formData);
```

The framework supports two styles of parameter binding on the receiving action:

#### Complex Model Binding

Bind form data to a model class. The framework creates an instance and populates its properties from matching dictionary keys:

```csharp
public class GreetFormModel
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}
```

```csharp
public class GreetController : Controller
{
    public ActionResult Index()
    {
        return View();
    }

    public ActionResult Result(GreetFormModel model)
    {
        // model.Name and model.Color are automatically populated
        if (string.IsNullOrWhiteSpace(model.Name))
            return RedirectToAction("Index"); // Redirect back on validation failure

        return View(new GreetResultModel { Greeting = $"Hello, {model.Name}!" });
    }
}
```

#### Simple Parameter Binding

Bind form data directly to individual method parameters by name, which is useful when you don't need a model class:

```
@model object

Console.Write("First number:  ");
var a = Console.ReadLine()?.Trim() ?? "0";

Console.Write("Operator (+, -, *, /): ");
var op = Console.ReadLine()?.Trim() ?? "+";

Console.Write("Second number: ");
var b = Console.ReadLine()?.Trim() ?? "0";

var formData = new Dictionary<string, string>
{
    ["a"] = a,
    ["b"] = b,
    ["op"] = op
};

return NavigationResult.ToAction("Result", formData);
```

```csharp
public class CalcController : Controller
{
    public ActionResult Index()
    {
        return View();
    }

    // Each parameter is bound by name from the form data dictionary.
    // Type conversion (string → int) is handled automatically.
    public ActionResult Result(int a, int b, string op)
    {
        var result = op switch
        {
            "-" => a - b,
            "*" => a * b,
            "/" when b != 0 => a / b,
            _ => a + b
        };

        return View(new CalcResultModel { Expression = $"{a} {op} {b}", Result = result.ToString() });
    }
}
```

#### Supported Types

The model binder converts string values from the form data dictionary to the following types:

| Type | Examples |
|------|----------|
| `string` | Passed as-is |
| `int`, `long`, `short`, `byte` | Integer types |
| `float`, `double`, `decimal` | Floating-point types |
| `bool` | `"true"` / `"false"` |
| `Half`, `Int128`, `UInt128` | Additional numeric types |
| `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan` | Date/time types |
| `Guid`, `Uri`, `Version` | GUID, URI and version strings |
| `enum` | Enum name or numeric value; numbers must be defined members (or, for `[Flags]` enums, a combination of them) |
| Nullable variants (`int?`, `bool?`, etc.) | Empty string → `null` |
| Complex types (classes with properties) | Properties populated by name |

Key matching is **case-insensitive**: a form data key `"name"` binds to a parameter `Name` or a property `Name`.

When a key is missing or the value can't be converted, the parameter receives its default value (`null` for reference types, `0` for numeric types, `false` for `bool`, etc.). A value that can't be converted is also reported in `ModelState` (see below).

Numbers and dates are parsed with the invariant culture (`1.5`, `2026-10-27`), so a form behaves the same on every machine regardless of the user's regional settings.

### Validation

Posted form data is validated **before the action runs**, using the standard `System.ComponentModel.DataAnnotations` attributes, on model properties or directly on action parameters. Check `ModelState.IsValid` and show the form again when it is not, exactly as in ASP.NET Core MVC:

```csharp
using System.ComponentModel.DataAnnotations;

public class SignupModel
{
    [Required, StringLength(30)]
    public string Name { get; set; } = "";

    [Range(13, 120)]
    public int Age { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}

public class AccountController : Controller
{
    public ActionResult Signup() => View();

    public ActionResult Save(SignupModel model)
    {
        if (!ModelState.IsValid)
            return View("Signup", model);          // render SignupView again, with the errors

        if (users.Exists(model.Name))
        {
            ModelState.AddModelError("Name", "That name is taken.");
            return View("Signup", model);
        }

        return RedirectToAction("Welcome");
    }

    // Simple parameters can be validated too
    public ActionResult Rate([Range(1, 5)] int stars) => ModelState.IsValid ? View() : View("Signup", null);
}
```

The view shows the messages with `<errors />` (all errors) or `<errors for="Email" />` (one field):

```
<errors />
<input name="Name" label="Name: " />
<input name="Age" label="Age: " />
<input name="Email" label="E-mail: " />
<menu>
    <option key="s" nav="Save" post>Save</option>
</menu>
```

```
• The Name field is required.
• The value 'twelve' is not valid for Age.
Name: _
```

| API | Purpose |
|-----|---------|
| `ModelState.IsValid` | `true` when binding and validation produced no errors |
| `ModelState.AddModelError(key, message)` | Add your own error (an empty key for a general error) |
| `ModelState.ErrorsFor(key)` | The messages for one field |
| `View("ActionName", model)` / `View("ActionName")` | Render another action's view, e.g. the form again |
| `TryValidateModel(model)` | Validate an object built in code (calling it twice does not repeat errors) |
| `ConsoleView.ModelState` | The errors, available in the view |

Validation runs only when form data is posted. A value that can't be converted produces one message (`The value 'abc' is not valid for Age.`, using `[Display(Name = "…")]` when present) and that field is not validated again. Error keys are the form field names, so they match `<input name="…">`.

- **Several fields:** `IValidatableObject` is supported for rules that involve several fields. As in ASP.NET Core, it runs only once every attribute passes and no value failed to convert, so it never compares fallback values.
- **Required values:** `[Required]` checks reference and nullable types (`string`, `int?`). A non-nullable `int` always has a value (its default), so use `int?` with `[Required]` to demand one.
- **Records:** models without a parameterless constructor, such as `record Review([property: Range(1, 5)] int Stars, string Comment)`, are created through their constructor, whose parameters are bound by name.
- **Strings as models:** `View("Index")` renders the Index view, as in ASP.NET Core; pass a string model to the current view with `View((object)text)`.

### Dependency Injection

Services registered on `builder.Services` (the standard `Microsoft.Extensions.DependencyInjection` container) are passed to controller constructors and to views that declare `@inject`:

```csharp
builder.Services.AddSingleton<IGreetingService, GreetingService>();
builder.Services.AddScoped<IBasket, Basket>();
```

```csharp
public class GreetController(IGreetingService greetings) : Controller
{
    public ActionResult Result(GreetFormModel model) =>
        View(new GreetResultModel { Greeting = greetings.Greet(model.Name, model.Color) });
}
```

```
@model MyApp.Models.GreetResultModel
@inject MyApp.Services.IGreetingService Greetings
<text dim>Greetings sent so far: @Greetings.Count</text>
```

| Lifetime | Instance |
|----------|----------|
| `AddSingleton` | One for the whole run; disposed with `app` (`using var app = builder.Build();`) |
| `AddScoped` | One per navigation cycle (an action and the view it renders); disposed when the screen ends |
| `AddTransient` | A new one every time it is requested |

- Controllers and views that implement `IDisposable` are disposed at the end of their cycle; a redirect starts a new cycle.
- Scope validation is on, so a singleton depending on a scoped service fails at `Build()` rather than misbehaving later.
- `IMarkupRenderer` and `ITextInput` are registered for you (`builder.Renderer` / `builder.Input` take precedence) and can be injected.
- `app.Services` is the root provider, for preparing data before `app.Run()`.
- An unregistered dependency fails with an `InvalidOperationException` naming the missing type; `@inject` mistakes are build errors CVW007 and CVW008.

See the [Dependency Injection guide](docs/guides/docs/dependency-injection.md) and the [design](docs/design/dependency-injection.md).

### Async Actions and Middleware

Actions may be asynchronous, and a `CancellationToken` parameter receives the token passed to `RunAsync`:

```csharp
public async Task<ActionResult> Index(CancellationToken cancellationToken) =>
    View(await store.ListAsync(cancellationToken));
```

Middleware runs around every action, outermost first, as in ASP.NET Core:

```csharp
app.Use(async (context, next) =>
{
    try
    {
        await next(context);                 // the next middleware, then the action
    }
    catch (HttpRequestException)
    {
        context.RedirectTo("Offline", "Errors");
    }
});

app.UseMiddleware<RequireSignInMiddleware>(); // class-based: IActionMiddleware, created per screen
await app.RunAsync();
```

| API | Purpose |
|-----|---------|
| `Task<ActionResult>` / `ValueTask<ActionResult>` | Asynchronous actions (also `Task<ViewResult>` and other result types) |
| `app.RunAsync(token)` | Run the application; cancelling the token stops it before the next screen |
| `app.Use((context, next) => …)` | Inline middleware |
| `app.UseMiddleware<T>()` | Class-based middleware (`IActionMiddleware`), constructed per cycle with scoped services |
| `context.Result` / `context.RedirectTo(…)` | Replace the action's result, or skip the action by setting it without calling `next` |

Views render after the pipeline. See the [guide](docs/guides/docs/async-and-middleware.md) and the [design](docs/design/async-and-middleware.md).

### Areas and Modules

Group related screens in an area: `Areas/Admin/Controllers` (namespace `*.Areas.Admin.*`, or `[Area("Admin")]`) and `Areas/Admin/Views/{Controller}`. Area routes start with the area, `nav="Admin/Users/Index"`, and inside an area a controller named without one is the area's own. Feature modules add another assembly's controllers, views and services:

```csharp
builder.AddModule<BillingModule>();   // BillingModule : IMvcModule, in a class library
```

See the [Areas and Modules guide](docs/guides/docs/areas-and-modules.md) and the [design](docs/design/areas-and-modules.md).

### Live Screens

A long action can show its progress while it works. `ILiveDisplay` is registered for you; `Update` changes the element and redraws it in place:

```csharp
public class ImportController(ILiveDisplay live, IImporter importer) : Controller
{
    public async Task<ActionResult> Run(CancellationToken cancellationToken)
    {
        var progress = new ProgressElement { Label = "Import ", Max = importer.Count };
        await using (var region = live.Start(progress))           // Import ████████░░░░░░░░  50%
        {
            await foreach (var _ in importer.ImportAsync(cancellationToken))
                region.Update(() => progress.Value++);
        }

        return View();
    }
}
```

A view with `@refresh 5s` is drawn again every 5 seconds, running its action again for fresh data, until the user starts typing:

```
@model StatusViewModel
@refresh 5s

<spinner label="Watching the queue" frame="@DateTime.Now.Second" />
<progress label="Disk " value="@Model.DiskUsed" max="@Model.DiskSize" />
```

| API | Purpose |
|-----|---------|
| `<progress value max label bar-width percent style />` | A progress bar (`ProgressElement`) |
| `<spinner label style frame done />` | A spinner (`SpinnerElement`), turned by a live region at each tick |
| `ILiveDisplay.Start(element, interval?)` | Draws an element and keeps it up to date until the `LiveRegion` is disposed; on a pipe or file it is drawn once, in its final state |
| `@refresh 5s` | Redraws the view every interval (`ms`, `s` or `m`) until the user types; CVW010 for an invalid interval |
| `View(model).RefreshEvery(TimeSpan)` / `.NoRefresh()` | The action chooses, winning over `@refresh` |
| `ScriptedInput.Wait()` | In tests: a pause in which a refreshing screen is drawn again |

Dashboards can draw their numbers as charts:

```
<bar-chart format="N0">
    @foreach (var day in Model.Days) { <bar label="@day.Name" value="@day.Sales" /> }
</bar-chart>
<sparkline label="Queue " values="@Model.QueueHistory" show-last />     Queue ▁▂▂▃▅▇█▆▄▃▂▁ 3
```

See [charts](docs/guides/docs/c-view-markup.md#bar-chart-and-bar) and the [design](docs/design/charts.md).

Long content is paged with `<scroll height="15">…</scroll>`: the arrow keys, Page Up, Page Down, Home and End scroll it and Enter continues; piped output gets everything at once. See [`<scroll>`](docs/guides/docs/c-view-markup.md#scroll) and the [design](docs/design/full-screen.md).

Text written in Markdown (help pages, release notes) is drawn with `<markdown text="@Model.Notes" />`: headings, lists, quotes, fenced code, `**bold**`, `*italic*`, `` `code` `` and links, wrapped to the terminal. See [`<markdown />`](docs/guides/docs/c-view-markup.md#markdown) and the [design](docs/design/markdown.md).

See the [Live Screens guide](docs/guides/docs/live-screens.md) and the [design](docs/design/live-elements.md).

### Remote Sessions

One process can serve the application to many users at once, each with their own screen, history and session values:

```bash
dotnet run -- --ConsoleMVC:Serve=2323     # or: await app.ServeAsync(new TerminalServerOptions(), token);
telnet localhost 2323                     # from as many terminals as you like
```

Controllers and views ask for `ITerminalSession` to tell users apart (`session.Items["user"] = name`). The server listens
on the loopback address by default and needs a TLS certificate before it listens on the network; OpenSSH or ttyd put
SSH and a web browser in front of it. See the [Remote Sessions guide](docs/guides/docs/remote-sessions.md).

### Live Reload

Run `dotnet watch` in the project folder. Edits to `.cvw` views, controllers and models are applied to the running application and show from the next screen; newly added controllers and views are discovered after each update, so they are routable without a restart. See [Live reload](docs/guides/docs/getting-started.md#live-reload).

### Conventions

ConsoleMVC uses naming conventions to wire everything together, with no manual registration required:

| Convention | Example |
|------------|---------|
| Controllers are named `{Name}Controller` | `HomeController`, `SettingsController` |
| Controllers live in the `Controllers/` folder | `Controllers/HomeController.cs` |
| Views are named `{Action}View.cvw` | `IndexView.cvw`, `AboutView.cvw` |
| Views are placed in `Views/{Controller}/` | `Views/Home/IndexView.cvw` |
| View namespace matches the folder path | `MyApp.Views.Home` |
| Models live in the `Models/` folder | `Models/HomeViewModel.cs` |
| Default route is `Home/Index` | Configurable via `MvcApplicationBuilder` |

### Models

Models are simple DTOs (Data Transfer Objects), plain C# classes with properties:

```csharp
namespace MyApp.Models;

public class HomeViewModel
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public List<string> MenuOptions { get; set; } = [];
}
```

### Application Flow

**Basic navigation** (no form data):
```
Start → Home/Index (default route)
          ↓
    Controller.Index() returns View(model)
          ↓
    Framework resolves Views/Home/IndexView.cvw
          ↓
    Console is cleared, view renders, waits for input
          ↓
    View returns NavigationResult.To("Home", "About") (a <menu> option with nav="Home/About")
          ↓
    Controller.About() returns View(model)
          ↓
    ... continues until NavigationResult.Quit(); NavigationResult.Back() returns to the previous screen
```

**Form data posting** (view collects input → controller receives it):
```
    Greet/Index view renders, collects user input
          ↓
    View returns NavigationResult.ToAction("Result", formData)
          ↓
    Framework binds formData → GreetController.Result(GreetFormModel model)
          ↓
    Controller processes data, returns View(resultModel)
          ↓
    Greet/Result view renders the result
```

### <a id="IDESupport"></a> IDE Support for `.cvw` Files

The [ConsoleMVC CVWSupport](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport) plugin for JetBrains Rider understands directives and the C# code in views. Highlighting and completion for C-View Markup elements are planned for the plugin; until then markup is shown as plain text and errors are reported by the build.

To get basic C# syntax highlighting in `.cvw` files in other editors:

**JetBrains Rider / IntelliJ:**
Settings → Editor → File Types → find C# → add `*.cvw` pattern

**Visual Studio Code:**
Add to `settings.json`:
```json
{
    "files.associations": {
        "*.cvw": "csharp"
    }
}
```

## Adding New Pages

To add a new page, follow these three steps:

**1. Create the Model**: `Models/ContactViewModel.cs`
```csharp
namespace MyApp.Models;

public class ContactViewModel
{
    public string Email { get; set; } = "";
}
```

**2. Add the Controller Action**: in an existing or new controller
```csharp
public ActionResult Contact()
{
    var model = new ContactViewModel { Email = "hello@example.com" };
    return View(model);
}
```

**3. Create the View**: `Views/Home/ContactView.cvw`
```
@model MyApp.Models.ContactViewModel

<box title="Contact" border="rounded">
    <text>Contact us at: @Model.Email</text>
</box>

<menu>
    <option key="b" back>Back</option>
    <option key="q" quit>Exit</option>
</menu>
```

**4. Link to it** from another view: `<option key="5" nav="Home/Contact">Contact</option>`.

Build and run: the new page is discovered automatically, framed by the layout, and a typo in `nav` is reported by the build (CVW119).

## Configuration

The builder configures the application before it starts:

```csharp
var builder = MvcApplication.CreateBuilder(args);
builder.DefaultController = "Dashboard";        // Default: "Home"
builder.DefaultAction = "Overview";             // Default: "Index"
builder.ClearScreenBetweenViews = false;        // Default: true
builder.FullScreen = true;                      // Default: false
builder.Mouse = true;                           // Default: false (users: --ConsoleMVC:Mouse=true)
builder.ScreenReader = true;                    // Default: false (users: --ConsoleMVC:ScreenReader=true)
builder.HighContrast = true;                    // Default: false (users: --ConsoleMVC:HighContrast=true)
builder.ApplicationAssembly = typeof(Program).Assembly; // Default: the entry assembly
builder.Renderer = MarkupRenderer.CreateForConsole();   // Default: detected automatically
builder.Input = ConsoleTextInput.Instance;              // Default: the console
builder.Theme.Set("brand", new ThemeStyle { Bold = true }); // Default: Theme.CreateDefault()
builder.Services.AddSingleton<IClock, SystemClock>();   // Your services
builder.Services.Configure<ShopOptions>(builder.Configuration.GetSection("Shop")); // Settings
builder.Logging.AddFile("logs/app.log");                // Logs to a file, not over the screen
using var app = builder.Build();
app.Run();                                      // Or serve many users: --ConsoleMVC:Serve=2323
```

| Option | Purpose |
|--------|---------|
| `DefaultController` / `DefaultAction` | The first route to run |
| `Services` | Services for controller constructors and `@inject` (see [Dependency Injection](#dependency-injection)) |
| `Theme` | Named styles used by `class="…"` in markup (see [Layout and Themes](#layout-and-themes)) |
| `ClearScreenBetweenViews` | Clear the console before each view renders |
| `ScreenReader` | Plain text for screen readers: tables read as `Header: value`, no borders, bars or clearing ([Accessibility](docs/guides/docs/accessibility.md)) |
| `HighContrast` | Bold, bright theme colours with no dim text (`Theme.CreateHighContrast()`) |
| `Localization` | Culture, translated framework messages and validation messages ([Localisation](docs/guides/docs/localisation.md)) |
| `Mouse` | Click menu options, choices and checkboxes; the wheel scrolls ([Mouse](docs/guides/docs/controllers-and-forms.md#mouse)) |
| `FullScreen` | Run on the terminal's alternate screen; the user's scrollback is restored when the application ends |
| `ApplicationAssembly` | The assembly scanned for controllers and views (useful in tests or when hosting from another process) |
| `Input` | Where `<input>`, `<select>`, `<radio>`, `<checkbox>` and `<menu>` read from. Supply your own `ITextInput` to drive the application with scripted input in tests; implement `CanReadKeys` and `ReadKey` to script arrow keys too |
| `Configuration` | Settings from `appsettings.json`, `appsettings.{Environment}.json`, environment variables (`Shop__Name`) and the command line (`--Shop:Name=Corner`), later ones winning; registered as `IConfiguration` (see [Configuration Files and Logging](docs/guides/docs/hosting.md)) |
| `Environment` | `EnvironmentName` (`DOTNET_ENVIRONMENT`, default `Production`), `ContentRootPath` and `ApplicationName`; registered as `IHostEnvironment`. Choose them in code with `MvcApplication.CreateBuilder(new MvcApplicationOptions { … })` |
| `Logging` | Log providers and levels (the `Logging` section of the configuration). None is added by default, because the console is the user interface; `builder.Logging.AddFile("logs/app.log")` writes a rolling log file. The framework logs screens and failures under `ConsoleMVC.Mvc.MvcApplication` |
| `Renderer` | How markup is drawn. The default detects colour support (`NO_COLOR`, `COLORTERM`, Windows VT mode), terminal width and redirected output, and redraws the current screen when the terminal is resized (`MarkupRenderer.CreateForConsole(redrawOnResize: false)` turns that off). Use `new MarkupRenderer(new PlainTextRenderTarget(writer), () => 60)` for plain text at a fixed width |

## Roadmap

The full list, with the reasoning behind it, is in [Planned Features & Project Direction](plannedfeatures.md). Every feature planned for the framework is now built, from full-screen mode, live elements and charts to Native AOT and remote sessions. What remains is **IDE support for markup**, developed in its own repository, [ConsoleMVC-CVWSupport](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport).

Ideas and feedback are welcome in the [issues](https://github.com/MateuszPodeszwa/ConsoleMVC/issues).

## Package Information

| Package | Description | Purpose |
|---------|-------------|---------|
| [`ConsoleMVC.Framework`](https://www.nuget.org/packages/ConsoleMVC.Framework) | Framework library + source generator | Referenced by your project |
| [`ConsoleMVC.Testing`](https://www.nuget.org/packages/ConsoleMVC.Testing) | Test host, scripted input and snapshots | Referenced by your test project ([guide](docs/guides/docs/testing.md)) |
| [`ConsoleMVC.Template`](https://www.nuget.org/packages/ConsoleMVC.Template) | `dotnet new` project template | Used to scaffold new projects |

All three packages are published on [NuGet.org](https://www.nuget.org/profiles/MateuszPodeszwa). The framework package is named `ConsoleMVC.Framework` (rather than `ConsoleMVC`) because the `ConsoleMVC` identifier was already reserved on nuget.org by an unrelated package.

## License

This project is licensed under the [MIT License](LICENSE).

---

## For Maintainers

This section documents how to build, pack, and publish new versions of the framework and template packages.

### Repository Structure

```
ConsoleMVC/
  ConsoleMVC.sln                          # Solution file
  ConsoleMVC.Template.csproj              # Packs the dotnet new template
  LICENSE
  readme.md
  src/
    ConsoleMVC/                           # Framework library (NuGet: ConsoleMVC.Framework)
      ConsoleMVC.csproj                   #   Packs lib + generator + build props
      build/ConsoleMVC.Framework.props    #   Auto-imported by consuming projects
      PACKAGE_README.md                   #   README shown on NuGet gallery
      Application/ Controllers/ Results/  #   MVC framework (namespace ConsoleMVC.Mvc)
      Routing/ Binding/ Views/
      Validation/ DependencyInjection/ Middleware/
      Hosting/                            #   Configuration, environment and logging for the builder
      Rendering/                          #   Rendering engine (namespace ConsoleMVC.Rendering, no MVC dependency)
    ConsoleMVC.Testing/                   # Test host, scripted input and snapshots (NuGet: ConsoleMVC.Testing)
    ConsoleMVC.Generators/                # Source generator (bundled into ConsoleMVC package)
      ConsoleMVC.Generators.csproj
      ViewSourceGenerator.cs              #   Pipeline: .cvw → parse → diagnostics → emit ConsoleView<T>
      Parsing/ Syntax/ Schema/            #   Directive, C# and markup parsing; element/attribute schema
      Emit/ Diagnostics/                  #   C# generation with #line mapping; CVWxxx diagnostics
      Analysis/                           #   NavigationAnalyzer: CVW120 and CVW121 for C# navigation
    ConsoleMVC.CodeFixes/                 # IDE code fixes (create a missing view), bundled into ConsoleMVC package
  tests/
    ConsoleMVC.Tests/                     # Framework and rendering unit/integration tests
    ConsoleMVC.Generators.Tests/          # Runs the generator, compiles and executes views
  docs/
    design/                               # Designs: UML, grammar, pseudocode for each feature
    guides/                               # MkDocs user guides
  templates/
    ConsoleMVC.App/                       # Template content (what users get from dotnet new)
      .template.config/template.json      #   Template engine configuration
      ConsoleMVC.App.csproj               #   sourceName replaced with user's project name
      Program.cs
      appsettings.json                    #   Settings read by builder.Configuration
      Controllers/
      Models/
      Services/
      Views/                              #   including Views/Shared partials
```

### Building and Testing

```bash
dotnet build ConsoleMVC.sln
dotnet test ConsoleMVC.sln
```

### Releasing a New Version

**1. Bump version numbers** in the three packed `.csproj` files:

- `src/ConsoleMVC/ConsoleMVC.csproj`: update `<Version>`
- `src/ConsoleMVC.Testing/ConsoleMVC.Testing.csproj`: update `<Version>`
- `ConsoleMVC.Template.csproj`: update `<Version>`

Also update the `<PackageReference>` versions in `templates/ConsoleMVC.App/ConsoleMVC.App.csproj` (ConsoleMVC.Framework) and `templates/ConsoleMVC.App/ConsoleMVC.App.Tests/ConsoleMVC.App.Tests.csproj` (ConsoleMVC.Testing).

**2. Merge to `main`.** The [Publish packages](.github/workflows/publish.yml) workflow notices that the version has no `vX.Y.Z` tag yet and:

- checks that the three `<Version>` values and the template's package references agree;
- builds with `-warnaserror` and runs every test;
- packs `ConsoleMVC.Framework`, `ConsoleMVC.Testing` and `ConsoleMVC.Template`;
- pushes them to **NuGet.org** with [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (no stored API key: GitHub issues a short-lived token that nuget.org exchanges for a one-hour key) and to **GitHub Packages** with the built-in `GITHUB_TOKEN`;
- creates the GitHub release `vX.Y.Z` with this version's section of `CHANGELOG.md` and the `.nupkg` files attached.

Publishing a release by hand, or **Actions → Publish packages → Run workflow**, publishes the declared version too; versions already on NuGet.org are skipped.

**One-time setup on nuget.org:** open your profile → **Trusted Publishing** → add a policy for the packages' owner with:

| Field | Value |
|---|---|
| Repository Owner | `MateuszPodeszwa` |
| Repository | `ConsoleMVC` |
| Workflow File | `publish.yml` |
| Environment | `release` |
| Glob patterns and packages | `ConsoleMVC.*` (covers `ConsoleMVC.Framework`, `ConsoleMVC.Testing`, `ConsoleMVC.Template` and future packages) |
| Scope | Push new versions of existing packages (with `ConsoleMVC.*`, also push new packages) |

The workflow logs in as the nuget.org profile `MateuszPodeszwa`; set a repository variable `NUGET_USER` to use another account. GitHub creates the `release` environment on the first run; add required reviewers to it under **Settings → Environments** if you want to approve each release.

**To publish manually** instead:

```bash
dotnet pack src/ConsoleMVC/ConsoleMVC.csproj -c Release -o nupkg
dotnet pack src/ConsoleMVC.Testing/ConsoleMVC.Testing.csproj -c Release -o nupkg
dotnet pack ConsoleMVC.Template.csproj -c Release -o nupkg
dotnet nuget push "nupkg/*.nupkg" --api-key YOUR_NUGET_API_KEY --source nuget.org   # a key from nuget.org/account/apikeys
dotnet nuget push "nupkg/*.nupkg" --api-key YOUR_GITHUB_TOKEN --source https://nuget.pkg.github.com/MateuszPodeszwa/index.json
```

**3. Verify** the packages appear on your [NuGet.org profile](https://www.nuget.org/profiles/MateuszPodeszwa) and under the repository's **Packages**.

**Installing from GitHub Packages** needs a personal access token with `read:packages`:

```bash
dotnet nuget add source https://nuget.pkg.github.com/MateuszPodeszwa/index.json \
    --name github --username YOUR_GITHUB_USERNAME --password YOUR_TOKEN
dotnet add package ConsoleMVC.Framework --source github
```

### Updating the Template

When modifying the template content (files in `templates/ConsoleMVC.App/`):

- All namespaces in template files must use `ConsoleMVC.App`: this is the `sourceName` in `template.json` and gets replaced with the user's project name at scaffolding time
- The `@model` directives in `.cvw` files must also use `ConsoleMVC.App.Models.ClassName`
- The `--tests` option is a `symbols` entry in `template.json`; files under `ConsoleMVC.App.Tests/` are excluded without it, and `<!--#if (tests) -->` blocks in the project file are kept only with it
- If a change alters the home menu, regenerate `ConsoleMVC.App.Tests/__snapshots__/home-menu.txt` (CI checks it by generating a project with `--tests`)
- After changes, repack and republish the `ConsoleMVC.Template` package

### Updating the Source Generator

When modifying the source generator (`src/ConsoleMVC.Generators`):

- The generator targets `netstandard2.0`: avoid APIs not available in that target
- To add a markup element, add its runtime class under `src/ConsoleMVC/Rendering/Elements` and register it in `Schema/MarkupSchema.cs`; add a rendering test in `ConsoleMVC.Generators.Tests`
- New diagnostics need an entry in `AnalyzerReleases.Unshipped.md`
- `Environment.NewLine` and similar banned symbols cannot be used (Roslyn analyzer restriction)
- After changes, you must repack the `ConsoleMVC` package since the generator DLL is bundled inside it
- Clear the local NuGet cache (`rm -rf ~/.nuget/packages/consolemvc.framework`) when testing locally to avoid stale generator DLLs

### Version Strategy

Follow [SemVer](https://semver.org/):
- **Patch** (1.0.x): Bug fixes, documentation updates
- **Minor** (1.x.0): New features, new base class methods, backward-compatible additions
- **Major** (x.0.0): Breaking changes to the public API (Controller, ConsoleView, NavigationResult, etc.)

Keep both packages' versions in sync: when the framework version bumps, update the template's `PackageReference` and bump the template version too.
