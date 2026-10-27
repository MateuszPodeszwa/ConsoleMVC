namespace ConsoleMVC.Mvc;

/// <summary>
/// Everything about one navigation cycle that middleware can read or change: the route, the services of the
/// cycle's scope and, once the action has run, its controller, model state and result.
/// </summary>
/// <example>
/// <code>
/// app.Use(async (context, next) =>
/// {
///     await next(context);
///     if (context.Result is ViewResult view)
///         log.Add($"{context.Route} rendered {view.ViewName}");
/// });
/// </code>
/// </example>
public sealed class ActionContext
{
    /// <summary>
    /// Initialises a new context for one navigation cycle.
    /// </summary>
    /// <param name="route">The route about to run.</param>
    /// <param name="services">The services of the cycle's scope.</param>
    /// <param name="cancellation">The token passed to <see cref="MvcApplication.RunAsync"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> or <paramref name="services"/> is <see langword="null"/>.</exception>
    public ActionContext(RouteContext route, IServiceProvider services, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(services);

        Route = route;
        Services = services;
        Cancellation = cancellation;
    }

    /// <summary>
    /// Gets or sets the route to run. Middleware may replace it before calling <c>next</c>, for example to rewrite
    /// a deprecated action name or attach form data.
    /// </summary>
    /// <example><code>context.Route = new RouteContext { Controller = "Home", Action = "Index" };</code></example>
    public RouteContext Route { get; set; }

    /// <summary>Gets the services of this cycle's scope (scoped services are shared with the controller and view).</summary>
    /// <example><code>var clock = context.Services.GetRequiredService&lt;IClock&gt;();</code></example>
    public IServiceProvider Services { get; }

    /// <summary>Gets the token passed to <see cref="MvcApplication.RunAsync"/>.</summary>
    public CancellationToken Cancellation { get; }

    /// <summary>
    /// Gets the controller that ran the action, or <see langword="null"/> before the action runs or when a
    /// middleware short-circuited it.
    /// </summary>
    public Controller? Controller { get; internal set; }

    /// <summary>Gets the model state of the action (binding and validation errors). Empty until the action runs.</summary>
    public ModelStateDictionary ModelState { get; internal set; } = new();

    /// <summary>
    /// Gets or sets the result that the framework processes after the pipeline: a view to render or a redirect to
    /// follow. The action sets it; middleware may replace it, or set it without calling <c>next</c> to skip the action.
    /// </summary>
    /// <example><code>context.Result = new RedirectToActionResult { ControllerName = "Home", ActionName = "Index" };</code></example>
    public ActionResult? Result { get; set; }

    /// <summary>Gets a bag for passing values between middleware during this cycle.</summary>
    /// <example><code>context.Items["started"] = Stopwatch.GetTimestamp();</code></example>
    public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    /// <summary>
    /// Sets <see cref="Result"/> to a redirect, as <c>Controller.RedirectToAction</c> does.
    /// </summary>
    /// <param name="action">The target action.</param>
    /// <param name="controller">The target controller; <see langword="null"/> keeps the current one.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="action"/> is empty.</exception>
    /// <example><code>context.RedirectTo("SignIn", "Account");</code></example>
    public void RedirectTo(string action, string? controller = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        Result = new RedirectToActionResult
        {
            ActionName = action,
            ControllerName = controller ?? Route.Controller
        };
    }
}
