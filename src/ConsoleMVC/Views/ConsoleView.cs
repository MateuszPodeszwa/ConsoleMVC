using ConsoleMVC.Rendering;

namespace ConsoleMVC.Mvc;

/// <summary>
/// Non-generic base class for all console views. Provides a type-agnostic contract
/// used internally by the framework for view resolution and rendering.
/// </summary>
/// <remarks>
/// Application developers should not inherit from this class directly.
/// Instead, use the generic <see cref="ConsoleView{TModel}"/> base class, which provides
/// a strongly-typed <c>Model</c> parameter in the <see cref="ConsoleView{TModel}.Render"/> method.
/// Views authored as <c>.cvw</c> files are automatically compiled into
/// <see cref="ConsoleView{TModel}"/> subclasses by the source generator at build time.
/// </remarks>
public abstract class ConsoleView : ConsoleViewBase
{
    /// <summary>
    /// Shows a <c>&lt;menu&gt;</c>, waits for a valid choice and converts it into a <see cref="NavigationResult"/>.
    /// Generated code returns the result of this method for a top-level <c>&lt;menu&gt;</c>.
    /// </summary>
    /// <param name="menu">The menu to show.</param>
    /// <returns>
    /// <see cref="NavigationResult.Quit"/> for a <c>quit</c> option or when the input ends; otherwise navigation to
    /// the option's <c>nav</c> target, carrying a copy of <see cref="ConsoleViewBase.Form"/> when the option is marked <c>post</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when an option's target is not <c>Controller/Action</c> or <c>Action</c>.</exception>
    protected internal NavigationResult RunMenu(MenuElement menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        BeforeInteraction?.Invoke();
        var option = menu.Choose(Renderer, Input);
        if (option is null || option.Quit)
            return NavigationResult.Quit();

        if (option.Back)
            return NavigationResult.Back();

        var target = option.Target ?? "";
        string? controller;
        string action;
        Dictionary<string, string>? routeValues;
        try
        {
            (controller, action, routeValues) = RouteValues.ParseTarget(target);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"Menu option '{option.Key}' in view '{GetType().Name}' has an invalid target '{target}'. " +
                "Use 'Controller/Action' or 'Action', optionally followed by '?key=value'.");
        }

        var formData = option.PostForm ? new Dictionary<string, string>(Form, StringComparer.OrdinalIgnoreCase) : null;

        return new NavigationResult { Controller = controller, Action = action, FormData = formData, RouteValues = routeValues };
    }

    /// <summary>
    /// Renders the view using the supplied model object and returns a
    /// <see cref="NavigationResult"/> indicating the next route.
    /// </summary>
    /// <param name="model">The view model instance, or <see langword="null"/> if no model is provided.</param>
    /// <returns>A <see cref="NavigationResult"/> describing where the application should navigate next.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="model"/> is not compatible with the view's model type.
    /// </exception>
    internal abstract NavigationResult RenderInternal(object? model);

    /// <summary>
    /// Gets how often the screen is drawn again while the user has not started answering, or <see langword="null"/> (the
    /// default) to wait for the user. The <c>@refresh 5s</c> directive overrides this; an action's
    /// <see cref="ViewResult.RefreshEvery"/> or <see cref="ViewResult.NoRefresh"/> wins over both.
    /// </summary>
    /// <example>
    /// <code>
    /// // @refresh 5s generates:
    /// protected override TimeSpan? RefreshInterval => TimeSpan.FromMilliseconds(5000);
    /// </code>
    /// </example>
    protected virtual TimeSpan? RefreshInterval => null;

    /// <summary>Gets <see cref="RefreshInterval"/> for the application loop.</summary>
    internal TimeSpan? DeclaredRefreshInterval => RefreshInterval;

    /// <summary>The layout whose footer is still to be drawn, or <see langword="null"/>.</summary>
    private ConsoleLayoutView? _pendingLayout;

    /// <summary>
    /// Draws <paramref name="layout"/>'s header now and its footer before the view first waits for the user (or when it
    /// returns). Generated code calls this at the start of <c>Render</c> for a view with a layout.
    /// </summary>
    /// <param name="layout">The layout, e.g. <c>new MyApp.Views.Shared._Layout()</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layout"/> is <see langword="null"/>.</exception>
    /// <example><code>this.UseLayout(new global::MyApp.Views.Shared._Layout());</code></example>
    protected internal void UseLayout(ConsoleLayoutView layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        _pendingLayout = layout;
        BeforeInteraction = CompleteLayout;
        ShareStateWith(layout);
        layout.BeforeInteraction = null; // the layout's own footer must not trigger itself
        layout.RenderHeader();
    }

    /// <summary>Draws the pending layout footer, once.</summary>
    private protected void CompleteLayout()
    {
        var layout = _pendingLayout;
        if (layout is null)
            return;

        _pendingLayout = null;
        BeforeInteraction = null;
        layout.RenderFooter();
    }
}

/// <summary>
/// Generic base class for console views that accept a strongly-typed view model.
/// All <c>.cvw</c> view files are compiled into subclasses of this type by the source generator.
/// </summary>
/// <typeparam name="TModel">The type of view model this view expects to receive from its controller.</typeparam>
/// <remarks>
/// Each view must implement <see cref="Render"/> to write output to the console and return
/// a <see cref="NavigationResult"/> that tells the framework what to do next (navigate to
/// another page or quit the application).
/// </remarks>
public abstract class ConsoleView<TModel> : ConsoleView
{
    /// <summary>
    /// Renders the view to the console using the specified model and returns a
    /// <see cref="NavigationResult"/> indicating the next navigation target.
    /// </summary>
    /// <param name="Model">The strongly-typed view model passed from the controller action.</param>
    /// <returns>A <see cref="NavigationResult"/> describing where the application should navigate next.</returns>
    public abstract NavigationResult Render(TModel Model);

    /// <inheritdoc />
    internal override NavigationResult RenderInternal(object? model)
    {
        var navigation = model switch
        {
            TModel typed => Render(typed),
            null when default(TModel) is null => Render(default!),
            null => throw new InvalidOperationException(
                $"View '{GetType().Name}' requires a model of type '{typeof(TModel).Name}', but the controller " +
                "passed no model. Call View(model) with a value, or change the view's @model directive."),
            _ => throw new InvalidOperationException(
                $"View '{GetType().Name}' expects a model of type '{typeof(TModel).FullName}', but the controller " +
                $"passed '{model.GetType().FullName}'. Pass the correct model to View(), or change the view's @model directive.")
        };

        // A view that never waited for the user still gets its layout's footer
        CompleteLayout();
        return navigation;
    }
}
