namespace ConsoleMVC.Mvc;

/// <summary>
/// An action result that renders a console view with the specified model.
/// </summary>
public class ViewResult : ActionResult
{
    public string ControllerName { get; init; } = "";
    public string ViewName { get; init; } = "";
    public object? Model { get; init; }
    public ViewDataDictionary ViewData { get; init; } = new();
}
