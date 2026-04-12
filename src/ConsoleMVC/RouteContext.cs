namespace ConsoleMVC.Mvc;

/// <summary>
/// Represents the current routing state, identifying which controller and action
/// are being executed in the current navigation cycle.
/// </summary>
/// <remarks>
/// The framework creates a new <see cref="RouteContext"/> for each iteration of the main loop
/// and assigns it to the controller via <see cref="Controller.RouteContext"/> before invoking
/// the action method.
/// </remarks>
public class RouteContext
{
    /// <summary>
    /// Gets the name of the current controller (without the <c>Controller</c> suffix).
    /// </summary>
    /// <value>Defaults to <c>"Home"</c>.</value>
    public string Controller { get; init; } = "Home";

    /// <summary>
    /// Gets the name of the current action method being executed.
    /// </summary>
    /// <value>Defaults to <c>"Index"</c>.</value>
    public string Action { get; init; } = "Index";
}
