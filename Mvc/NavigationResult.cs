namespace ConsoleMVC.Mvc;

/// <summary>
/// Returned by views after rendering to signal where to navigate next.
/// </summary>
public class NavigationResult
{
    public string? Controller { get; init; }
    public string? Action { get; init; }
    public bool Exit { get; init; }

    /// <summary>
    /// Navigate to a specific controller and action.
    /// </summary>
    public static NavigationResult To(string controller, string action)
        => new() { Controller = controller, Action = action };

    /// <summary>
    /// Navigate to a different action on the current controller.
    /// </summary>
    public static NavigationResult ToAction(string action)
        => new() { Action = action };

    /// <summary>
    /// Signal the application to exit.
    /// </summary>
    public static NavigationResult Quit()
        => new() { Exit = true };
}
