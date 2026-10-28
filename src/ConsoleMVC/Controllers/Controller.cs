namespace ConsoleMVC.Mvc;

/// <summary>
/// Base class for all controllers in a ConsoleMVC application. Controllers contain
/// action methods that handle application logic and return an <see cref="ActionResult"/>
/// to determine what the framework should do next.
/// </summary>
/// <remarks>
/// <para>
/// This mirrors the <c>Controller</c> base class found in ASP.NET Core MVC. Controllers
/// are discovered automatically at startup via reflection — any non-abstract class that
/// inherits from <see cref="Controller"/> and follows the <c>{Name}Controller</c> naming
/// convention is registered by the <see cref="Router"/>.
/// </para>
/// <para>
/// Action methods must be public, parameterless, and return <see cref="ActionResult"/>.
/// Use <see cref="View(object?)"/> to render a view or <see cref="RedirectToAction(string, string?)"/> to navigate
/// to a different action without rendering.
/// </para>
/// </remarks>
public abstract class Controller
{
    /// <summary>
    /// Gets or sets the current route context, which contains the controller and action
    /// names for the currently executing request. This is set by the framework prior to
    /// action invocation and should not be modified by application code.
    /// </summary>
    /// <value>The <see cref="Mvc.RouteContext"/> for the current navigation cycle.</value>
    public RouteContext RouteContext { get; internal set; } = new();

    /// <summary>
    /// Gets the <see cref="ViewDataDictionary"/> used to pass supplementary key-value
    /// data to views. Data placed here is available in the view via <see cref="ConsoleViewBase.ViewData"/>.
    /// </summary>
    /// <value>A dictionary of loosely-typed data items accessible from the rendered view.</value>
    public ViewDataDictionary ViewData { get; } = new();

    /// <summary>
    /// Gets the validation errors for the current request. When a view posts form data, the framework binds it
    /// and validates it with DataAnnotations attributes (<c>[Required]</c>, <c>[Range]</c>, …) before the action
    /// runs; check <see cref="ModelStateDictionary.IsValid"/> and return the form again when it is not.
    /// </summary>
    /// <value>The model state, passed on to the view returned by <see cref="View(object?)"/>.</value>
    public ModelStateDictionary ModelState { get; internal set; } = new();

    /// <summary>
    /// Creates a <see cref="ViewResult"/> that renders the view corresponding to the
    /// current action, optionally passing a model to the view.
    /// </summary>
    /// <param name="model">
    /// The view model to pass to the view, or <see langword="null"/> if the view does not require a model.
    /// </param>
    /// <returns>A <see cref="ViewResult"/> that instructs the framework to render the matching view.</returns>
    /// <remarks>
    /// The view is resolved by convention: the framework looks for a class named
    /// <c>{Action}View</c> in the <c>*.Views.{Controller}</c> namespace. A <see cref="string"/> argument selects
    /// <see cref="View(string)"/> instead; cast it to <see cref="object"/> to pass a string model.
    /// </remarks>
    protected ViewResult View(object? model = null)
    {
        return View(RouteContext.Action, model);
    }

    /// <summary>
    /// Creates a <see cref="ViewResult"/> that renders the view of another action on this controller without a model,
    /// as in ASP.NET Core MVC.
    /// </summary>
    /// <param name="viewName">The action whose view to render, e.g. <c>"Index"</c> for <c>IndexView</c>.</param>
    /// <returns>A <see cref="ViewResult"/> for the named view.</returns>
    /// <remarks>To pass a <see cref="string"/> as the model of the current view, cast it: <c>View((object)text)</c>.</remarks>
    /// <example><code>return View("Index");</code></example>
    protected ViewResult View(string viewName)
    {
        return View(viewName, null);
    }

    /// <summary>
    /// Creates a <see cref="ViewResult"/> that renders the view of another action on this controller — typically
    /// to show a form again after validation fails.
    /// </summary>
    /// <param name="viewName">The action whose view to render, e.g. <c>"Index"</c> for <c>IndexView</c>.</param>
    /// <param name="model">The view model, or <see langword="null"/>.</param>
    /// <returns>A <see cref="ViewResult"/> for the named view.</returns>
    /// <example>
    /// <code>
    /// if (!ModelState.IsValid)
    ///     return View("Index", model);
    /// </code>
    /// </example>
    protected ViewResult View(string viewName, object? model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewName);

        return new ViewResult
        {
            ControllerName = GetControllerName(),
            ViewName = viewName,
            Model = model,
            ViewData = ViewData,
            ModelState = ModelState
        };
    }

    /// <summary>
    /// Validates <paramref name="model"/> with its DataAnnotations attributes and adds any errors to
    /// <see cref="ModelState"/>. Useful for models built in code rather than posted from a view.
    /// </summary>
    /// <param name="model">The object to validate.</param>
    /// <returns><see langword="true"/> when the model is valid. Calling it again does not add the same errors twice.</returns>
    /// <example><code>if (!TryValidateModel(order)) return View("Edit", order);</code></example>
    protected bool TryValidateModel(object model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return ModelValidator.ValidateModel(model, ModelState, ValidationLocalizer);
    }

    /// <summary>
    /// Creates a <see cref="RedirectToActionResult"/> that navigates to a different
    /// action without rendering any view in the current cycle.
    /// </summary>
    /// <param name="action">The name of the target action method.</param>
    /// <param name="controller">
    /// The name of the target controller (without the <c>Controller</c> suffix).
    /// If <see langword="null"/>, the current controller is used.
    /// </param>
    /// <returns>
    /// A <see cref="RedirectToActionResult"/> that instructs the framework to navigate
    /// to the specified controller and action.
    /// </returns>
    protected RedirectToActionResult RedirectToAction(string action, string? controller = null)
    {
        return new RedirectToActionResult
        {
            ActionName = action,
            ControllerName = controller ?? GetControllerName()
        };
    }

    /// <summary>
    /// Creates a <see cref="RedirectToActionResult"/> to an action on this controller, passing route values from an object
    /// such as <c>new { id = 42 }</c> or a dictionary.
    /// </summary>
    /// <param name="action">The name of the target action method.</param>
    /// <param name="routeValues">The values bound to the action's parameters; see <see cref="Mvc.RouteValues.From"/>.</param>
    /// <returns>The redirect.</returns>
    /// <example><code>return RedirectToAction("Details", new { id = order.Id });</code></example>
    protected RedirectToActionResult RedirectToAction(string action, object routeValues) =>
        RedirectToAction(action, null, routeValues);

    /// <summary>
    /// Creates a <see cref="RedirectToActionResult"/> to an action on another controller, passing route values.
    /// </summary>
    /// <param name="action">The name of the target action method.</param>
    /// <param name="controller">The target controller, or <see langword="null"/> for this one.</param>
    /// <param name="routeValues">The values bound to the action's parameters; see <see cref="Mvc.RouteValues.From"/>.</param>
    /// <returns>The redirect.</returns>
    /// <example><code>return RedirectToAction("Details", "Orders", new { id });</code></example>
    protected RedirectToActionResult RedirectToAction(string action, string? controller, object routeValues)
    {
        return new RedirectToActionResult
        {
            ActionName = action,
            ControllerName = controller ?? GetControllerName(),
            RouteValues = Mvc.RouteValues.From(routeValues)
        };
    }

    /// <summary>
    /// Derives the logical controller name by stripping the <c>Controller</c> suffix
    /// from the concrete type name.
    /// </summary>
    /// <returns>The controller name without the <c>Controller</c> suffix.</returns>
    private string GetControllerName()
    {
        var name = GetType().Name;
        return name.EndsWith("Controller")
            ? name[..^"Controller".Length]
            : name;
    }

    /// <summary>
    /// Gets or sets the resources that translate validation messages, when
    /// <see cref="MvcLocalizationOptions.LocalizeValidation"/> is on; assigned by the application before the action runs.
    /// </summary>
    internal Func<Type, Microsoft.Extensions.Localization.IStringLocalizer>? ValidationLocalizer { get; set; }
}
