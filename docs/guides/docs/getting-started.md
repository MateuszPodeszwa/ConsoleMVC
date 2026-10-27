# Getting Started

## Installation

The quickest way to begin is with the project template. Install it from NuGet and scaffold a new application:

```bash
dotnet new install ConsoleMVC.Template
dotnet new consolemvc -n MyApp
cd MyApp
dotnet run
```

This creates a working console MVC application with a sample controller, view, and model already wired up.

If you prefer to add ConsoleMVC to an existing project, install the framework package directly:

```bash
dotnet add package ConsoleMVC.Framework
```

## Project Structure

A ConsoleMVC application follows a convention-based layout. Controllers live at the project root (or wherever you prefer) and are discovered automatically by reflection. Views are placed under a `Views/` folder, organised by controller name, and use the `.cvw` file extension:

```
MyApp/
├── Controllers/
│   └── HomeController.cs
├── Models/
│   └── HomeViewModel.cs
├── Services/                 (optional: registered with builder.Services)
│   └── GreetingService.cs
├── Views/
│   └── Home/
│       └── IndexView.cvw
└── Program.cs
```

Controllers inherit from `Controller` and expose public methods that return `ActionResult`; parameters are bound from form data posted by views. Views are `.cvw` files that declare their model type with an `@model` directive and contain C# code, optionally mixed with [C-View Markup](c-view-markup.md) elements such as `<box>` and `<text>`, that renders output to the console. Services registered in `Program.cs` reach controllers through their constructors and views through `@inject` — see [Dependency Injection](dependency-injection.md).

## Running Your Application

Once the project is set up, build and run it as you would any .NET console application:

```bash
dotnet run
```

The framework boots, resolves the default route (`Home/Index` unless you configure otherwise via `MvcApplicationBuilder`), and enters the MVC event loop. From there, views can return navigation results that direct the framework to other controller actions or signal that the application should exit.

## Live reload

Run the application with `dotnet watch` while you work:

```bash
dotnet watch
```

Saving a `.cvw` view, a controller or a model applies the change to the running application (.NET hot reload). The change shows from the next screen: navigate away and back, or answer the current prompt. After each update the framework rediscovers controllers and views, so a page you have just added is routable without restarting. Edits that hot reload cannot apply (for example changing a view's `@model` type) make `dotnet watch` offer a restart; press Ctrl+R to restart at any time.

!!! note
    A view that is waiting for input keeps its old version until the user answers, because the console cannot be redrawn in the middle of a prompt.
