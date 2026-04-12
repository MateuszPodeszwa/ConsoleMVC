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
var app = builder.Build();
app.Run();
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

**Views** — `.cvw` files with `@model` directive, no class boilerplate:
```
@model MyApp.Models.HomeViewModel

Console.WriteLine(Model.Title);
Console.Write("Press any key...");
Console.ReadKey();

return NavigationResult.To("Home", "Index");
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
- **Familiar API** — `View()`, `RedirectToAction()`, `ViewData` — all the patterns you know from ASP.NET Core MVC
- **Navigation** — Views return `NavigationResult` to control app flow (`NavigationResult.To()`, `NavigationResult.Quit()`)

## Documentation

For full documentation and source code, visit the [GitHub repository](https://github.com/MateuszPodeszwa/ConsoleMVC).
