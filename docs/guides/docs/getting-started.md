# Getting Started

## Installation

The quickest way to begin is with the project template. Install it from NuGet and scaffold a new application:

```bash
dotnet new install ConsoleMVC.Template
dotnet new consolemvc -n MyApp
cd MyApp
dotnet run
```

This creates a working application with a home menu, a greeting form (text input, a `<select>` and a `<checkbox>`), a
calculator, a table booking form (a date, a filtered list, several choices and a masked PIN), a feature tour with
tables and columns, a shared layout and partial view, a service registered for dependency injection, keyboard
shortcuts (F1, Esc, Ctrl+X), and settings in `appsettings.json`.

Add `--tests` to get a test project as well, `MyApp/MyApp.Tests`, which runs the application with scripted input and
checks a snapshot of the home menu (see [Testing](testing.md)):

```bash
dotnet new consolemvc -n MyApp --tests
dotnet test MyApp/MyApp.Tests
```

If you prefer to add ConsoleMVC to an existing project, install the framework package directly:

```bash
dotnet add package ConsoleMVC.Framework
```

## Project Structure

A ConsoleMVC application follows a convention-based layout. Controllers live in `Controllers/` (or wherever you prefer) and are found automatically: the build generates a route table of every controller and view. Views are placed under a `Views/` folder, organised by controller name, and use the `.cvw` file extension:

```
MyApp/
├── Controllers/
│   └── HomeController.cs
├── Models/
│   └── HomeViewModel.cs
├── Services/                 (optional: registered with builder.Services)
│   ├── GreetingOptions.cs    (bound from appsettings.json)
│   └── GreetingService.cs
├── Views/
│   ├── Home/
│   │   └── IndexView.cvw     (the view for HomeController.Index)
│   └── Shared/
│       ├── _Layout.cvw       (frames every view)
│       └── PageHeader.cvw    (a partial view, used with <partial>)
├── appsettings.json          (read by builder.Configuration)
└── Program.cs
```

Controllers inherit from `Controller` and expose public methods that return `ActionResult`; parameters are bound from form data posted by views. Views are `.cvw` files that declare their model type with an `@model` directive and contain C# code, optionally mixed with [C-View Markup](c-view-markup.md) elements such as `<box>` and `<text>`, that renders output to the console. Services registered in `Program.cs` reach controllers through their constructors and views through `@inject`: see [Dependency Injection](dependency-injection.md). Settings come from `appsettings.json`, environment variables and the command line: see [Configuration Files and Logging](hosting.md).

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

## Next steps

- Describe screens with [C-View Markup](c-view-markup.md): boxes, tables, columns, menus and form fields.
- Post forms and bind them to models in [Controllers and Forms](controllers-and-forms.md), and check them with [Validation](validation.md).
- Test the application end to end with the [Testing](testing.md) package.
