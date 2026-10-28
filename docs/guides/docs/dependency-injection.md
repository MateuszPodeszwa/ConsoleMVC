# Dependency Injection

ConsoleMVC uses the standard .NET container, `Microsoft.Extensions.DependencyInjection`, with the same
`IServiceCollection` API as ASP.NET Core. Register services on the builder; controllers receive them through their
constructors and views through `@inject`.

## Registering services

```csharp
using ConsoleMVC.Mvc;
using Microsoft.Extensions.DependencyInjection;

var builder = MvcApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGreetingService, GreetingService>(); // one instance for the whole run
builder.Services.AddScoped<IBasket, Basket>();                      // one instance per screen
builder.Services.AddTransient<IClock, SystemClock>();               // a new instance every time

using var app = builder.Build();
app.Run();
```

`using var app` disposes the container when the application ends, which disposes every singleton it created. Use `await using var app` when a service implements only `IAsyncDisposable`.

## Controllers: constructor injection

```csharp
public class GreetController(IGreetingService greetings) : Controller
{
    public ActionResult Result(GreetFormModel model) =>
        View(new GreetResultModel { Greeting = greetings.Greet(model.Name, model.Color) });
}
```

A constructor parameter that cannot be resolved fails with an `InvalidOperationException` naming the missing type.

## Views: `@inject`

```
@model ConsoleMVC.App.Models.GreetResultModel
@inject ConsoleMVC.App.Services.IGreetingService Greetings

<text>@Model.Greeting</text>
<text dim>Greetings sent so far: @Greetings.Count</text>
```

`@inject Type Name` declares a property called `Name` that the framework fills before the view renders. It sits with
`@model` and `@using` at the top of the file, in any order. Generic types are fine:
`@inject IOptions<Dictionary<string, int>> Limits`.

The name must be a valid C# identifier and cannot be one the view already has: `Model` (the model parameter); the
view's own class name (such as `IndexView`); the members of the view base classes, `ViewData`, `ModelState`,
`Renderer`, `Input`, `Form`, `Theme`, `Services`, `ReadInput`, `ShowScroll`, `ShortcutHints`, `NavigationHistory`,
`Breadcrumbs`, `PreparePartial`, `InjectServices`, `RequireService`, `Render`, `RenderInternal`, `RunMenu`,
`RefreshInterval`, `UseLayout`, `RenderHeader` and `RenderFooter`; and the members every object has, `Equals`,
`GetHashCode`, `GetType`, `ToString`, `MemberwiseClone`, `Finalize` and `ReferenceEquals`. Names starting with `__cvw_`
are reserved for generated code. Mistakes are build errors in the `.cvw` file:

| Id | Meaning |
|---|---|
| CVW007 | `@inject` without a type and a property name, or an invalid or reserved name |
| CVW008 | Two `@inject` directives declare the same property name |

A misspelt type is reported by the compiler at the directive's line and column.

## Scopes and lifetimes

Each navigation cycle (one action and the view it renders) runs in its own scope:

```
action Index ─┐            scope 1: controller and view share scoped services
view   Index ─┘ → dispose
action Save ──→ redirect   scope 2
action Done ──┐            scope 3
view   Done  ─┘ → dispose
```

- **Scoped** services are shared by a controller and its view, and disposed when the screen ends.
- Controllers and views that implement `IDisposable` are disposed at the end of their cycle.
- A redirect ends the cycle; the next action gets a new scope.
- Scope validation is on: a singleton that depends on a scoped service, or a scoped service resolved from
  `app.Services`, fails immediately instead of silently living for the whole run.

## Framework services

The framework registers `IMarkupRenderer`, `ITextInput` and `ILiveDisplay` unless you do, and always provides
`IConfiguration`, `IHostEnvironment`, the options services (`IOptions<T>`), localisation (`IStringLocalizer<T>`; see
[Localisation](localisation.md)), `ITerminalSession` (the session the code runs in; see
[Remote Sessions](remote-sessions.md)) and logging (`ILogger<T>`; see [Configuration Files and Logging](hosting.md)). You can therefore inject them, or replace the renderer and input with
your own registration:

```csharp
builder.Services.AddSingleton<IMarkupRenderer>(_ => new MarkupRenderer(new PlainTextRenderTarget(Console.Out), () => 60));
```

`builder.Renderer` and `builder.Input`, when set, take precedence over registrations of the same type.

## Using services outside the loop

`app.Services` is the root provider. Use it to prepare data before the first screen:

```csharp
using var app = builder.Build();
app.Services.GetRequiredService<IProductCatalogue>().Load("products.json");
app.Run();
```

For scoped services, create a scope: `using var scope = app.Services.CreateScope();`.

## Testing

`Router.ResolveController(name, services)` and `Router.ResolveView(controller, action, services)` create
controllers and views exactly as the application does, so you can test them with a hand-built provider:

```csharp
var services = new ServiceCollection().AddSingleton<IGreetingService, FakeGreetings>().BuildServiceProvider();
var router = new Router();
router.DiscoverAll(typeof(Program).Assembly);

var controller = router.ResolveController("Greet", services);
```
