namespace ConsoleMVC.Mvc;

/// <summary>
/// An action result that redirects to another controller action without rendering a view.
/// </summary>
public class RedirectToActionResult : ActionResult
{
    public string ControllerName { get; init; } = "";
    public string ActionName { get; init; } = "";
}
