namespace ConsoleMVC.Mvc;

/// <summary>
/// Base class for all controllers. Provides View() and RedirectToAction() methods
/// mirroring ASP.NET Core MVC's Controller.
/// </summary>
public abstract class Controller
{
    /// <summary>
    /// The current route context, set by the framework before action invocation.
    /// </summary>
    public RouteContext RouteContext { get; internal set; } = new();

    /// <summary>
    /// Loose key-value data bag for passing extra data to views.
    /// </summary>
    public ViewDataDictionary ViewData { get; } = new();

    /// <summary>
    /// Returns a ViewResult that renders the view matching the current action with the given model.
    /// </summary>
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
    /// Returns a RedirectToActionResult that navigates to a different action.
    /// </summary>
    protected RedirectToActionResult RedirectToAction(string action, string? controller = null)
    {
        return new RedirectToActionResult
        {
            ActionName = action,
            ControllerName = controller ?? GetControllerName()
        };
    }

    private string GetControllerName()
    {
        var name = GetType().Name;
        return name.EndsWith("Controller")
            ? name[..^"Controller".Length]
            : name;
    }
}
