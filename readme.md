# ConsoleMVC

> **C-View Markup:** describe console screens with `<box>`, `<text>`, `<table>`, `<input>` and `<menu>`, embed C# with Razor-style `@` expressions, and let the build compile it all to plain C#. New in 1.3: tables, form inputs and menus. See the **[C-View Markup guide](docs/guides/docs/c-view-markup.md)** and the roadmap in **[Planned Features & Project Direction](plannedfeatures.md)**.

An MVC framework for .NET console applications, inspired by ASP.NET Core MVC.

ConsoleMVC brings the **Controller-ViewModel** pattern to console apps — with convention-based routing, reflection-based auto-discovery, and a Razor-like view engine powered by a C# source generator. If you've built ASP.NET Core MVC apps before, you already know how to use ConsoleMVC.

## Why ConsoleMVC?

Console applications often end up as unstructured, hard-to-maintain code. UI frameworks like Spectre.Console help with presentation, but they don't solve the underlying architectural problem. ConsoleMVC provides:

- **Separation of concerns** — Controllers handle logic, Views handle rendering, Models carry data
- **Convention over configuration** — Controllers and Views are auto-discovered at startup via reflection
- **Familiar API** — `View()`, `RedirectToAction()`, `ViewData` — the same patterns from ASP.NET Core MVC
- **Razor-like view engine** — `.cvw` files are plain C# with an `@model` directive, compiled at build time by a source generator
- **C-View Markup** — declarative `<box>`, `<text>`, `<separator>` and `<table>` elements with colours, borders, alignment and word wrapping, checked at compile time
- **Declarative input and navigation** — `<input>` collects form values and `<menu>` reads the user's choice and navigates, with no `Console.ReadLine()` or `switch` boilerplate
- **Safe by default** — model data is sanitised before it reaches the terminal, so it cannot inject escape sequences
- **Form data posting** — Views collect user input and post it to controller actions with automatic model binding, just like HTML forms in web MVC
- **Validation** — posted forms are checked with the standard DataAnnotations attributes (`[Required]`, `[Range]`, …) before the action runs; `ModelState` and `<errors />` work as in ASP.NET Core MVC
- **Dependency injection** — register services with `builder.Services`; controllers receive them through their constructors and views through `@inject`, with one scope per screen

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- JetBrain's Rider (personally recommended for the [plugin](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport/edit) support), VS plugin is planned
- If using VS code or other, [add IDE support for cvw filles](#IDESupport)

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

This scaffolds a ready-to-run application with the following structure:

```
MyApp/
  MyApp.csproj
  Program.cs
  Controllers/
    HomeController.cs
    GreetController.cs          # Form posting with model binding
    CalcController.cs           # Form posting with simple parameter binding
  Models/
    HomeViewModel.cs
    FeaturesViewModel.cs        # Rows for the <table> on the feature tour page
    GreetFormModel.cs           # Bound from form data (complex model)
    GreetResultModel.cs
    CalcResultModel.cs
  Views/
    Home/
      IndexView.cvw             # <menu> navigation
      AboutView.cvw
      FeaturesView.cvw          # <table> generated from the model
    Greet/
      IndexView.cvw             # <input> fields, posts the Form
      ResultView.cvw
    Calc/
      IndexView.cvw             # <input> fields, posts the Form
      ResultView.cvw
```

## How It Works

### Entry Point

`Program.cs` uses the familiar ASP.NET Core builder pattern:

```csharp
using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = MvcApplication.CreateBuilder(args);
builder.Services.AddSingleton<IGreetingService, GreetingService>(); // optional: your services
using var app = builder.Build();
app.Run();
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

Any public method that returns `ActionResult` is an action. Mark helper methods with `[NonAction]` to keep them out of navigation. Action names are matched case-insensitively and must be unique (overloads are not supported).

**Available methods:**

| Method | Description |
|--------|-------------|
| `View(model)` | Renders the view matching the current action, passing the model |
| `View()` | Renders the view without a model |
| `RedirectToAction("action")` | Navigates to a different action on the same controller |
| `RedirectToAction("action", "controller")` | Navigates to an action on a different controller |

### Views (`.cvw` files)

Views use the `.cvw` (Console View) file format — plain C# code with an `@model` directive at the top. No class boilerplate, no inheritance, no interface implementation. The source generator compiles them into proper `ConsoleView<TModel>` classes at build time.

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
| `@model FullTypeName` | Declares the ViewModel type. The model is available as `Model` in the view code. Optional — defaults to `object`. |
| `@using Namespace` | Adds a `using` statement to the generated class. |
| `@inject Type Name` | Declares a property filled from the application's services (see [Dependency Injection](#dependency-injection)). |

**C-View Markup:**

Instead of formatting output and reading input by hand, views can describe the screen with markup. Markup and C# mix freely — top-level elements run in source order, and `@` switches back to C# inside an element:

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

Mistakes such as an unknown attribute or a missing closing tag are build errors pointing at the `.cvw` line, and C# errors inside views are mapped back to the `.cvw` file too. The full element and attribute reference is in the **[C-View Markup guide](docs/guides/docs/c-view-markup.md)**.

**Navigation from views:**

Every view must return a `NavigationResult` to tell the framework what to do next — either from C#, or by ending with a `<menu>`:

| Method | Description |
|--------|-------------|
| `NavigationResult.To("Controller", "Action")` | Navigate to a specific controller and action |
| `NavigationResult.To("Controller", "Action", formData)` | Navigate and post form data to the target action |
| `NavigationResult.ToAction("Action")` | Navigate to a different action on the current controller |
| `NavigationResult.ToAction("Action", formData)` | Navigate within the current controller and post form data |
| `NavigationResult.Quit()` | Exit the application |

### Form Data Posting

Views can collect user input and post it to a controller action — just like HTML forms in web MVC. The framework binds the posted values to the target action's method parameters automatically.

**Collecting input with `<input>`** — `Views/Greet/IndexView.cvw`:
```
@model object

<input name="Name" label="Name: " required />
<input name="Color" label="Favourite colour: " />

return NavigationResult.ToAction("Result", Form);
```

Each `<input>` stores its value in the view's `Form` dictionary under its `name`. Use `required` to re-prompt until a value is entered, `default` for a fallback value and `masked` for passwords. A menu option marked `post` sends the `Form` too: `<option key="s" nav="Result" post>Submit</option>`.

**Collecting input in C#** — any `Dictionary<string, string>` can be posted, so plain C# views keep working:
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

Bind form data directly to individual method parameters by name — useful when you don't need a model class:

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

Key matching is **case-insensitive** — a form data key `"name"` binds to a parameter `Name` or a property `Name`.

When a key is missing or the value can't be converted, the parameter receives its default value (`null` for reference types, `0` for numeric types, `false` for `bool`, etc.). A value that can't be converted is also reported in `ModelState` (see below).

Numbers and dates are parsed with the invariant culture (`1.5`, `2026-10-27`), so a form behaves the same on every machine regardless of the user's regional settings.

### Validation

Posted form data is validated **before the action runs**, using the standard `System.ComponentModel.DataAnnotations` attributes — on model properties or directly on action parameters. Check `ModelState.IsValid` and show the form again when it is not, exactly as in ASP.NET Core MVC:

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

### Conventions

ConsoleMVC uses naming conventions to wire everything together — no manual registration required:

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

Models are simple DTOs (Data Transfer Objects) — plain C# classes with properties:

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
    View returns NavigationResult.To("Home", "About")
          ↓
    Controller.About() returns View(model)
          ↓
    ... continues until NavigationResult.Quit()
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

**1. Create the Model** — `Models/ContactViewModel.cs`
```csharp
namespace MyApp.Models;

public class ContactViewModel
{
    public string Email { get; set; } = "";
}
```

**2. Add the Controller Action** — in an existing or new controller
```csharp
public ActionResult Contact()
{
    var model = new ContactViewModel { Email = "hello@example.com" };
    return View(model);
}
```

**3. Create the View** — `Views/Home/ContactView.cvw`
```
@model MyApp.Models.ContactViewModel

Console.WriteLine($"Contact us at: {Model.Email}");
Console.Write("Press any key to go back...");
Console.ReadKey();

return NavigationResult.To("Home", "Index");
```

Build and run — the new page is automatically discovered and routable.

## Configuration

The builder configures the application before it starts:

```csharp
var builder = MvcApplication.CreateBuilder(args);
builder.DefaultController = "Dashboard";        // Default: "Home"
builder.DefaultAction = "Overview";             // Default: "Index"
builder.ClearScreenBetweenViews = false;        // Default: true
builder.ApplicationAssembly = typeof(Program).Assembly; // Default: the entry assembly
builder.Renderer = MarkupRenderer.CreateForConsole();   // Default: detected automatically
builder.Input = ConsoleTextInput.Instance;              // Default: the console
builder.Services.AddSingleton<IClock, SystemClock>();   // Your services
using var app = builder.Build();
app.Run();
```

| Option | Purpose |
|--------|---------|
| `DefaultController` / `DefaultAction` | The first route to run |
| `Services` | Services for controller constructors and `@inject` (see [Dependency Injection](#dependency-injection)) |
| `ClearScreenBetweenViews` | Clear the console before each view renders |
| `ApplicationAssembly` | The assembly scanned for controllers and views (useful in tests or when hosting from another process) |
| `Input` | Where `<input>` and `<menu>` read from. Supply your own `ITextInput` to drive the application with scripted input in tests |
| `Renderer` | How markup is drawn. The default detects colour support (`NO_COLOR`, `COLORTERM`, Windows VT mode), terminal width and redirected output. Use `new MarkupRenderer(new PlainTextRenderTarget(writer), () => 60)` for plain text at a fixed width |

## Package Information

| Package | Description | Purpose |
|---------|-------------|---------|
| [`ConsoleMVC.Framework`](https://www.nuget.org/packages/ConsoleMVC.Framework) | Framework library + source generator | Referenced by your project |
| [`ConsoleMVC.Template`](https://www.nuget.org/packages/ConsoleMVC.Template) | `dotnet new` project template | Used to scaffold new projects |

Both packages are published on [NuGet.org](https://www.nuget.org/profiles/MateuszPodeszwa). The framework package is named `ConsoleMVC.Framework` (rather than `ConsoleMVC`) because the `ConsoleMVC` identifier was already reserved on nuget.org by an unrelated package.

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
      Validation/ DependencyInjection/
      Rendering/                          #   Rendering engine (namespace ConsoleMVC.Rendering, no MVC dependency)
    ConsoleMVC.Generators/                # Source generator (bundled into ConsoleMVC package)
      ConsoleMVC.Generators.csproj
      ViewSourceGenerator.cs              #   Pipeline: .cvw → parse → diagnostics → emit ConsoleView<T>
      Parsing/ Syntax/ Schema/            #   Directive, C# and markup parsing; element/attribute schema
      Emit/ Diagnostics/                  #   C# generation with #line mapping; CVWxxx diagnostics
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
      Controllers/
      Models/
      Services/
      Views/
```

### Building and Testing

```bash
dotnet build ConsoleMVC.sln
dotnet test ConsoleMVC.sln
```

### Releasing a New Version

**1. Bump version numbers** in both `.csproj` files:

- `src/ConsoleMVC/ConsoleMVC.csproj` — update `<Version>`
- `ConsoleMVC.Template.csproj` — update `<Version>`

If the template references a new framework version, also update the `<PackageReference>` version in `templates/ConsoleMVC.App/ConsoleMVC.App.csproj`.

**2. Pack both packages:**

```bash
dotnet pack src/ConsoleMVC/ConsoleMVC.csproj -c Release -o nupkg
dotnet pack ConsoleMVC.Template.csproj -c Release -o nupkg
```

**3. Publish to NuGet.org:**

```bash
dotnet nuget push "nupkg/ConsoleMVC.Framework.X.Y.Z.nupkg" --api-key YOUR_NUGET_API_KEY --source nuget.org
dotnet nuget push "nupkg/ConsoleMVC.Template.X.Y.Z.nupkg" --api-key YOUR_NUGET_API_KEY --source nuget.org
```

Replace `X.Y.Z` with the new version number and `YOUR_NUGET_API_KEY` with an API key from your [nuget.org account](https://www.nuget.org/account/apikeys).

**4. Verify** the packages appear on your [NuGet.org profile](https://www.nuget.org/profiles/MateuszPodeszwa).

### Updating the Template

When modifying the template content (files in `templates/ConsoleMVC.App/`):

- All namespaces in template files must use `ConsoleMVC.App` — this is the `sourceName` in `template.json` and gets replaced with the user's project name at scaffolding time
- The `@model` directives in `.cvw` files must also use `ConsoleMVC.App.Models.ClassName`
- After changes, repack and republish the `ConsoleMVC.Template` package

### Updating the Source Generator

When modifying the source generator (`src/ConsoleMVC.Generators`):

- The generator targets `netstandard2.0` — avoid APIs not available in that target
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

Keep both packages' versions in sync — when the framework version bumps, update the template's `PackageReference` and bump the template version too.
