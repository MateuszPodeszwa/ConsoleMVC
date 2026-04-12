namespace ConsoleMVC.Mvc;

/// <summary>
/// Base class for all controllers in a ConsoleMVC application. Controllers contain
/// action methods that handle application logic and return an <see cref="ActionResult"/>
/// to determine what the framework should do next.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the <c>Controller</c> base class found in ASP.NET Core MVC. Controllers
/// are discovered automatically at startup via reflection — any non-abstract class that
/// inherits from <see cref="Controller"/> and follows the <c>{Name}Controller</c> naming
/// convention is registered by the <see cref="Router"/>.
/// </para>
/// <para>
/// Action methods must be public, parameterless, and return <see cref="ActionResult"/>.
/// Use <see cref="View"/> to render a view or <see cref="RedirectToAction"/> to navigate
/// to a different action without rendering.
/// </para>
/// </remarks>
public abstract class Controller
{
    /// <summary>
    /// Gets or sets the current route context, which contains the controller and action
    /// names for the currently executing request. This is set by the framework prior to
    /// action invocation and should not be modified by application code.
    /// </summary>
    /// <value>The <see cref="Mvc.RouteContext"/> for the current navigation cycle.</value>
    public RouteContext RouteContext { get; internal set; } = new();

    /// <summary>
    /// Gets the <see cref="ViewDataDictionary"/> used to pass supplementary key-value
    /// data to views. Data placed here is available in the view via <see cref="ConsoleView.ViewData"/>.
    /// </summary>
    /// <value>A dictionary of loosely-typed data items accessible from the rendered view.</value>
    public ViewDataDictionary ViewData { get; } = new();

    /// <summary>
    /// Creates a <see cref="ViewResult"/> that renders the view corresponding to the
    /// current action, optionally passing a model to the view.
    /// </summary>
    /// <param name="model">
    /// The view model to pass to the view, or <see langword="null"/> if the view does not require a model.
    /// </param>
    /// <returns>A <see cref="ViewResult"/> that instructs the framework to render the matching view.</returns>
    /// <remarks>
    /// The view is resolved by convention: the framework looks for a class named
    /// <c>{Action}View</c> in the <c>*.Views.{Controller}</c> namespace.
    /// </remarks>
    protected ViewResult View(object? model = null)
    {
        return new ViewResult
        {
            ControllerName = GetControllerName(),
            ViewName = RouteContext.Action,
            Model = model,
            ViewData = ViewData
        };
    }

    /// <summary>
    /// Creates a <see cref="RedirectToActionResult"/> that navigates to a different
    /// action without rendering any view in the current cycle.
    /// </summary>
    /// <param name="action">The name of the target action method.</param>
    /// <param name="controller">
    /// The name of the target controller (without the <c>Controller</c> suffix).
    /// If <see langword="null"/>, the current controller is used.
    /// </param>
    /// <returns>
    /// A <see cref="RedirectToActionResult"/> that instructs the framework to navigate
    /// to the specified controller and action.
    /// </returns>
    protected RedirectToActionResult RedirectToAction(string action, string? controller = null)
    {
        return new RedirectToActionResult
        {
            ActionName = action,
            ControllerName = controller ?? GetControllerName()
        };
    }

    /// <summary>
    /// Derives the logical controller name by stripping the <c>Controller</c> suffix
    /// from the concrete type name.
    /// </summary>
    /// <returns>The controller name without the <c>Controller</c> suffix.</returns>
    private string GetControllerName()
    {
        var name = GetType().Name;
        return name.EndsWith("Controller")
            ? name[..^"Controller".Length]
            : name;
    }
}
