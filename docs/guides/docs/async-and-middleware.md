# Async Actions and Middleware

## Asynchronous actions

Actions can `await` I/O. Return `Task<ActionResult>`, `ValueTask<ActionResult>`, or a task of a more specific
result such as `Task<ViewResult>`:

```csharp
public class OrdersController(IOrderStore store) : Controller
{
    public async Task<ActionResult> Index(CancellationToken cancellationToken)
    {
        var orders = await store.ListAsync(cancellationToken);
        return View(orders);
    }
}
```

- A `CancellationToken` parameter receives the token passed to `app.RunAsync(token)` (or `CancellationToken.None`).
  It is never bound from form data.
- A method returning a plain `Task` is not an action: there would be nothing to render.
- Exceptions thrown by an async action surface unchanged, just like synchronous ones.
- Views stay synchronous: `Render` returns a `NavigationResult`.

To show progress while a long action works, draw a `<progress>` bar or a `<spinner>` with `ILiveDisplay`: see
[Live Screens](live-screens.md).

Start the application with `await app.RunAsync()`. `app.Run()` still works; it waits for `RunAsync`.

Ctrl+C ends the application cleanly, whatever the screen is doing: a screen waiting for the user is left at once, the
token passed to running actions is cancelled, services are disposed, `Stopped` is logged, the terminal is put back,
and `Run` and `RunAsync` return. A second Ctrl+C ends the process at once, for an action that ignores its token. To
stop the application from your own code, pass a token:

```csharp
using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));   // for example, a time limit

await app.RunAsync(cancellation.Token); // throws OperationCanceledException once your token is cancelled
```

## Middleware

Middleware runs around every action, in the order it was added (the first is the outermost), with the same shape as
ASP.NET Core:

```csharp
using var app = builder.Build();

app.Use(async (context, next) =>
{
    var started = Stopwatch.GetTimestamp();
    await next(context);                       // runs the next middleware, then the action
    Debug.WriteLine($"{context.Route} took {Stopwatch.GetElapsedTime(started)}");
});

await app.RunAsync();
```

Add middleware before the application runs; adding it later throws `InvalidOperationException`.

### The context

| Member | Meaning |
|--------|---------|
| `Route` | The controller, action and posted form data about to run. Settable, to rewrite the route before `next` |
| `Services` | The services of the cycle's scope |
| `Cancellation` | Cancelled when the token passed to `RunAsync` is, or when the user presses Ctrl+C |
| `Controller` | The controller, once the action has run |
| `ModelState` | Binding and validation errors, once the action has run |
| `Result` | The action's result. Replace it, or set it without calling `next` to skip the action |
| `RedirectTo(action, controller)` | Sets `Result` to a redirect |
| `Items` | Values shared between middleware during one cycle |

The framework renders the view (or follows the redirect) in `Result` **after** the pipeline, so middleware sees what
the action produced but not the view's own rendering.

### Short-circuiting

```csharp
app.Use((context, next) =>
{
    if (!session.IsSignedIn && context.Route.Controller != "Account")
    {
        context.RedirectTo("SignIn", "Account");   // the action never runs
        return Task.CompletedTask;
    }

    return next(context);
});
```

A middleware that neither calls `next` nor sets `Result` stops the application with a clear
`InvalidOperationException`.

### Handling errors

```csharp
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (HttpRequestException)
    {
        context.RedirectTo("Offline", "Errors");   // show an error screen instead of crashing
    }
});
```

### Class-based middleware

Implement `IActionMiddleware` and add it with `UseMiddleware<T>()`. A new instance is created for every navigation
cycle from the cycle's services, so the constructor can take scoped services:

```csharp
public sealed class RequireSignInMiddleware(ISession session) : IActionMiddleware
{
    public Task InvokeAsync(ActionContext context, ActionDelegate next)
    {
        if (!session.IsSignedIn && context.Route.Controller != "Account")
        {
            context.RedirectTo("SignIn", "Account");
            return Task.CompletedTask;
        }

        return next(context);
    }
}

app.UseMiddleware<RequireSignInMiddleware>();
```
