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

    /// <summary>
    /// Gets the form data submitted by the previous view, or <see langword="null"/>
    /// if no form data was posted. The framework uses this to bind values to the
    /// action method parameters. After binding, the data is also accessible here
    /// for controllers that need raw access.
    /// </summary>
    public Dictionary<string, string>? FormData { get; init; }

    /// <summary>
    /// Gets the route values passed with the navigation (<c>Orders/Details?id=42</c>), or <see langword="null"/>. They are
    /// bound to the action's parameters like form data, but do not trigger validation.
    /// </summary>
    public IReadOnlyDictionary<string, string>? RouteValues { get; init; }

    /// <summary>
    /// Gets the area of the controller (<c>Admin</c> for <c>Admin/Users</c>), or <see langword="null"/> outside an area.
    /// </summary>
    /// <example><code>if (route.Area == "Admin") { /* … */ }</code></example>
    public string? Area => Controller.LastIndexOf('/') is var slash and > 0 ? Controller[..slash] : null;

    /// <summary>Gets the controller's name without its area: <c>Users</c> for <c>Admin/Users</c>.</summary>
    /// <example><code>var label = route.ControllerName;</code></example>
    public string ControllerName => Controller[(Controller.LastIndexOf('/') + 1)..];

    /// <summary>Returns the route as <c>Controller/Action</c>.</summary>
    /// <returns>For example <c>"Home/Index"</c>.</returns>
    /// <example><code>logger.LogInformation("Showing {Route}", context.Route);</code></example>
    public override string ToString() => $"{Controller}/{Action}";
}
