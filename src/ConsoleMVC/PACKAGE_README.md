# ConsoleMVC

An MVC framework for console applications, inspired by ASP.NET Core MVC.

ConsoleMVC brings the familiar **Controller-ViewModel** pattern to console apps — with convention-based routing, reflection-based auto-discovery, and Razor-like `.cvw` view templates powered by a C# source generator.

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

**Program.cs** — familiar ASP.NET-style builder:
```csharp
var builder = MvcApplication.CreateBuilder(args);
builder.Services.AddSingleton<IGreetingService, GreetingService>(); // optional
using var app = builder.Build();
await app.RunAsync();
```

**Controllers** — inherit from `Controller`, return `ActionResult`:
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

**Views** — `.cvw` files with an `@model` directive and C-View Markup, no class boilerplate:
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

**Models** — simple DTOs:
```csharp
public class HomeViewModel
{
    public string Title { get; set; } = "";
}
```

## Features

- **Convention-based routing** — Controllers and Views are auto-discovered via reflection and namespace conventions
- **Razor-like view engine** — `.cvw` files are compiled into `ConsoleView<TModel>` classes at build time by a source generator
- **No boilerplate** — Views are plain C# code with an `@model` directive, no class inheritance required
- **C-View Markup** — `<box>`, `<text>`, `<separator>`, `<br>` and `<table>` elements with colours, borders, alignment and word wrapping, plus `@` expressions and `@if`/`@foreach`; validated at compile time with errors pointing at the `.cvw` file
- **Layout and themes** — `<columns>` (stacking on narrow terminals), `min-width`/`max-width`, and theme classes with `class="title"`
- **Partial views** — reusable markup in `Views/Shared/{Name}.cvw`, rendered with `<partial name="Header" model="@Model.Title" />` and checked at build time
- **Declarative input and navigation** — `<input>` fields (required, default, masked) collect a `Form`, and `<menu>` reads the choice and navigates
- **Secure output** — model values are sanitised so they cannot inject terminal escape sequences
- **Reusable rendering engine** — the `ConsoleMVC.Rendering` namespace can draw elements in any console app
- **Familiar API** — `View()`, `RedirectToAction()`, `ViewData` — all the patterns you know from ASP.NET Core MVC
- **Navigation** — Views return `NavigationResult` to control app flow (`NavigationResult.To()`, `NavigationResult.Quit()`)
- **Validation** — posted forms are checked with DataAnnotations (`[Required]`, `[Range]`, `[EmailAddress]`, …) before the action runs; use `ModelState.IsValid`, `View("Form", model)` and `<errors />` as in ASP.NET Core MVC
- **Dependency injection** — `builder.Services` (Microsoft.Extensions.DependencyInjection); constructor injection into controllers, `@inject Type Name` in views, one scope per screen
- **Async actions and middleware** — `Task<ActionResult>` actions, `RunAsync(token)`, and `app.Use(...)` / `UseMiddleware<T>()` around every action
- **Live reload** — `dotnet watch` applies `.cvw` and C# edits to the running app from the next screen; new pages are discovered automatically
- **Form data posting** — Views collect user input via `Console.ReadLine()` and post it to controller actions using `NavigationResult.To("Controller", "Action", formData)`. The framework automatically binds the data to action method parameters — either as individual simple types (`int`, `string`, etc.) or as a complex model class with matching property names

## IDE Support

For `.cvw` file recognition, syntax highlighting, and icon support in JetBrains Rider, install the **ConsoleMVC CVWSupport** plugin:

- [JetBrains Marketplace](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport)
- [Plugin Source Code](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport)

## Documentation

For full documentation and source code, visit the [GitHub repository](https://github.com/MateuszPodeszwa/ConsoleMVC).
