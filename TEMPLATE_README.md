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
```

## What You Get

A ready-to-run console application structured with the MVC pattern:

- **Program.cs**: entry point using the familiar `CreateBuilder()`, `Build()`, `RunAsync()` pattern, with a middleware that shows the current page in the terminal title
- **Controllers/**: controller classes that handle actions and return results
- **Views/**: `.cvw` views using C-View Markup (`<box>`, `<text>`, `<table>`, `<input>`, `<menu>`) and C#, compiled at build time
- **Models/**: ViewModel DTOs passed from controllers to views
- **Services/**: an `IGreetingService` registered with `builder.Services` in `Program.cs`

The template includes working examples of:

- **Navigation**: `<menu>` elements moving between controllers and actions (`Home`, `About`)
- **Tables, columns and themes**: a feature tour page with a `<table>` generated from the model, `<columns>` that stack on narrow terminals, and theme classes (`class="panel"`)
- **Partial views**: the home and about pages share `Views/Shared/PageHeader.cvw` through `<partial>`
- **Form data posting with model binding**: a greeting form that collects input and posts it to a controller as a `GreetFormModel` class, validated with `[Required]` and `[StringLength]` and showing errors with `<errors />`
- **Dependency injection**: `GreetController` receives `IGreetingService` through its constructor and the result view reads it with `@inject`
- **Form data posting with simple parameters**: a calculator that posts individual values (`int a`, `int b`, `string op`) directly to action method parameters

## IDE Support

For `.cvw` file recognition, syntax highlighting, and icon support in JetBrains Rider, install the **ConsoleMVC CVWSupport** plugin:

- [JetBrains Marketplace](https://plugins.jetbrains.com/plugin/31261-consolemvc-cvwsupport)
- [Plugin Source Code](https://github.com/MateuszPodeszwa/ConsoleMVC-CVWSupport)

## Learn More

- [ConsoleMVC.Framework NuGet Package](https://www.nuget.org/packages/ConsoleMVC.Framework)
- [GitHub Repository](https://github.com/MateuszPodeszwa/ConsoleMVC)
