namespace ConsoleMVC.Mvc;

/// <summary>
/// An <see cref="ActionResult"/> that instructs the framework to navigate to a different
/// controller action without rendering a view in the current cycle.
/// </summary>
/// <remarks>
/// This is analogous to <c>RedirectToActionResult</c> in ASP.NET Core MVC.
/// Instances are created by calling <see cref="Controller.RedirectToAction"/> from within
/// a controller action method.
/// </remarks>
public class RedirectToActionResult : ActionResult
{
    /// <summary>
    /// Gets the name of the target controller (without the <c>Controller</c> suffix).
    /// </summary>
    public string ControllerName { get; init; } = "";

    /// <summary>
    /// Gets the name of the target action method to invoke on the target controller.
    /// </summary>
    public string ActionName { get; init; } = "";
}
