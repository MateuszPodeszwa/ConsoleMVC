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
public abstract class ConsoleView
{
    /// <summary>
    /// Shared console renderer used by views that are created outside the application loop
    /// (for example in unit tests or when rendering a view manually).
    /// </summary>
    private static readonly Lazy<IMarkupRenderer> DefaultRenderer = new(MarkupRenderer.CreateForConsole);

    private IMarkupRenderer? _renderer;

    /// <summary>
    /// Gets or sets the <see cref="ViewDataDictionary"/> used to pass supplementary
    /// key-value data from the controller to the view.
    /// </summary>
    /// <value>
    /// A dictionary of loosely-typed data items shared between the controller and view.
    /// </value>
    public ViewDataDictionary ViewData { get; internal set; } = new();

    /// <summary>
    /// Gets the renderer that draws C-View Markup elements written in this view. The framework
    /// assigns the renderer configured on <see cref="MvcApplicationBuilder.Renderer"/>; views
    /// created manually fall back to a shared console renderer.
    /// </summary>
    /// <example>
    /// <code>
    /// // Generated code for a top-level &lt;text&gt; element calls:
    /// Renderer.Render(textElement);
    /// </code>
    /// </example>
    public IMarkupRenderer Renderer
    {
        get => _renderer ??= DefaultRenderer.Value;
        internal set => _renderer = value;
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
        return model switch
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
    }
}
