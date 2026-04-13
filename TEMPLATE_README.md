# ConsoleMVC Application Template

A `dotnet new` project template for **ConsoleMVC** — an MVC framework for console applications inspired by ASP.NET Core MVC.

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

- **Program.cs** — entry point using the familiar `CreateBuilder().Build().Run()` pattern
- **Controllers/** — controller classes that handle actions and return results
- **Views/** — `.cvw` view templates with `@model` directive (compiled at build time)
- **Models/** — ViewModel DTOs passed from controllers to views

## Learn More

- [ConsoleMVC.Framework NuGet Package](https://www.nuget.org/packages/ConsoleMVC.Framework)
- [GitHub Repository](https://github.com/MateuszPodeszwa/ConsoleMVC)
