using ConsoleMVC.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleMVC.Mvc;

/// <summary>
/// The members shared by full views (<see cref="ConsoleView"/>) and partial views (<see cref="ConsolePartialView"/>):
/// view data, model state, the renderer, user input and the form being filled in.
/// </summary>
/// <remarks>
/// Generated code relies on these members (for example <c>Renderer.Render(...)</c> for a top-level element), so markup
/// works the same way in views and partials. Do not inherit from this class directly.
/// </remarks>
public abstract class ConsoleViewBase
{
    /// <summary>Services used when a view created outside the application renders a partial that uses <c>@inject</c>.</summary>
    private static readonly Lazy<IServiceProvider> NoServices = new(() => new ServiceCollection().BuildServiceProvider());

    /// <summary>
    /// Shared console renderer used by views that are created outside the application loop
    /// (for example in unit tests or when rendering a view manually).
    /// </summary>
    private static readonly Lazy<IMarkupRenderer> DefaultRenderer = new(() => MarkupRenderer.CreateForConsole(redrawOnResize: false));

    private IMarkupRenderer? _renderer;
    private ITextInput? _input;

    /// <summary>
    /// Gets or sets the <see cref="ViewDataDictionary"/> used to pass supplementary
    /// key-value data from the controller to the view.
    /// </summary>
    /// <value>
    /// A dictionary of loosely-typed data items shared between the controller and view.
    /// </value>
    public ViewDataDictionary ViewData { get; internal set; } = new();

    /// <summary>
    /// Gets the validation errors from the action that rendered this view (see <see cref="Controller.ModelState"/>).
    /// Show them with <c>&lt;errors /&gt;</c> or <c>&lt;errors for="Name" /&gt;</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// @if (!ModelState.IsValid) { &lt;text color="red"&gt;Please correct the errors below.&lt;/text&gt; }
    /// </code>
    /// </example>
    public ModelStateDictionary ModelState { get; internal set; } = new();

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
    /// Gets the source of user input used by <c>&lt;input&gt;</c> and <c>&lt;menu&gt;</c>. The framework assigns
    /// <see cref="MvcApplicationBuilder.Input"/>; views created manually fall back to the console.
    /// </summary>
    public ITextInput Input
    {
        get => _input ??= ConsoleTextInput.Instance;
        internal set => _input = value;
    }

    /// <summary>
    /// Gets the values collected by <c>&lt;input&gt;</c> elements in this view, keyed by their <c>name</c>
    /// (case-insensitive). Post them to a controller with <c>NavigationResult.ToAction("Save", Form)</c> or a
    /// menu option marked <c>post</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;input name="Name" label="Name: " required /&gt;
    /// return NavigationResult.ToAction("Result", Form);
    /// </code>
    /// </example>
    public Dictionary<string, string> Form { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Asks for the value of a form field (<c>&lt;input&gt;</c>, <c>&lt;select&gt;</c>, <c>&lt;radio&gt;</c> or
    /// <c>&lt;checkbox&gt;</c>) and stores it in <see cref="Form"/> under the field's name. Generated code calls this for
    /// every top-level field.
    /// </summary>
    /// <param name="field">The field to read.</param>
    /// <returns>The value read; an empty string when the input has ended.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="field"/> is <see langword="null"/>.</exception>
    /// <example><code>var answer = ReadInput(new CheckboxElement { Name = "Newsletter", Label = "Send news" });</code></example>
    protected internal string ReadInput(IFormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        BeforeInteraction?.Invoke();
        var value = field.Read(Renderer, Input) ?? "";
        Form[field.Name] = value;
        return value;
    }

    /// <summary>
    /// Shows a <c>&lt;scroll&gt;</c> a page at a time and waits until the user continues. Generated code calls this for
    /// every top-level <c>&lt;scroll&gt;</c>. It does not draw a layout's footer: the footer still comes before the first field.
    /// </summary>
    /// <param name="scroll">The content to show.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scroll"/> is <see langword="null"/>.</exception>
    /// <example><code>ShowScroll(logElement);</code></example>
    protected internal void ShowScroll(ScrollElement scroll)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        scroll.Show(Renderer, Input);
    }

    /// <summary>
    /// Gets the application's keyboard shortcuts that have a description (<c>app.MapKey(ConsoleKey.F1, "Help/Index", "Help")</c>),
    /// as <c>&lt;shortcuts /&gt;</c> shows them.
    /// </summary>
    /// <example><code>&lt;text&gt;@ShortcutHints.Count shortcuts&lt;/text&gt;</code></example>
    public IReadOnlyList<ShortcutHint> ShortcutHints { get; internal set; } = [];

    /// <summary>
    /// Gets the screens shown so far, oldest first, ending with this one. A <c>&lt;menu&gt;</c> option marked
    /// <c>back</c> (or <see cref="NavigationResult.Back"/>) returns to the one before it.
    /// </summary>
    /// <example><code>var previous = NavigationHistory.Count > 1 ? NavigationHistory[^2] : null;</code></example>
    public IReadOnlyList<RouteContext> NavigationHistory { get; internal set; } = [];

    /// <summary>
    /// Gets a label for each screen in <see cref="NavigationHistory"/>: the controller for an <c>Index</c> action, else the
    /// action, e.g. <c>Home › Orders › Details</c>. <c>&lt;breadcrumbs /&gt;</c> shows them.
    /// </summary>
    /// <example><code>&lt;text&gt;@string.Join(" / ", Breadcrumbs)&lt;/text&gt;</code></example>
    public IReadOnlyList<string> Breadcrumbs =>
        NavigationHistory.Select(r => string.Equals(r.Action, "Index", StringComparison.OrdinalIgnoreCase) ? r.ControllerName : r.Action).ToArray();

    /// <summary>Theme used by views created outside the application (for example in unit tests).</summary>
    private static readonly Lazy<Theme> DefaultTheme = new(Theme.CreateDefault);

    private Theme? _theme;

    /// <summary>
    /// Gets the theme whose named styles elements use through their <c>class</c> attribute. The framework assigns
    /// <see cref="MvcApplicationBuilder.Theme"/>; views created manually use <see cref="Rendering.Theme.CreateDefault"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// // Generated code for &lt;text class="title"&gt; calls:
    /// Theme.Apply(textElement, "title");
    /// </code>
    /// </example>
    public Theme Theme
    {
        get => _theme ??= DefaultTheme.Value;
        internal set => _theme = value;
    }

    /// <summary>
    /// Gets or sets the services of the current navigation cycle, used to fill <c>@inject</c> properties of partials.
    /// Assigned by <see cref="Router.ResolveView(string, string, IServiceProvider)"/>.
    /// </summary>
    internal IServiceProvider? Services { get; set; }

    /// <summary>
    /// Prepares a partial view to render inside this view: it shares this view's renderer, input, view data, model
    /// state and <see cref="Form"/>, and receives its <c>@inject</c> services. Generated code calls this for every
    /// <c>&lt;partial&gt;</c> element.
    /// </summary>
    /// <param name="partial">The partial view about to render.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="partial"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a service the partial injects is not registered.</exception>
    /// <example>
    /// <code>
    /// // &lt;partial name="Header" model="@Model.User" /&gt; generates:
    /// var header = new MyApp.Views.Shared.Header();
    /// PreparePartial(header);
    /// header.Render(Model.User);
    /// </code>
    /// </example>
    protected internal void PreparePartial(ConsolePartialView partial)
    {
        ArgumentNullException.ThrowIfNull(partial);
        ShareStateWith(partial);
    }

    /// <summary>
    /// Fills the view's <c>@inject</c> properties. Generated views override this with plain assignments, so no reflection
    /// is needed (and trimmed applications keep working); it returns <see langword="false"/> by default, and the
    /// framework then fills <see cref="InjectAttribute"/> properties by reflection.
    /// </summary>
    /// <param name="services">The services of the current navigation cycle.</param>
    /// <returns><see langword="true"/> when the properties were filled.</returns>
    /// <example>
    /// <code>
    /// // @inject IClock Clock generates:
    /// protected override bool InjectServices(IServiceProvider services)
    /// {
    ///     this.Clock = RequireService(services, this.Clock, "Clock");
    ///     return true;
    /// }
    /// </code>
    /// </example>
    protected virtual bool InjectServices(IServiceProvider services) => false;

    /// <summary>Calls <see cref="InjectServices"/> for the framework.</summary>
    internal bool TryInjectServices(IServiceProvider services) => InjectServices(services);

    /// <summary>
    /// Returns the service of type <typeparamref name="T"/> for an <c>@inject</c> property, or throws a message naming the
    /// view and the property when it is not registered.
    /// </summary>
    /// <typeparam name="T">The service type, the type of the property.</typeparam>
    /// <param name="services">The services.</param>
    /// <param name="current">The property's current value; only its type is used, so the service type is written once.</param>
    /// <param name="property">The property's name.</param>
    /// <returns>The service.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no such service is registered.</exception>
    /// <example><code>this.Clock = RequireService(services, this.Clock, "Clock");</code></example>
    protected T RequireService<T>(IServiceProvider services, T current, string property)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService(typeof(T)) is T service
            ? service
            : throw new InvalidOperationException(
                $"'{GetType().Name}' injects '{typeof(T).Name} {property}', but no service of type '{typeof(T).FullName}' is registered. " +
                $"Register it with builder.Services, e.g. builder.Services.AddSingleton<{typeof(T).Name}, ...>();");
    }

    /// <summary>
    /// Called before this view, or a partial it renders, first waits for the user (an <c>&lt;input&gt;</c> or a
    /// <c>&lt;menu&gt;</c>). A view with a layout uses it to draw the layout's footer.
    /// </summary>
    internal Action? BeforeInteraction { get; set; }

    /// <summary>
    /// Gives <paramref name="other"/> (a partial or a layout) this view's renderer, input, view data, model state, form,
    /// theme and services, and fills its <c>@inject</c> properties.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a service <paramref name="other"/> injects is not registered.</exception>
    internal void ShareStateWith(ConsoleViewBase other)
    {
        other.BeforeInteraction = BeforeInteraction;
        other.ViewData = ViewData;
        other.ModelState = ModelState;
        other.Renderer = Renderer;
        other.Input = Input;
        other.Form = Form;
        other.Theme = Theme;
        other.NavigationHistory = NavigationHistory;
        other.ShortcutHints = ShortcutHints;
        other.Services = Services;

        ServiceInjector.InjectProperties(other, Services ?? NoServices.Value);
    }
}
