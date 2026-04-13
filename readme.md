# ConsoleMVC

An MVC framework for .NET console applications, inspired by ASP.NET Core MVC.

ConsoleMVC brings the **Controller-ViewModel** pattern to console apps — with convention-based routing, reflection-based auto-discovery, and a Razor-like view engine powered by a C# source generator. If you've built ASP.NET Core MVC apps before, you already know how to use ConsoleMVC.

## Why ConsoleMVC?

Console applications often end up as unstructured, hard-to-maintain code. UI frameworks like Spectre.Console help with presentation, but they don't solve the underlying architectural problem. ConsoleMVC provides:

- **Separation of concerns** — Controllers handle logic, Views handle rendering, Models carry data
- **Convention over configuration** — Controllers and Views are auto-discovered at startup via reflection
- **Familiar API** — `View()`, `RedirectToAction()`, `ViewData` — the same patterns from ASP.NET Core MVC
- **Razor-like view engine** — `.cvw` files are plain C# with an `@model` directive, compiled at build time by a source generator

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later

### Installation

Install the project template:

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
  Models/
    HomeViewModel.cs
  Views/
    Home/
      IndexView.cvw
      AboutView.cvw
```

## How It Works

### Entry Point

`Program.cs` uses the familiar ASP.NET Core builder pattern:

```csharp
using ConsoleMVC.Mvc;

var builder = MvcApplication.CreateBuilder(args);
var app = builder.Build();
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
| `@model FullTypeName` | Declares the ViewModel type. The model is available as `Model` in the view code. |
| `@using Namespace` | Adds a `using` statement to the generated class. |

**Navigation from views:**

Every view must return a `NavigationResult` to tell the framework what to do next:

| Method | Description |
|--------|-------------|
| `NavigationResult.To("Controller", "Action")` | Navigate to a specific controller and action |
| `NavigationResult.ToAction("Action")` | Navigate to a different action on the current controller |
| `NavigationResult.Quit()` | Exit the application |

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

### IDE Support for `.cvw` Files

To get C# syntax highlighting in `.cvw` files:

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

The builder allows configuring the default route:

```csharp
var builder = MvcApplication.CreateBuilder(args);
builder.DefaultController = "Dashboard";  // Default: "Home"
builder.DefaultAction = "Overview";       // Default: "Index"
var app = builder.Build();
app.Run();
```

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
      *.cs                                #   Framework source (Controller, Router, etc.)
    ConsoleMVC.Generators/                # Source generator (bundled into ConsoleMVC package)
      ConsoleMVC.Generators.csproj
      ViewSourceGenerator.cs              #   Transforms .cvw → ConsoleView<T> at compile time
  templates/
    ConsoleMVC.App/                       # Template content (what users get from dotnet new)
      .template.config/template.json      #   Template engine configuration
      ConsoleMVC.App.csproj               #   sourceName replaced with user's project name
      Program.cs
      Controllers/
      Models/
      Views/
```

### Building

```bash
dotnet build ConsoleMVC.sln
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

When modifying `ViewSourceGenerator.cs`:

- The generator targets `netstandard2.0` — avoid APIs not available in that target
- `Environment.NewLine` and similar banned symbols cannot be used (Roslyn analyzer restriction)
- After changes, you must repack the `ConsoleMVC` package since the generator DLL is bundled inside it
- Clear the local NuGet cache (`rm -rf ~/.nuget/packages/consolemvc.framework`) when testing locally to avoid stale generator DLLs

### Version Strategy

Follow [SemVer](https://semver.org/):
- **Patch** (1.0.x): Bug fixes, documentation updates
- **Minor** (1.x.0): New features, new base class methods, backward-compatible additions
- **Major** (x.0.0): Breaking changes to the public API (Controller, ConsoleView, NavigationResult, etc.)

Keep both packages' versions in sync — when the framework version bumps, update the template's `PackageReference` and bump the template version too.
