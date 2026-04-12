namespace ConsoleMVC.Mvc;

/// <summary>
/// An <see cref="ActionResult"/> that instructs the framework to render the console view
/// matching the current controller and action, passing the specified model and view data.
/// </summary>
/// <remarks>
/// This is analogous to <c>ViewResult</c> in ASP.NET Core MVC.
/// Instances are created by calling <see cref="Controller.View"/> from within
/// a controller action method.
/// </remarks>
public class ViewResult : ActionResult
{
    /// <summary>
    /// Gets the name of the controller whose view should be rendered
    /// (without the <c>Controller</c> suffix).
    /// </summary>
    public string ControllerName { get; init; } = "";

    /// <summary>
    /// Gets the name of the view to render, which corresponds to the action method name.
    /// The framework resolves this to a class named <c>{ViewName}View</c> in the
    /// <c>*.Views.{ControllerName}</c> namespace.
    /// </summary>
    public string ViewName { get; init; } = "";

    /// <summary>
    /// Gets the view model to pass to the view, or <see langword="null"/> if no model is provided.
    /// </summary>
    public object? Model { get; init; }

    /// <summary>
    /// Gets the <see cref="ViewDataDictionary"/> containing supplementary data
    /// to make available to the view.
    /// </summary>
    public ViewDataDictionary ViewData { get; init; } = new();
}
