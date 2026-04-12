namespace ConsoleMVC.Mvc;

/// <summary>
/// Represents the current route state — which controller and action are being executed.
/// </summary>
public class RouteContext
{
    public string Controller { get; init; } = "Home";
    public string Action { get; init; } = "Index";
}
