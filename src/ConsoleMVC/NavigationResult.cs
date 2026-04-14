namespace ConsoleMVC.Mvc;

/// <summary>
/// Represents the outcome of a view render, instructing the framework where to navigate next.
/// Every view must return a <see cref="NavigationResult"/> from its
/// <see cref="ConsoleView{TModel}.Render"/> method.
/// </summary>
/// <remarks>
/// Use the static factory methods <see cref="To(string, string)"/>, <see cref="ToAction(string)"/>, and
/// <see cref="Quit"/> rather than constructing instances directly.
/// </remarks>
public class NavigationResult
{
    /// <summary>
    /// Gets the name of the target controller to navigate to, or <see langword="null"/>
    /// to remain on the current controller.
    /// </summary>
    public string? Controller { get; init; }

    /// <summary>
    /// Gets the name of the target action to invoke, or <see langword="null"/>
    /// to remain on the current action.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Gets a value indicating whether the application should terminate.
    /// When <see langword="true"/>, the main loop exits and the application shuts down.
    /// </summary>
    public bool Exit { get; init; }

    /// <summary>
    /// Gets the form data collected by the view to be passed to the target controller action.
    /// The framework uses this dictionary to bind values to action method parameters.
    /// When <see langword="null"/>, no form data is submitted and the action is invoked
    /// without parameter binding (equivalent to a GET request in web MVC).
    /// </summary>
    public Dictionary<string, string>? FormData { get; init; }

    /// <summary>
    /// Creates a <see cref="NavigationResult"/> that navigates to the specified
    /// controller and action.
    /// </summary>
    /// <param name="controller">The target controller name (without the <c>Controller</c> suffix).</param>
    /// <param name="action">The target action method name.</param>
    /// <returns>A <see cref="NavigationResult"/> pointing to the specified route.</returns>
    public static NavigationResult To(string controller, string action)
        => new() { Controller = controller, Action = action };

    /// <summary>
    /// Creates a <see cref="NavigationResult"/> that navigates to the specified
    /// controller and action, posting the collected form data to the target action.
    /// </summary>
    /// <param name="controller">The target controller name (without the <c>Controller</c> suffix).</param>
    /// <param name="action">The target action method name.</param>
    /// <param name="formData">The form data to pass to the target action for parameter binding.</param>
    /// <returns>A <see cref="NavigationResult"/> pointing to the specified route with form data.</returns>
    public static NavigationResult To(string controller, string action, Dictionary<string, string> formData)
        => new() { Controller = controller, Action = action, FormData = formData };

    /// <summary>
    /// Creates a <see cref="NavigationResult"/> that navigates to a different action
    /// on the current controller.
    /// </summary>
    /// <param name="action">The target action method name.</param>
    /// <returns>A <see cref="NavigationResult"/> pointing to the specified action on the current controller.</returns>
    public static NavigationResult ToAction(string action)
        => new() { Action = action };

    /// <summary>
    /// Creates a <see cref="NavigationResult"/> that navigates to a different action
    /// on the current controller, posting the collected form data to the target action.
    /// </summary>
    /// <param name="action">The target action method name.</param>
    /// <param name="formData">The form data to pass to the target action for parameter binding.</param>
    /// <returns>A <see cref="NavigationResult"/> pointing to the specified action with form data.</returns>
    public static NavigationResult ToAction(string action, Dictionary<string, string> formData)
        => new() { Action = action, FormData = formData };

    /// <summary>
    /// Creates a <see cref="NavigationResult"/> that signals the application to exit gracefully.
    /// </summary>
    /// <returns>A <see cref="NavigationResult"/> with <see cref="Exit"/> set to <see langword="true"/>.</returns>
    public static NavigationResult Quit()
        => new() { Exit = true };
}
