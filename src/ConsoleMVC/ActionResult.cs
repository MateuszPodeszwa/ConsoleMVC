namespace ConsoleMVC.Mvc;

/// <summary>
/// Represents the abstract base class for all action results returned by controller action methods.
/// Concrete implementations determine how the framework processes the result of a controller action,
/// such as rendering a view or redirecting to another action.
/// </summary>
/// <remarks>
/// This mirrors the <c>IActionResult</c> pattern found in ASP.NET Core MVC.
/// Framework consumers should not inherit from this class directly; instead, use the
/// helper methods on <see cref="Controller"/> such as <see cref="Controller.View"/> and
/// <see cref="Controller.RedirectToAction"/>.
/// </remarks>
public abstract class ActionResult
{
}
