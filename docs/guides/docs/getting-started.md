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
├── Views/
│   └── Home/
│       └── IndexView.cvw
└── Program.cs
```

Controllers inherit from `Controller` and expose public parameterless methods that return `ActionResult`. Views are `.cvw` files that declare their model type with an `@model` directive and contain C# code that renders output to the console.

## Running Your Application

Once the project is set up, build and run it as you would any .NET console application:

```bash
dotnet run
```

The framework boots, resolves the default route (`Home/Index` unless you configure otherwise via `MvcApplicationBuilder`), and enters the MVC event loop. From there, views can return navigation results that direct the framework to other controller actions or signal that the application should exit.
